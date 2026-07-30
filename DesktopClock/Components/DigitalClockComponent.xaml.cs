using System;
using System.Windows.Controls;
using DesktopClock.Services;

namespace DesktopClock.Components;

public partial class DigitalClockComponent : UserControl, IClockComponent
{
    public string Id => "digital_clock";
    public string DisplayName => "数字时钟";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public DigitalClockComponent()
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
        TimeText.Text = DateTime.Now.ToString(format);
    }

    public void ApplyConfig()
    {
        var settings = SettingsProvider.Instance.Settings;
        try { TimeText.FontFamily = new System.Windows.Media.FontFamily(settings.FontFamily); } catch { }
        TimeText.FontSize = settings.FontSize;
        try { TimeText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.FontColor)); } catch { }
    }
}
