using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TickTracker.Shared.Data;

namespace TickTracker.Shared.Tracking;

public class ForegroundTracker
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowModuleFileName(IntPtr hWnd, StringBuilder lpszFileName, int cch);

    private TimeSpan _pollInterval = TimeSpan.FromSeconds(2);
    private DateOnly _lastRetentionDate = DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var db = new UsageDbContext();
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var retentionDays = 90;
        var pollSeconds = 2;
        try
        {
            var retentionSetting = db.AppSettings.FirstOrDefault(x => x.Key == "RetentionDays");
            if (retentionSetting != null &&
                int.TryParse(retentionSetting.Value, out var parsedRetention) &&
                parsedRetention > 0)
            {
                retentionDays = parsedRetention;
            }

            var pollSetting = db.AppSettings.FirstOrDefault(x => x.Key == "PollSeconds");
            if (pollSetting != null &&
                int.TryParse(pollSetting.Value, out var parsedPoll) &&
                parsedPoll >= 1 && parsedPoll <= 10)
            {
                pollSeconds = parsedPoll;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Settings read error: {ex.Message}");
        }

        _pollInterval = TimeSpan.FromSeconds(pollSeconds);

        await RunRetentionAsync(db, retentionDays, cancellationToken);

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

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (today > _lastRetentionDate && !cancellationToken.IsCancellationRequested)
            {
                await RunRetentionAsync(db, retentionDays, cancellationToken);
                _lastRetentionDate = today;
            }
        }

        if (currentProcess != null)
        {
            await SaveIntervalAsync(currentProcess, currentStartUtc, DateTime.UtcNow, cancellationToken);
        }
    }

    private static async Task RunRetentionAsync(
        UsageDbContext db,
        int retentionDays,
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-retentionDays));
            var oldIntervals = db.AppUsageIntervals
                .Where(x => x.ProcessUsingDate < cutoffDate)
                .ToList();

            if (oldIntervals.Count == 0)
            {
                return;
            }

            var grouped = oldIntervals
                .GroupBy(x => x.ProcessName);

            foreach (var group in grouped)
            {
                var processName = group.Key;
                var totalSeconds = group.Sum(i => (i.EndUtc - i.StartUtc).TotalSeconds);
                var firstSeen = group.Min(i => i.StartUtc);
                var lastSeen = group.Max(i => i.EndUtc);
                var sessionCount = group.Count();

                var aggregate = db.AppUsageAggregates
                    .FirstOrDefault(a => a.ProcessName == processName);

                if (aggregate == null)
                {
                    aggregate = new AppUsageAggregate
                    {
                        Id = Guid.CreateVersion7(),
                        ProcessName = processName,
                        TotalSeconds = totalSeconds,
                        SessionCount = sessionCount,
                        FirstSeenUtc = firstSeen,
                        LastSeenUtc = lastSeen
                    };
                    db.AppUsageAggregates.Add(aggregate);
                }
                else
                {
                    aggregate.TotalSeconds += totalSeconds;
                    aggregate.SessionCount += sessionCount;
                    if (firstSeen < aggregate.FirstSeenUtc)
                    {
                        aggregate.FirstSeenUtc = firstSeen;
                    }
                    if (lastSeen > aggregate.LastSeenUtc)
                    {
                        aggregate.LastSeenUtc = lastSeen;
                    }
                }
            }

            db.AppUsageIntervals.RemoveRange(oldIntervals);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Retention/aggregation error: {ex.Message}");
        }
    }
    private static string? GetCurrentForegroundProcessName()
    {
        var hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            return null;
        }

        var exeName = TryGetExeFriendlyNameFromWindow(hWnd);
        if (!string.IsNullOrWhiteSpace(exeName))
        {
            return exeName;
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

    private static string? TryGetExeFriendlyNameFromWindow(IntPtr hWnd)
    {
        var sb = new StringBuilder(1024);
        var len = GetWindowModuleFileName(hWnd, sb, sb.Capacity);
        if (len <= 0)
        {
            return null;
        }

        var fullPath = sb.ToString();
        var rawName = Path.GetFileNameWithoutExtension(fullPath);

        try
        {
            var info = FileVersionInfo.GetVersionInfo(fullPath);

            if (!string.IsNullOrWhiteSpace(info.FileDescription))
                return info.FileDescription.Trim();

            if (!string.IsNullOrWhiteSpace(info.ProductName))
                return info.ProductName.Trim();
        }
        catch
        {
            // if reading version info fails, just fall back to raw exe name
        }

        return rawName;
    }



    private static async Task SaveIntervalAsync(
        string processName,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken token)
    {
        if (endUtc <= startUtc) return;

        // Do not track this tracker or the UI viewer itself
        if (string.Equals(processName, "ExeTicksTracker", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(processName, "TickTracker", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

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
