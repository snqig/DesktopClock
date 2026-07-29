using System;
using System.Windows.Controls;

namespace DesktopClock.Components;

public partial class DateComponent : UserControl, IClockComponent
{
    private AppSettings _settings;

    public string Id => "date";
    public string DisplayName => "日期";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public DateComponent(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        ApplyConfig();
    }

    public void Update(DateTime now)
    {
        if (!_settings.ShowDate)
        {
            Visibility = System.Windows.Visibility.Collapsed;
            return;
        }
        Visibility = System.Windows.Visibility.Visible;
        var culture = _settings.Language == "en"
            ? System.Globalization.CultureInfo.GetCultureInfo("en-US")
            : System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
        DateTextBlock.Text = now.ToString("yyyy-MM-dd dddd", culture);
    }

    public void ApplyConfig()
    {
        try { DateTextBlock.FontFamily = new System.Windows.Media.FontFamily(_settings.DateFontFamily); } catch { }
        DateTextBlock.FontSize = _settings.DateFontSize;
        try { DateTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.DateColor)); } catch { }
    }
}
