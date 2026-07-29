using System.IO;
using System.Text.Json;

namespace DesktopClock.Services;

public class SettingsService
{
    private static readonly string FilePath = System.IO.Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public AppSettings Settings { get; private set; } = new();

    public static SettingsService Load()
    {
        var service = new SettingsService();
        service.Settings = AppSettings.Load();
        return service;
    }

    public void Save()
    {
        Settings.Save();
    }

    public TVal GetComponentSetting<TVal>(string componentId, string key, TVal fallback)
    {
        if (Settings.Components.TryGetValue(componentId, out var cfg) && cfg.Settings.TryGetValue(key, out var val))
        {
            if (val is JsonElement je)
            {
                try { return JsonSerializer.Deserialize<TVal>(je.GetRawText()) ?? fallback; }
                catch { return fallback; }
            }
            if (val is TVal t) return t;
        }
        return fallback;
    }

    public void SetComponentSetting(string componentId, string key, object value)
    {
        if (!Settings.Components.ContainsKey(componentId))
            Settings.Components[componentId] = new Models.ComponentConfig();
        Settings.Components[componentId].Settings[key] = value;
    }

    public bool IsComponentEnabled(string componentId)
    {
        return Settings.Components.TryGetValue(componentId, out var cfg) && cfg.Enabled;
    }

    public void SetComponentEnabled(string componentId, bool enabled)
    {
        if (!Settings.Components.ContainsKey(componentId))
            Settings.Components[componentId] = new Models.ComponentConfig();
        Settings.Components[componentId].Enabled = enabled;
    }
}
