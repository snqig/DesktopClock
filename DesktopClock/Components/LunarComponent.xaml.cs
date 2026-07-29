using System;
using System.Windows.Controls;

namespace DesktopClock.Components;

public partial class LunarComponent : UserControl, IClockComponent
{
    private AppSettings _settings;

    public string Id => "lunar";
    public string DisplayName => "农历";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public LunarComponent(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        ApplyConfig();
    }

    public void Update(DateTime now)
    {
        if (!_settings.LunarEnabled)
        {
            Visibility = System.Windows.Visibility.Collapsed;
            return;
        }
        Visibility = System.Windows.Visibility.Visible;

        var result = LunarCalendar.GetLunarInfo(now);
        if (result.IsValid)
        {
            var parts = new System.Collections.Generic.List<string>();
            parts.Add(result.FullString);
            if (!string.IsNullOrEmpty(result.SolarTerm) && _settings.ShowSolarTerm)
                parts.Add(result.SolarTerm);
            if (!string.IsNullOrEmpty(result.Holiday))
                parts.Add(result.Holiday);
            if (_settings.ShowZodiac)
                parts.Add(result.Zodiac + "年");
            LunarTextBlock.Text = string.Join(" | ", parts);
        }
    }

    public void ApplyConfig()
    {
        LunarTextBlock.FontSize = _settings.LunarFontSize;
        try { LunarTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.LunarColor)); } catch { }
    }
}
