using ExeTicksTracker.Tracking;

Console.WriteLine("AppUsageTracker started. Press Ctrl+C to exit.");

using var cts = new CancellationTokenSource();


Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("Stopping tracker...");
};

var tracker = new ForegroundTracker();
await tracker.RunAsync(cts.Token);

Console.WriteLine("Tracker stopped.");
