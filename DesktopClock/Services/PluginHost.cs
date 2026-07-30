using System;
using System.Windows;
using DesktopClock.Components;

namespace DesktopClock.Services;

public class PluginHost
{
    public string PluginDirectory { get; }

    private readonly ComponentRegistry _registry;
    private readonly Action<string> _logAction;

    public PluginHost(string pluginDir, ComponentRegistry registry, Action<string> logAction)
    {
        PluginDirectory = pluginDir;
        _registry = registry;
        _logAction = logAction;
    }

    public void RegisterComponent(string id, FrameworkElement view)
    {
        _registry.RegisterExternal(id, view);
    }

    public void UnregisterComponent(string id)
    {
        _registry.Unregister(id);
    }

    public void Log(string message)
    {
        _logAction?.Invoke(message);
    }
}
