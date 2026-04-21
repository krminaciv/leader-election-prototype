using Microsoft.VisualBasic.CompilerServices;

namespace ThreadsApp;

public class EtcdMock
{
    private readonly Dictionary<string, Lease> _leases = new();
    private readonly object _lock = new();
    private readonly Barrier _barrier;

    public EtcdMock(Barrier barrier)
    {
        _barrier = barrier;
    }
    
    
    // GetOrCreate:
    //     read
    //     if not found:
    //         try create
    //         if conflict:
    //             read again
    public Lease GetOrCreate(string name, string identity)
    {
        // try to read
        var lease = Query(name);
        if (lease != null)
            return lease;
        
        _barrier.SignalAndWait();
        
        // lease does not exist, try to create
        lock (_lock)
        {
            if (!_leases.TryGetValue(name, out var existing))
            {
                var newLease = new Lease
                {
                    Name = name,
                    Holder = "", 
                    AcquireTime = null,
                    RenewTime = null,
                    DurationSeconds = 10,
                    ResourceVersion = 0
                };

                _leases[name] = newLease;
                
                Console.WriteLine($"{identity}: Successfuly created Lease - {newLease.Name}-.");
                return Clone(newLease); 
            }
        }
        
        //someone else created, try to read again
        Console.WriteLine($"{identity}: Someone else created Lease - {name}-. (CONFLICT 409)");
        return Query(name);
    }

    public bool TryAcquireOrRenew(Lease lease, string member)
    {
        lock (_lock)
        {
            if (!_leases.TryGetValue(lease.Name, out var current))
            {
                Console.WriteLine($"Lease {lease.Name} does not exist.");
                return false;
            }
            Console.WriteLine($"{member}: Lease {lease.Name} exist. its resource version is {lease.ResourceVersion}. current resource version is {current.ResourceVersion}.");
            
            if (Random.Shared.NextDouble() < 0.1)
            {
                Console.WriteLine($"===== {member} network error.");
                throw new Exception("Network error");
            }
            
            if (lease.ResourceVersion != current.ResourceVersion)
            {
                Console.WriteLine($"{member}: Conflict 409.");
                return false; // conflict (409)
            }
            
            // success
            DateTime now = DateTime.UtcNow;
            bool isNewHolder = current.Holder != member;
            
            current.ResourceVersion++;
            current.Holder = member;
            if (isNewHolder)
                current.AcquireTime = now;
            current.RenewTime = now;

            return true;
        }
    }
    
    public Lease Query(string name)
    {
        lock (_lock)
        {
            if (!_leases.TryGetValue(name, out var lease))
                return null;

            return Clone(lease);
        }
    }
    
    private Lease Clone(Lease l)
    {
        return new Lease
        {
            Name = l.Name,
            Holder = l.Holder,
            AcquireTime = l.AcquireTime,
            RenewTime = l.RenewTime,
            DurationSeconds = l.DurationSeconds,
            ResourceVersion = l.ResourceVersion
        };
    }
    
}