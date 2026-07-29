using System;
using System.Windows.Controls;

namespace DesktopClock.Components;

public partial class MinimalClockComponent : UserControl, IClockComponent
{
    private readonly AppSettings _settings;

    public string Id => "minimal_clock";
    public string DisplayName => "极简时钟";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public MinimalClockComponent(AppSettings settings)
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
        MinimalTimeText.Text = now.ToString(format);
    }

    public void ApplyConfig()
    {
        MinimalTimeText.FontSize = Math.Min(_settings.FontSize, 72);
        try { MinimalTimeText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.FontColor)); } catch { }
    }
}
