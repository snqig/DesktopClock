using System.IO;
using System.Text.Json;

namespace DesktopClock;

public class AppSettings
{
    public double FontSize { get; set; } = 64;
    public double BackgroundOpacity { get; set; } = 0.85;
    public string FontColor { get; set; } = "#00d4ff";
    public string FontFamily { get; set; } = "DS-Digital";

    public bool ShowDate { get; set; } = true;
    public string DateFontFamily { get; set; } = "DS-Digital";
    public double DateFontSize { get; set; } = 16;
    public string DateColor { get; set; } = "#00FF00";
    public string DatePosition { get; set; } = "top";

    public bool Use24Hour { get; set; } = true;
    public bool ShowSeconds { get; set; } = true;
    public string DisplayMode { get; set; } = "digital";
    public string BackgroundType { get; set; } = "solid";
    public string GradientStartColor { get; set; } = "#1a1a2e";
    public string GradientEndColor { get; set; } = "#16213e";
    public double GradientAngle { get; set; } = 45;
    public string BorderColor { get; set; } = "#00d4ff";
    public double BorderThickness { get; set; } = 1;
    public bool LunarEnabled { get; set; } = false;
    public bool ShowSolarTerm { get; set; } = true;
    public bool ShowZodiac { get; set; } = true;
    public double LunarFontSize { get; set; } = 14;
    public string LunarColor { get; set; } = "#aaaaaa";
    public bool ChimeEnabled { get; set; } = false;
    public bool ReminderEnabled { get; set; } = false;
    public string RemindersJson { get; set; } = "[]";
    public bool WorldClockEnabled { get; set; } = false;
    public string WorldClockTimeZone { get; set; } = "China Standard Time";
    public string HotkeyHide { get; set; } = "Ctrl+H";
    public string Language { get; set; } = "zh";
    public string ThemePreset { get; set; } = "default";
    public bool SnapToEdge { get; set; } = false;
    public bool AutoStart { get; set; } = false;
    public bool LockPosition { get; set; } = false;
    public bool ClickThrough { get; set; } = false;

    private static readonly string FilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
        }
        return new AppSettings();
    }
}
