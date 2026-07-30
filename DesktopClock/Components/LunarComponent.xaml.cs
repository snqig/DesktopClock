using System;
using System.Windows.Controls;
using DesktopClock.Services;

namespace DesktopClock.Components;

public partial class LunarComponent : UserControl, IClockComponent
{
    public string Id => "lunar";
    public string DisplayName => "农历";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public LunarComponent()
    {
        InitializeComponent();
        SettingsProvider.Instance.SettingsChanged += OnSettingsChanged;
        ApplyConfig();
    }

    private void OnSettingsChanged()
    {
        ApplyConfig();
    }

    public void Update(DateTime now)
    {
        var settings = SettingsProvider.Instance.Settings;
        if (!settings.LunarEnabled)
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
            if (!string.IsNullOrEmpty(result.SolarTerm) && settings.ShowSolarTerm)
                parts.Add(result.SolarTerm);
            if (!string.IsNullOrEmpty(result.Holiday))
                parts.Add(result.Holiday);
            if (settings.ShowZodiac)
                parts.Add(result.Zodiac + "年");
            LunarTextBlock.Text = string.Join(" | ", parts);
        }
    }

    public void ApplyConfig()
    {
        var settings = SettingsProvider.Instance.Settings;
        LunarTextBlock.FontSize = settings.LunarFontSize;
        try { LunarTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.LunarColor)); } catch { }
    }
}
