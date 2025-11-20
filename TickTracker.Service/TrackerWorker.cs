using TickTracker.Shared.Tracking;

namespace TickTracker.Service;

public class TrackerWorker : BackgroundService
{
    private readonly ForegroundTracker _tracker;

    public TrackerWorker(ForegroundTracker tracker)
    {
        _tracker = tracker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _tracker.RunAsync(stoppingToken);
    }
}