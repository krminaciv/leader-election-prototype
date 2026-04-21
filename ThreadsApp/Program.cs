using System;
using ThreadsApp;

class Program
{

    static async Task Main(string[] args)
    {
        // var etcd = new EtcdMock();
        // string sharedLeaseName = "resource-lease-1234";
        //
        // var cts0 = new CancellationTokenSource();
        // var cts1 = new CancellationTokenSource();
        // var cts2 = new CancellationTokenSource();
        // var cts3 = new CancellationTokenSource();
        // var cts4 = new CancellationTokenSource();
        //
        // var barrier = new Barrier(2);
        //
        // var member0 = new Member(sharedLeaseName, "member-0", etcd, barrier);
        // var member1 = new Member(sharedLeaseName, "member-1", etcd, barrier);
        // // var member2 = new Member(sharedLeaseName, "member-2", etcd, barrier);
        // // var member3 = new Member(sharedLeaseName, "member-3", etcd, barrier);
        // // var member4 = new Member(sharedLeaseName, "member-4", etcd, barrier);
        // var m0 = member0.RunAsync(cts0.Token);
        // var m1 = member1.RunAsync(cts1.Token);
        // // var m2 = member2.RunAsync(cts2.Token);
        // // var m3 = member3.RunAsync(cts3.Token);
        // // var m4 = member4.RunAsync(cts4.Token);
        //
        // // await Task.Delay(7000);
        // //
        // // Console.WriteLine("Killing member-0...");
        // // cts0.Cancel();
        // //
        // // await Task.Delay(10000);
        // // cts1.Cancel();
        //     
        // Task.WaitAll(m0, m1);
        //
        // Console.WriteLine("App finished.");

        Scenario1().Wait();
    }

    // two threads
    // can simulate conflict using barrier in etcd GetOrCreate()
    static async Task Scenario1()
    {
        var barrier = new Barrier(2);
        
        var etcd = new EtcdMock(barrier);
        string sharedLeaseName = "resource-lease-1234";

        var cts0 = new CancellationTokenSource();
        var cts1 = new CancellationTokenSource();

        var member0 = new Member(sharedLeaseName, "member-0", etcd, barrier);
        var member1 = new Member(sharedLeaseName, "member-1", etcd, barrier);

        var m0 = member0.RunAsync(cts0.Token);
        var m1 = member1.RunAsync(cts1.Token);
        
        await Task.Delay(7000);
        
        Console.WriteLine("Killing member-0...");
        cts0.Cancel();
        
        await Task.Delay(15000);
        cts1.Cancel();
            
        Task.WaitAll(m0, m1);

        Console.WriteLine("App finished.");
    }
    
    // static async Task Scenario2()
    // {
        // var barrier = new Barrier(2);
        //
        // var etcd = new EtcdMock(barrier);
        // string sharedLeaseName = "resource-lease-1234";
        //
        // var cts0 = new CancellationTokenSource();
        // var cts1 = new CancellationTokenSource();
        //
        // var member0 = new Member(sharedLeaseName, "member-0", etcd, barrier);
        // var member1 = new Member(sharedLeaseName, "member-1", etcd, barrier);
        //
        // var m0 = member0.RunAsync(cts0.Token);
        // var m1 = member1.RunAsync(cts1.Token);
        //
        // await Task.Delay(7000);
        //
        // Console.WriteLine("Killing member-0...");
        // cts0.Cancel();
        //
        // await Task.Delay(20000);
        // cts1.Cancel();
        //     
        // Task.WaitAll(m0, m1);
        //
        // Console.WriteLine("App finished.");
    // }
    
    // three threads
    // without barrier in Member
    static async Task Scenario3()
    {
        var barrier = new Barrier(2);
        
        var etcd = new EtcdMock(barrier);
        string sharedLeaseName = "resource-lease-1234";
    
        var cts0 = new CancellationTokenSource();
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();
    
        var member0 = new Member(sharedLeaseName, "member-0", etcd, barrier);
        var member1 = new Member(sharedLeaseName, "member-1", etcd, barrier);
        var member2 = new Member(sharedLeaseName, "member-2", etcd, barrier);
    
        var m0 = member0.RunAsync(cts0.Token);
        var m1 = member1.RunAsync(cts1.Token);
        var m2 = member2.RunAsync(cts2.Token);
    
        // await Task.Delay(50000); 
        // cts.Cancel();
    
        Task.WaitAll(m0, m1, m2);
    
        Console.WriteLine("App finished.");
    }
    
    static async Task Scenario4()
    {
        var barrier = new Barrier(2);
        
        var etcd = new EtcdMock(barrier);
        string sharedLeaseName = "resource-lease-1234";
        
        var cts0 = new CancellationTokenSource();
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();
        var cts3 = new CancellationTokenSource();
        var cts4 = new CancellationTokenSource();
        
        var member0 = new Member(sharedLeaseName, "member-0", etcd, barrier);
        var member1 = new Member(sharedLeaseName, "member-1", etcd, barrier);
        var member2 = new Member(sharedLeaseName, "member-2", etcd, barrier);
        var member3 = new Member(sharedLeaseName, "member-3", etcd, barrier);
        var member4 = new Member(sharedLeaseName, "member-4", etcd, barrier);
        var m0 = member0.RunAsync(cts0.Token);
        var m1 = member1.RunAsync(cts1.Token);
        var m2 = member2.RunAsync(cts2.Token);
        var m3 = member3.RunAsync(cts3.Token);
        var m4 = member4.RunAsync(cts4.Token);
        
        Task.WaitAll(m0, m1, m2, m3, m4);
    
        Console.WriteLine("App finished.");
    }
    
}