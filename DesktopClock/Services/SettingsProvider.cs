using DesktopClock.Models;

namespace DesktopClock.Services;

public class SettingsProvider
{
    private static SettingsProvider? _instance;
    private AppSettings _settings = new();

    public static SettingsProvider Instance => _instance ??= new();

    public AppSettings Settings => _settings;

    public event Action? SettingsChanged;

    public void UpdateSettings(AppSettings newSettings)
    {
        _settings = newSettings;
        SettingsChanged?.Invoke();
    }
}