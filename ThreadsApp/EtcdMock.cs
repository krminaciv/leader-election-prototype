namespace ThreadsApp;

public class EtcdMock
{
    private readonly Dictionary<string, Lease> _leases = new();
    private readonly object _lock = new();
    
    public Lease GetOrCreate(string name, string identity)
    {
        lock (_lock)
        {
            // lease does exist
            if (_leases.TryGetValue(name, out var lease))
            {
                return Clone(lease); 
            }

            // lease does not exist
            var now = DateTime.UtcNow;
            var newLease = new Lease
            {
                Name = name,
                Holder = identity,
                AcquireTime = now,
                RenewTime = now,
                DurationSeconds = 10,
                ResourceVersion = 0
            };

            _leases[name] = newLease;

            return Clone(newLease);
        }
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