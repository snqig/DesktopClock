using System.Windows;
using DesktopClock.Models;

namespace DesktopClock.Components;

public abstract class ClockComponentBase : IClockComponent
{
    public string Id { get; protected set; } = string.Empty;
    public string DisplayName { get; protected set; } = string.Empty;
    public FrameworkElement View { get; protected set; } = null!;
    public ComponentConfig Config { get; set; } = new();

    public abstract void Update(DateTime now);

    public virtual void ApplyConfig()
    {
    }
}
