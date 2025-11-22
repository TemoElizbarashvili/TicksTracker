using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TickTracker.Utils.Data;
using TickTracker.Utils.Models;

namespace TickTracker.Utils.Helpers;

public static class DbOperations
{
    public static string? GetFromAppSettings(string key)
    {
        using var db = new UsageDbContext();
        return db.AppSettings.FirstOrDefault(x => x.Key == key)?.Value;
    }

    public static void SetInAppSettings(string key, string value)
    {
        using var db = new UsageDbContext();
        var setting = db.AppSettings.FirstOrDefault(x => x.Key == key);
        if (setting != null)
        {
            setting.Value = value;
        }
        else
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
        db.SaveChanges();
    }

    public static List<T> Query<T>(Expression<Func<T, bool>>? expression = null) where T : class
    {
        using var db = new UsageDbContext();

        IQueryable<T> query = db.Set<T>();
        if (expression != null)
        {
            query = query.Where(expression);
        }

        return query.AsNoTracking().ToList();
    }

    public static List<string> GetBlacklistedAppNames()
    {
        using var db = new UsageDbContext();

        return db.BlacklistedApps
            .AsNoTracking()
            .OrderBy(x => x.ProcessName)
            .Select(x => x.ProcessName)
            .ToList();
    }

    public static void BlacklistApp(string processName)
    {
        using var db = new UsageDbContext();

        var existing = db.BlacklistedApps
            .FirstOrDefault(x => x.ProcessName == processName);

        if (existing == null)
        {
            db.BlacklistedApps.Add(new BlacklistedApp
            {
                Id = Guid.CreateVersion7(),
                ProcessName = processName,
                CreatedUtc = DateTime.UtcNow
            });
        }

        var aggregates = db.AppUsageAggregates
            .Where(x => x.ProcessName == processName)
            .ToList();

        var intervals = db.AppUsageIntervals
            .Where(x => x.ProcessName == processName)
            .ToList();

        if (aggregates.Count > 0)
        {
            db.AppUsageAggregates.RemoveRange(aggregates);
        }

        if (intervals.Count > 0)
        {
            db.AppUsageIntervals.RemoveRange(intervals);
        }

        db.SaveChanges();
    }

    public static async Task RemoveItemFromBlackListAsync(string processName, CancellationToken cancellationToken = default)
    {
        await using var db = new UsageDbContext();

        var entry = await db.BlacklistedApps
            .FirstOrDefaultAsync(x => x.ProcessName == processName, cancellationToken: cancellationToken);

        if (entry == null)
        {
            return;
        }
        db.BlacklistedApps.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static List<AppUsageSummary> GetAllUsageSummaries()
    {
        using var db = new UsageDbContext();

        var blacklistedNames = db.BlacklistedApps
            .AsNoTracking()
            .Select(x => x.ProcessName)
            .ToList();

        var blacklistSet = new HashSet<string>(blacklistedNames, StringComparer.OrdinalIgnoreCase);

        var aggregates = db.AppUsageAggregates
            .AsNoTracking()
            .Where(a => !blacklistSet.Contains(a.ProcessName))
            .Select(a => new
            {
                a.ProcessName,
                a.TotalSeconds,
                a.SessionCount,
                a.FirstSeenUtc,
                a.LastSeenUtc
            })
            .ToList();

        var intervals = db.AppUsageIntervals
            .AsNoTracking()
            .Where(i => !blacklistSet.Contains(i.ProcessName))
            .ToList();

        var summaries = new Dictionary<string, AppUsageSummary>(StringComparer.OrdinalIgnoreCase);

        foreach (var agg in aggregates)
        {
            summaries[agg.ProcessName] = new AppUsageSummary
            {
                ProcessName = agg.ProcessName,
                TotalSeconds = agg.TotalSeconds,
                SessionCount = agg.SessionCount,
                FirstSeenUtc = agg.FirstSeenUtc,
                LastSeenUtc = agg.LastSeenUtc
            };
        }

        foreach (var group in intervals.GroupBy(i => i.ProcessName))
        {
            var processName = group.Key;
            var totalSeconds = group.Sum(i => (i.EndUtc - i.StartUtc).TotalSeconds);
            var firstSeen = group.Min(i => i.StartUtc);
            var lastSeen = group.Max(i => i.EndUtc);
            var sessionCount = group.Count();

            if (summaries.TryGetValue(processName, out var existing))
            {
                existing.TotalSeconds += totalSeconds;
                existing.SessionCount += sessionCount;

                if (!existing.FirstSeenUtc.HasValue || firstSeen < existing.FirstSeenUtc.Value)
                {
                    existing.FirstSeenUtc = firstSeen;
                }

                if (!existing.LastSeenUtc.HasValue || lastSeen > existing.LastSeenUtc.Value)
                {
                    existing.LastSeenUtc = lastSeen;
                }
            }
            else
            {
                summaries[processName] = new AppUsageSummary
                {
                    ProcessName = processName,
                    TotalSeconds = totalSeconds,
                    SessionCount = sessionCount,
                    FirstSeenUtc = firstSeen,
                    LastSeenUtc = lastSeen
                };
            }
        }

        return summaries.Values
            .OrderByDescending(x => x.TotalSeconds)
            .ToList();
    }


    public static async Task SaveIntervalAsync(AppUsageInterval interval, CancellationToken cancellationToken)
    {
        await using var db = new UsageDbContext();

        var isBlacklisted = await db.BlacklistedApps
            .AnyAsync(x => x.ProcessName == interval.ProcessName, cancellationToken: cancellationToken);

        if (isBlacklisted)
        {
            return;
        }
      

        db.AppUsageIntervals.Add(interval);
        await db.SaveChangesAsync(cancellationToken);

    }
}
