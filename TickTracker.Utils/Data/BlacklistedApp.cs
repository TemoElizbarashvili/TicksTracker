using System.ComponentModel.DataAnnotations;

namespace TickTracker.Utils.Data;

public class BlacklistedApp
{
    public Guid Id { get; set; }

    [MaxLength(128)]
    public string ProcessName { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }
}

