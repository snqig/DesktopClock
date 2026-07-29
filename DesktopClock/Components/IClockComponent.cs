using System.Windows;
using DesktopClock.Models;

namespace DesktopClock.Components;

public interface IClockComponent
{
    string Id { get; }
    string DisplayName { get; }
    FrameworkElement View { get; }
    ComponentConfig Config { get; set; }
    void Update(DateTime now);
    void ApplyConfig();
}
