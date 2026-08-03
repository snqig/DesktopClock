using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopClock.Core;

namespace DesktopClock.FloatWindows;

/// <summary>
/// 日历悬浮窗口(P0)：显示日期 + 农历。
/// </summary>
public partial class CalendarWindow : BaseFloatWindow
{
    private readonly DispatcherTimer _timer;

    public CalendarWindow()
    {
        ComponentId = "calendar";
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += (_, _) => UpdateDate();
        _timer.Start();
        UpdateDate();
    }

    private void UpdateDate()
    {
        var now = DateTime.Now;
        var cfg = ComponentManager.Instance.GetConfig(ComponentId);
        var lang = cfg?.GetString("language", "zh") ?? "zh";
        var culture = lang == "en"
            ? System.Globalization.CultureInfo.GetCultureInfo("en-US")
            : System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
        DateText.Text = now.ToString("yyyy-MM-dd dddd", culture);

        // 农历
        var showLunar = cfg?.GetBool("showLunar", false) ?? false;
        if (showLunar)
        {
            try
            {
                LunarText.Text = LunarCalendar.GetLunarInfo(now).FullString;
                LunarText.Visibility = Visibility.Visible;
            }
            catch
            {
                LunarText.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            LunarText.Visibility = Visibility.Collapsed;
        }
    }

    public override void LoadFromConfig()
    {
        var cfg = ComponentManager.Instance.EnsureConfig(ComponentId);

        if (!double.IsNaN(cfg.Left)) Left = cfg.Left;
        if (!double.IsNaN(cfg.Top)) Top = cfg.Top;
        Width = cfg.Width > 0 ? cfg.Width : 280;
        Height = cfg.Height > 0 ? cfg.Height : 80;
        ClampToScreen();

        IsTopmost = cfg.Topmost;
        IsLocked = cfg.LockPosition;
        WindowOpacity = cfg.Opacity;

        try { DateText.FontFamily = new FontFamily(cfg.FontFamily); } catch { }
        DateText.FontSize = cfg.FontSize;
        try { DateText.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(cfg.FontColor)); } catch { }
        try { LunarText.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(cfg.FontColor)); } catch { }
    }

    public override void SavePosition()
    {
        var cfg = ComponentManager.Instance.EnsureConfig(ComponentId);
        cfg.Left = Left;
        cfg.Top = Top;
        cfg.Width = ActualWidth > 0 ? ActualWidth : Width;
        cfg.Height = ActualHeight > 0 ? ActualHeight : Height;
        cfg.Topmost = IsTopmost;
        cfg.LockPosition = IsLocked;
        cfg.Opacity = WindowOpacity;
        ComponentManager.Instance.SaveConfig();
    }

    public override void ApplyConfigChange()
    {
        LoadFromConfig();
        UpdateDate();
    }
}
