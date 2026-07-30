using DesktopClock.Services;

namespace DesktopClock.Contracts;

public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Description { get; }

    void Load(PluginHost host);
    void Unload();
}
