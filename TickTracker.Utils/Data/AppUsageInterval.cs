using System.ComponentModel.DataAnnotations;

namespace TickTracker.Utils.Data;

public class AppUsageInterval
{
    public Guid Id { get; set; }

    [MaxLength(128)]
    public string ProcessName { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    public DateOnly ProcessUsingDate { get; set; }

    public TimeSpan Duration => EndUtc - StartUtc;
}
