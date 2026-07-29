namespace DesktopClock.Models;

public class GlobalConfig
{
    public string Language { get; set; } = "zh";
    public string ThemePreset { get; set; } = "default";
    public double BackgroundOpacity { get; set; } = 0.85;
    public bool ClickThrough { get; set; } = false;
    public bool SnapToEdge { get; set; } = false;
    public bool LockPosition { get; set; } = false;
    public bool AutoStart { get; set; } = false;
    public string HotkeyHide { get; set; } = "Ctrl+H";
    public double WindowWidth { get; set; } = 500;
    public double WindowHeight { get; set; } = 120;
}
