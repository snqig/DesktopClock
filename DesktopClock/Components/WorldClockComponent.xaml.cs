using System;
using System.Windows.Controls;

namespace DesktopClock.Components;

public partial class WorldClockComponent : UserControl, IClockComponent
{
    private AppSettings _settings;

    public string Id => "world_clock";
    public string DisplayName => "世界时钟";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public WorldClockComponent(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
    }

    public void Update(DateTime now)
    {
        if (!_settings.WorldClockEnabled)
        {
            Visibility = System.Windows.Visibility.Collapsed;
            return;
        }
        Visibility = System.Windows.Visibility.Visible;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(_settings.WorldClockTimeZone);
            var worldTime = TimeZoneInfo.ConvertTime(now, tz);
            var wf = _settings.Use24Hour ? "HH:mm" : "hh:mm";
            if (_settings.ShowSeconds) wf += ":ss";
            WorldClockTextBlock.Text = worldTime.ToString(wf);
        }
        catch
        {
            Visibility = System.Windows.Visibility.Collapsed;
        }
    }

    public void ApplyConfig() { }
}
