namespace ThreadsApp;

public class EtcdSimulator
{
    private readonly Dictionary<string, Lease> _leases = new();
    private readonly object _lock = new();
    
    public Lease GetOrCreate(string name, string identity)
    {
        lock (_lock)
        {
            if (!_leases.TryGetValue(name, out var lease))
            {
                lease = new Lease
                {
                    Name = name,
                    Holder = identity,
                    AcquireTime = DateTime.UtcNow,
                    RenewTime = DateTime.UtcNow,
                    DurationSeconds = 10,
                    ResourceVersion = 0
                };

                _leases[name] = lease;

                Console.WriteLine($"[{identity}] I created a Lease.");
            }
            else
            {
                Console.WriteLine($"[{identity}] Lease exists. Holder is {lease.Holder}");
            }

            return lease;
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
            Console.WriteLine($"{member}: Lease {current.Name} exist. its resource version is {current.ResourceVersion}.");
            
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
            bool isNewHolder = lease.Holder == member;
            
            lease.ResourceVersion++;
            lease.Holder = member;
            lease.AcquireTime = (isNewHolder || lease.AcquireTime == null) ? now : lease.AcquireTime;
            lease.RenewTime = now;

            return true;
        }
    }
    
}