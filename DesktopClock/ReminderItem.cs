using System;

namespace DesktopClock;

public class ReminderItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? DateTime { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public TimeSpan TimeOfDay { get; set; }
    public bool IsRecurring { get; set; }
    public bool IsEnabled { get; set; } = true;
}
