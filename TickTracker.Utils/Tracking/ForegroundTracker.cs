using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using TickTracker.Utils.Data;
using TickTracker.Utils.Helpers;

namespace TickTracker.Utils.Tracking;

public class ForegroundTracker
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private static bool _ignoreWindowsApps = true;

    private static TimeSpan _pollInterval = TimeSpan.FromSeconds(2);
    private static int _retentionDays = 90;
    private static DateOnly _lastRetentionDate = DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await ExtractSettings(cancellationToken);

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
                await using var db = new UsageDbContext();
                await RunRetentionAsync(db, _retentionDays, cancellationToken);
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

        GetWindowThreadProcessId(hWnd, out var pid);
        if (pid == 0)
        {
            return null;
        }

        using var proc = Process.GetProcessById((int)pid);

        var windowsDir =
                Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        try
        {
            if (_ignoreWindowsApps)
            {
                if (proc.MainModule?.FileName != null &&
                    proc.MainModule.FileName.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            return string.IsNullOrEmpty(proc.MainModule?.FileVersionInfo.FileDescription) ? (string.IsNullOrEmpty(proc.MainModule?.FileVersionInfo.ProductName) ? proc.ProcessName
                    : proc.MainModule?.FileVersionInfo.ProductName)
                : proc.MainModule?.FileVersionInfo.FileDescription;
        }
        catch
        {
            // Access denied or process exited
            return proc.ProcessName;
        }
    }

    private static async Task SaveIntervalAsync(
        string processName,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken token)
    {
        if (endUtc <= startUtc) return;

        if (string.Equals(processName, "TicksTracker", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(processName, "TickTracker", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }


        var interval = new AppUsageInterval
        {
            Id = Guid.CreateVersion7(),
            ProcessName = processName,
            StartUtc = startUtc,
            EndUtc = endUtc,
            ProcessUsingDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        await DbOperations.SaveIntervalAsync(interval, token);
    }

    private static async Task ExtractSettings(CancellationToken cancellationToken)
    {
        await using var db = new UsageDbContext();

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);

        if (pendingMigrations.Any())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        try
        {
            var retentionSetting = db.AppSettings.FirstOrDefault(x => x.Key == Constants.RetentionDaysKey);
            if (retentionSetting != null &&
                int.TryParse(retentionSetting.Value, out var parsedRetention) &&
                parsedRetention > 0)
            {
                _retentionDays = parsedRetention;
            }

            var pollSetting = db.AppSettings.FirstOrDefault(x => x.Key == Constants.PollSecondsKey);
            if (pollSetting != null &&
                int.TryParse(pollSetting.Value, out var parsedPoll) &&
                parsedPoll is >= 1 and <= 10)
            {
                _pollInterval = TimeSpan.FromSeconds(parsedPoll);
            }

            var ignoreWindowsSetting = db.AppSettings.FirstOrDefault(x => x.Key == Constants.IgnoreWindowsAppsKey);
            if (ignoreWindowsSetting != null &&
                bool.TryParse(ignoreWindowsSetting.Value, out var ignoreWindowsApps))
            {
                _ignoreWindowsApps = ignoreWindowsApps;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Settings read error: {ex.Message}");
        }

        await RunRetentionAsync(db, _retentionDays, cancellationToken);
    }
}
