using System;
using System.Windows.Controls;

namespace DesktopClock.Components;

public partial class DigitalClockComponent : UserControl, IClockComponent
{
    private readonly AppSettings _settings;

    public string Id => "digital_clock";
    public string DisplayName => "数字时钟";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public DigitalClockComponent(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        ApplyConfig();
    }

    public void Update(DateTime now)
    {
        var format = _settings.Use24Hour ? "HH" : "hh";
        format += ":mm";
        if (_settings.ShowSeconds) format += ":ss";
        TimeText.Text = now.ToString(format);
    }

    public void ApplyConfig()
    {
        try { TimeText.FontFamily = new System.Windows.Media.FontFamily(_settings.FontFamily); } catch { }
        TimeText.FontSize = _settings.FontSize;
        try { TimeText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.FontColor)); } catch { }
    }
}
