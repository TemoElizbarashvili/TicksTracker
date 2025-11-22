using System.ComponentModel.DataAnnotations;

namespace TickTracker.Utils.Data;

public class AppSetting
{

    [MaxLength(256)]
    public string Key { get; set; } = string.Empty;
    [MaxLength(256)]
    public string Value { get; set; } = string.Empty;
}
