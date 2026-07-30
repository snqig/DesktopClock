using System.Windows;

namespace DesktopClock.Contracts;

public interface IPluginComponent : IPlugin
{
    string ComponentId { get; }
    FrameworkElement View { get; }
}
