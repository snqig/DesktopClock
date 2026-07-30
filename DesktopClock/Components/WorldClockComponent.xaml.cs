using System;
using System.Windows.Controls;
using DesktopClock.Services;

namespace DesktopClock.Components;

public partial class WorldClockComponent : UserControl, IClockComponent
{
    public string Id => "world_clock";
    public string DisplayName => "世界时钟";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public WorldClockComponent()
    {
        InitializeComponent();
    }

    public void Update(DateTime now)
    {
        var settings = SettingsProvider.Instance.Settings;
        if (!settings.WorldClockEnabled)
        {
            Visibility = System.Windows.Visibility.Collapsed;
            return;
        }
        Visibility = System.Windows.Visibility.Visible;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(settings.WorldClockTimeZone);
            var worldTime = TimeZoneInfo.ConvertTime(now, tz);
            var wf = settings.Use24Hour ? "HH:mm" : "hh:mm";
            if (settings.ShowSeconds) wf += ":ss";
            WorldClockTextBlock.Text = worldTime.ToString(wf);
        }
        catch
        {
            Visibility = System.Windows.Visibility.Collapsed;
        }
    }

    public void ApplyConfig() { }
}
