namespace ExeTickTracker.UI.Models;

internal class AppUsageSummary
{
    public string ProcessName { get; set; } = default!;
    public double TotalSeconds { get; set; }
    public DateTime? FirstSeenUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public int SessionCount { get; set; }

    // For UI: hh:mm:ss
    public string TotalTimeFormatted =>
        TimeSpan.FromSeconds(TotalSeconds).ToString(@"hh\:mm\:ss");

    public string FirstSeenLocal =>
        FirstSeenUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";

    public string LastSeenLocal =>
        LastSeenUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";

    public string TotalHoursDisplay
    {
        get
        {
            if (TotalSeconds >= 3600)
            {
                var hours = TotalSeconds / 3600d;
                return $"{hours:0.0} hours";
            }

            var minutes = TotalSeconds / 60d;
            return $"{minutes:0.0} minutes";
        }
    }
}
