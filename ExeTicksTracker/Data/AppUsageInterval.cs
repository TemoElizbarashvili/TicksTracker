using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExeTicksTracker.Data;
public class AppUsageInterval
{
    public Guid Id { get; set; }

    public string ProcessName { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    public DateOnly ProcessUsingDate { get; set; }

    public TimeSpan Duration => EndUtc - StartUtc;
}
