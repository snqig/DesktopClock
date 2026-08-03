using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopClock.Core;

namespace DesktopClock.FloatWindows;

/// <summary>
/// 数字时钟悬浮窗口(P0)。
/// 独立配置：字体、字号、颜色、描边、阴影、位置、透明度。
/// </summary>
public partial class ClockWindow : BaseFloatWindow
{
    private readonly DispatcherTimer _timer;

    public ClockWindow()
    {
        ComponentId = "clock";
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateTime();
        _timer.Start();
        UpdateTime();
    }

    private void UpdateTime()
    {
        var cfg = ComponentManager.Instance.GetConfig(ComponentId);
        var use24 = cfg?.GetBool("use24hour", true) ?? true;
        var showSec = cfg?.GetBool("showSeconds", true) ?? true;
        var format = (use24 ? "HH" : "hh") + ":mm" + (showSec ? ":ss" : "");
        TimeText.Text = DateTime.Now.ToString(format);
    }

    public override void LoadFromConfig()
    {
        var cfg = ComponentManager.Instance.EnsureConfig(ComponentId);

        // 位置
        if (!double.IsNaN(cfg.Left)) Left = cfg.Left;
        if (!double.IsNaN(cfg.Top)) Top = cfg.Top;
        Width = cfg.Width > 0 ? cfg.Width : 320;
        Height = cfg.Height > 0 ? cfg.Height : 120;
        ClampToScreen();

        // 样式
        IsTopmost = cfg.Topmost;
        IsLocked = cfg.LockPosition;
        WindowOpacity = cfg.Opacity;

        try { TimeText.FontFamily = new FontFamily(cfg.FontFamily); } catch { }
        TimeText.FontSize = cfg.FontSize;
        try { TimeText.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(cfg.FontColor)); } catch { }

        // 描边
        if (cfg.StrokeEnabled)
        {
            // WPF TextBlock 无直接描边，使用 OutlineText 效果近似
            // 简化方案：用阴影模拟描边
        }

        // 阴影
        if (cfg.ShadowEnabled)
        {
            TextShadow.BlurRadius = cfg.ShadowSize;
            try { TextShadow.Color = (Color)ColorConverter.ConvertFromString(cfg.ShadowColor); } catch { }
            TextShadow.Opacity = 0.8;
        }
        else
        {
            TextShadow.Opacity = 0;
        }
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
        UpdateTime();
    }
}
