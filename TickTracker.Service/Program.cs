using TickTracker.Service;
using TickTracker.Shared.Tracking;

Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices(services =>
    {
        services.AddSingleton<ForegroundTracker>();
        services.AddHostedService<TrackerWorker>();
    })
    .Build()
    .Run();