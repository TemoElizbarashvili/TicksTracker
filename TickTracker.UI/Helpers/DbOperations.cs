using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TickTracker.UI.Models;
using TickTracker.Utils.Data;

namespace TickTracker.UI.Helpers;

internal static class DbOperations
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

    public static List<AppUsageSummary> GetAllUsageSummaries()
    {
        using var db = new UsageDbContext();

        var aggregates = db.AppUsageAggregates
            .AsNoTracking()
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
}
