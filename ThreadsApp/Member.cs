using ThreadsApp;

namespace ThreadsApp;

public class Member
{
    private readonly string _leaseName;
    private readonly string _identity;
    private readonly EtcdSimulator _etcd;
    private readonly Barrier _barrier;

    private readonly int _leaderDelayMs = 2000;
    private readonly int _followerDelayMs = 2000;

    public Member(string leaseName, string identity, EtcdSimulator etcd, Barrier barrier)
    {
        _leaseName = leaseName;
        _identity = identity;
        _etcd = etcd;
        _barrier = barrier;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("Member started with identity: " + _identity);
        Random waitTime = new Random();
        
        //await Task.Delay(waitTime.Next(1000,3000), ct);
        await Task.Delay(1000, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                //await Task.Delay(waitTime.Next(1000,3000), ct);
                //await Task.Delay(2000, ct);
                
                // var lease = GetOrCreateLease(_leaseName, _identity);
                var lease = _etcd.GetOrCreate(_leaseName, _identity);
                
                //_barrier.SignalAndWait();
                await Task.Delay(100);

                bool isExpired = lease.isExpired();
                bool isMine = lease.isMine(_identity);
                
                // try to acquire
                if (isExpired || isMine)
                {
                    if (isExpired && !isMine)
                    {
                        Console.WriteLine($"{_identity}: Lease expired and I'm trying to acquire.");
                    }
                    var success = _etcd.TryAcquireOrRenew(lease, _identity);

                    if (success)
                    {
                        Console.WriteLine($"{_identity}: I AM HOLDER!");
                        await Task.Delay(_leaderDelayMs, ct);
                    }
                    else
                    {
                        Console.WriteLine($"{_identity}: FAILED! (conflict 409)");
                        await Task.Delay(_followerDelayMs, ct);
                    }
                }
                else
                {
                    await Task.Delay(_followerDelayMs, ct);
                }
            }
            catch (TaskCanceledException)
            {
               Console.WriteLine("Member canceled: " + _identity);
               break;
            }
            
        }
        Console.WriteLine("Member stopped with identity: " +  _identity);
    }
    
    
    
}