using ExeTicksTracker.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ExeTicksTracker.Tracking;

public class ForegroundTracker
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var db = new UsageDbContext();
        await db.Database.EnsureCreatedAsync(cancellationToken);

        string? currentProcess = null;
        var currentStartUtc = DateTime.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var newProcess = GetCurrentForegroundProcessName();

                if (newProcess != null &&
                    !string.Equals(newProcess, currentProcess, StringComparison.OrdinalIgnoreCase))
                {
                    if (currentProcess != null)
                    {
                        await SaveIntervalAsync(currentProcess, currentStartUtc, DateTime.UtcNow, cancellationToken);
                    }

                    currentProcess = newProcess;
                    currentStartUtc = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Tracking error: {ex.Message}");
            }

            try
            {
                await Task.Delay(_pollInterval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            // app is shutting down: close last interval
            if (currentProcess != null)
            {
                await SaveIntervalAsync(currentProcess, currentStartUtc, DateTime.UtcNow, cancellationToken);
            }
        }


    }

    private static string? GetCurrentForegroundProcessName()
    {
        var hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(hWnd, out var pid);
        if (pid == 0)
        {
            return null;
        }

        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static async Task SaveIntervalAsync(
        string processName,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken token)
    {
        if (endUtc <= startUtc) return;

        await using var db = new UsageDbContext();
        var interval = new AppUsageInterval
        {
            Id = Guid.CreateVersion7(),
            ProcessName = processName,
            StartUtc = startUtc,
            EndUtc = endUtc,
            ProcessUsingDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        db.AppUsageIntervals.Add(interval);
        await db.SaveChangesAsync(token);
    }
}
