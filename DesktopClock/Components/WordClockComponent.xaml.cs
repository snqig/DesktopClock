using System;
using System.Windows.Controls;
using DesktopClock.Services;

namespace DesktopClock.Components;

public partial class WordClockComponent : UserControl, IClockComponent
{
    public string Id => "word_clock";
    public string DisplayName => "文字时钟";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public WordClockComponent()
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
        int h = settings.Use24Hour ? now.Hour : (now.Hour % 12 == 0 ? 12 : now.Hour % 12);
        WordTimeText.Text = TimeToChinese(h, now.Minute, now.Second);
    }

    private string NumberToChinese(int n)
    {
        string[] digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
        if (n < 10) return digits[n];
        if (n < 20) { if (n == 10) return "十"; return "十" + digits[n % 10]; }
        int tens = n / 10;
        int ones = n % 10;
        if (ones == 0) return digits[tens] + "十";
        return digits[tens] + "十" + digits[ones];
    }

    private string TimeToChinese(int h, int m, int s)
    {
        var settings = SettingsProvider.Instance.Settings;
        string hStr = h == 0 ? "零时" : NumberToChinese(h) + "点";
        if (m == 0 && s == 0) return hStr + "整";
        string result = hStr + NumberToChinese(m) + "分";
        if (settings.ShowSeconds) result += NumberToChinese(s) + "秒";
        return result;
    }

    public void ApplyConfig()
    {
        var settings = SettingsProvider.Instance.Settings;
        try { WordTimeText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.FontColor)); } catch { }
    }
}
