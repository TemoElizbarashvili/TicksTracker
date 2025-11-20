namespace ExeTicksTracker.Data;

public class AppUsageAggregate
{
    public Guid Id { get; set; }

    public string ProcessName { get; set; } = string.Empty;

    // Total accumulated seconds across all aggregated (old) intervals
    public double TotalSeconds { get; set; }

    public int SessionCount { get; set; }

    public DateTime FirstSeenUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }
}

