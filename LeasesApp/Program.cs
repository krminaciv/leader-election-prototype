using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Sinks.Elasticsearch;

class Program
{
    static async Task Main(string[] args)
    {
        
        // Logger
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console() 
            .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://192.168.101.50:9200")) {
                AutoRegisterTemplate = true,
                IndexFormat = "leader-logs-{0:yyyy.MM}"
            })
            .CreateLogger();
        
        var cts = new CancellationTokenSource();
        var worker = new LeaderWorker();
        
        var shutdownCompleted = new TaskCompletionSource();

        // Ctrl+C for local
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            Log.Warning("SIGINT received...");
            cts.Cancel();
        };
        
        // Kubernetes SIGTERM
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, async ctx =>
            {
                Log.Warning("SIGTERM received...");

                ctx.Cancel = true;
                cts.Cancel();

                try
                {
                    await worker.ReleaseLeaseAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning("Shutdown error: {Message}", ex.Message);
                }

                shutdownCompleted.SetResult();
            });
        }
        
        try
        {
            Log.Information("App starting...");

            await worker.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Worker stopped.");
        }
        
        //wait for shutdown hook
        await shutdownCompleted.Task;
        
        Log.Information("App stopped.");
        Log.CloseAndFlush();
    }
}