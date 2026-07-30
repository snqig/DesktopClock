using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DesktopClock.Components;
using DesktopClock.Contracts;
using DesktopClock.Models;

namespace DesktopClock.Services;

public class PluginManager
{
    private readonly string _pluginsPath;
    private readonly ComponentRegistry _registry;
    private readonly Dictionary<string, IPlugin> _loadedPlugins = new();
    private readonly List<string> _log = new();

    public IReadOnlyList<string> Log => _log;
    public IReadOnlyDictionary<string, IPlugin> LoadedPlugins => _loadedPlugins;

    public PluginManager(string pluginsPath, ComponentRegistry registry)
    {
        _pluginsPath = pluginsPath;
        _registry = registry;
    }

    public void LoadAll(Dictionary<string, bool>? pluginSettings)
    {
        _loadedPlugins.Clear();
        _log.Clear();

        if (!Directory.Exists(_pluginsPath))
        {
            _log.Add($"[PluginManager] Plugin directory not found: {_pluginsPath}");
            return;
        }

        foreach (var dllPath in Directory.GetFiles(_pluginsPath, "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllPath);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                    .ToList();

                if (pluginTypes.Count == 0) continue;

                foreach (var type in pluginTypes)
                {
                    var plugin = (IPlugin)Activator.CreateInstance(type)!;

                    // Check if enabled
                    if (pluginSettings != null && pluginSettings.TryGetValue(plugin.Id, out var enabled) && !enabled)
                    {
                        _log.Add($"[PluginManager] Plugin '{plugin.Id}' is disabled, skipping");
                        continue;
                    }

                    var host = new PluginHost(Path.GetDirectoryName(dllPath)!, _registry, msg => _log.Add(msg));
                    plugin.Load(host);
                    _loadedPlugins[plugin.Id] = plugin;
                    _log.Add($"[PluginManager] Loaded plugin: {plugin.Name} v{plugin.Version}");
                }
            }
            catch (Exception ex)
            {
                _log.Add($"[PluginManager] Failed to load {dllPath}: {ex.Message}");
            }
        }
    }

    public void UnloadPlugin(string id)
    {
        if (_loadedPlugins.TryGetValue(id, out var plugin))
        {
            try
            {
                plugin.Unload();
                _loadedPlugins.Remove(id);
                _log.Add($"[PluginManager] Unloaded plugin: {plugin.Name}");
            }
            catch (Exception ex)
            {
                _log.Add($"[PluginManager] Error unloading plugin '{id}': {ex.Message}");
            }
        }
    }

    public void UnloadAll()
    {
        foreach (var id in _loadedPlugins.Keys.ToList())
            UnloadPlugin(id);
    }
}
