using k8s;
using k8s.Models;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s.Autorest;
using Serilog;

public class LeaderWorker
{
    private readonly IKubernetes _client;

    private readonly string _leaseName =
        Environment.GetEnvironmentVariable("LEASE_NAME") ?? "leader-lease";

    private readonly string _namespace =
        Environment.GetEnvironmentVariable("POD_NAMESPACE") ?? "lease-test";

    private readonly string _identity =
        Environment.GetEnvironmentVariable("POD_NAME") ??
        $"{Environment.MachineName}-{Guid.NewGuid()}";

    private readonly int _leaderDelayMs =
        int.TryParse(Environment.GetEnvironmentVariable("LEADER_DELAY_MS"), out var l) ? l : 1000;

    private readonly int _followerDelayMs =
        int.TryParse(Environment.GetEnvironmentVariable("FOLLOWER_DELAY_MS"), out var f) ? f : 500;

    private readonly int _leaseDurationSeconds =
        int.TryParse(Environment.GetEnvironmentVariable("LEASE_DURATION_SECONDS"), out var d) ? d : 5;

    private readonly bool _useLeaseLock =
        bool.TryParse(Environment.GetEnvironmentVariable("USE_LEASE_LOCK"), out var u) ? u : true;

    public LeaderWorker()
    {
        var config = KubernetesClientConfiguration.InClusterConfig();
        _client = new Kubernetes(config);
    }

    public async Task RunAsync(CancellationToken token)
    {

        Log.Information("Worker started: {Identity}. Lock mode: {LockMode}", _identity, _useLeaseLock);

        while (!token.IsCancellationRequested)
        {
            try
            {
                
                // without lock
                if (!_useLeaseLock)
                {
                    Log.Information("{Identity}: I am the leader (NO LOCK MODE)", _identity);
                    await Task.Delay(_leaderDelayMs, token);
                    continue;
                }

                // with lease lock
                var lease = await GetOrCreateLeaseAsync();

                var now = DateTime.UtcNow;
                var renewTime = lease.Spec.RenewTime;
                var duration = lease.Spec.LeaseDurationSeconds ?? _leaseDurationSeconds;

                bool isExpired = renewTime == null || renewTime.Value.AddSeconds(duration) < now;
                bool amILeader = lease.Spec.HolderIdentity == _identity;

                //i can try to acquire onlly if: lease expired || already a leader (renew)
                if (isExpired || amILeader)
                {
                    var success = await TryAcquireOrRenewLeaseAsync(lease);
                    
                    if (success)
                    {
                        Log.Information("{Identity}: I AM LEADER", _identity);
                        await Task.Delay(_leaderDelayMs, token);
                    }
                    else
                    {
                        Log.Warning("{Identity}: FAILED (lost).", _identity);
                        await Task.Delay(_followerDelayMs, token);
                    }
                }
                else
                {
                    await Task.Delay(_followerDelayMs, token);
                }
            }
            catch (TaskCanceledException)
            {
                Log.Information("Work canceled for {Identity}", _identity);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in election loop for {Identity}", _identity);
                await Task.Delay(_followerDelayMs);
            }
        }

        Log.Information("Worker {Identity} shutting down...", _identity);
    }

    // note: ovde se kreira lease ali se niko ne postavlja za holdera, pa se kasnije u acqOrRenew bore za leaase
    private async Task<V1Lease> GetOrCreateLeaseAsync()
    {
        try
        {
            // read lease obj
            return await _client.CoordinationV1.ReadNamespacedLeaseAsync(_leaseName, _namespace);
        }
        catch (k8s.Autorest.HttpOperationException ex) 
            when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var lease = new V1Lease
            {
                Metadata = new V1ObjectMeta 
                    { Name = _leaseName, 
                        NamespaceProperty = _namespace 
                    },
                Spec = new V1LeaseSpec
                {
                    HolderIdentity = "",
                    LeaseDurationSeconds = _leaseDurationSeconds
                }
            };
            
            try
            {
                return await _client.CoordinationV1.CreateNamespacedLeaseAsync(lease, _namespace);
            }
            catch (k8s.Autorest.HttpOperationException createEx)
                when (createEx.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // someone else created lease, just read
                return await _client.CoordinationV1.ReadNamespacedLeaseAsync(_leaseName, _namespace);
            }
        }
    }

    private async Task<bool> TryAcquireOrRenewLeaseAsync(V1Lease lease)
    {
        var now = DateTime.UtcNow;

        var isNewLeader = lease.Spec.HolderIdentity != _identity;
        
        var newLease = new V1Lease
        {
            Metadata = new V1ObjectMeta
            {
                Name = lease.Metadata.Name,
                NamespaceProperty = lease.Metadata.NamespaceProperty,
                ResourceVersion = lease.Metadata.ResourceVersion
            },
            Spec = new V1LeaseSpec
            {
                HolderIdentity = _identity,
                LeaseDurationSeconds = _leaseDurationSeconds, //or: lease.Spec.LeaseDurationSeconds ?,
                AcquireTime = (isNewLeader || lease.Spec.AcquireTime == null) 
                    ? now 
                    : lease.Spec.AcquireTime,
                RenewTime = now
            }
        };
        
        try
        {
            await _client.CoordinationV1.ReplaceNamespacedLeaseAsync(
                newLease,
                name: _leaseName,
                namespaceParameter: _namespace
            );

            //win
            return true; 
        }
        catch (k8s.Autorest.HttpOperationException ex)
            when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            //lost
            return false;
        }
    }
    
    public async Task ReleaseLeaseAsync()
    {
        //Log.Information("DEBUG: ReleaseLeaseAsync called by {Identity}", _identity);
        
        try
        {
            var lease = await GetOrCreateLeaseAsync();
            
            if (lease.Spec.HolderIdentity == _identity)
            {
                Log.Information("Releasing lease before shutdown...");

                var newLease = new V1Lease
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = lease.Metadata.Name,
                        NamespaceProperty = lease.Metadata.NamespaceProperty,
                        ResourceVersion = lease.Metadata.ResourceVersion
                    },
                    Spec = new V1LeaseSpec
                    {
                        HolderIdentity = "",
                        LeaseDurationSeconds = lease.Spec.LeaseDurationSeconds,
                        RenewTime = DateTime.UtcNow.AddDays(-1)
                    }
                };

                try
                {
                    await _client.CoordinationV1.ReplaceNamespacedLeaseAsync(newLease, _leaseName, _namespace);
                    Log.Information("Lease released successfully.");
                }
                catch (k8s.Autorest.HttpOperationException ex)
                    when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    Log.Information("Lease already taken by another worker.");
                }
            }
        }
        catch (Exception ex)   
        {
            Log.Warning("Failed to release lease during shutdown: {Message}", ex.Message);
        }
    }
    
}