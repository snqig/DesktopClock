using System;
using System.Windows.Controls;
using DesktopClock.Services;

namespace DesktopClock.Components;

public partial class MinimalClockComponent : UserControl, IClockComponent
{
    public string Id => "minimal_clock";
    public string DisplayName => "极简时钟";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public MinimalClockComponent()
    {
        InitializeComponent();
        ApplyConfig();
        SettingsProvider.Instance.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        ApplyConfig();
    }

    public void Update(DateTime now)
    {
        var settings = SettingsProvider.Instance.Settings;
        var format = settings.Use24Hour ? "HH" : "hh";
        format += ":mm";
        if (settings.ShowSeconds) format += ":ss";
        MinimalTimeText.Text = now.ToString(format);
    }

    public void ApplyConfig()
    {
        var settings = SettingsProvider.Instance.Settings;
        MinimalTimeText.FontSize = Math.Min(settings.FontSize, 72);
        try { MinimalTimeText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.FontColor)); } catch { }
    }
}
