using System;
using System.Windows.Controls;
using DesktopClock.Services;

namespace DesktopClock.Components;

public partial class DateComponent : UserControl, IClockComponent
{
    public string Id => "date";
    public string DisplayName => "日期";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public DateComponent()
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
        if (!settings.ShowDate)
        {
            Visibility = System.Windows.Visibility.Collapsed;
            return;
        }
        Visibility = System.Windows.Visibility.Visible;
        var culture = settings.Language == "en"
            ? System.Globalization.CultureInfo.GetCultureInfo("en-US")
            : System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
        DateTextBlock.Text = now.ToString("yyyy-MM-dd dddd", culture);
    }

    public void ApplyConfig()
    {
        var settings = SettingsProvider.Instance.Settings;
        try { DateTextBlock.FontFamily = new System.Windows.Media.FontFamily(settings.DateFontFamily); } catch { }
        DateTextBlock.FontSize = settings.DateFontSize;
        try { DateTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.DateColor)); } catch { }
    }
}
