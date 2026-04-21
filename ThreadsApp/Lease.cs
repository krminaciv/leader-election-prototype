using ThreadsApp;

namespace ThreadsApp;

public class Lease
{
    public string Name { get; set; }

    public string Holder { get; set; }
    public DateTime? AcquireTime { get; set; }
    public DateTime? RenewTime { get; set; }
    public int DurationSeconds { get; set; }
    public long ResourceVersion { get; set; }

    // public Lease(string name)
    // {
    //     Name = name;
    //     Console.WriteLine("Created lease with name: " + Name);
    // }

    public void RenewLease(string holder)
    {
        RenewTime = DateTime.UtcNow;
        ResourceVersion++;
    }

    public string GetHolder() => Holder;

    public bool isExpired()
    {
        var now = DateTime.UtcNow;
        bool isExpired = RenewTime == null || RenewTime.Value.AddSeconds(DurationSeconds) < now;
        return isExpired;
    }

    public bool isMine(string holder)
    {
        return Holder == holder;
    }
}