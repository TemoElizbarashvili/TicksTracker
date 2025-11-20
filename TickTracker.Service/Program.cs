using TickTracker.Service;
using TickTracker.Utils.Tracking;

Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices(services =>
    {
        services.AddSingleton<ForegroundTracker>();
        services.AddHostedService<TrackerWorker>();
    })
    .Build()
    .Run();