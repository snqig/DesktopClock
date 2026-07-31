using System;
using System.Windows;
using System.Windows.Input;
using DesktopClock.Config;
using DesktopClock.Models;
using DesktopClock.Render;

namespace DesktopClock.Views.Widgets;

/// <summary>
/// 倒计时挂件独立悬浮窗口。
/// 支持两种创建模式:
/// 1. WidgetManager 工厂模式:无参构造后调用 ApplyConfig + 外部订阅 OnFrame。
/// 2. 传统模式:传入 CountdownWidgetConfig + FrameRenderScheduler。
/// </summary>
public partial class CountdownWindow : Window
{
    private FrameRenderScheduler? _scheduler;
    private CountdownWidgetConfig? _config;

    /// <summary>无参构造(WidgetManager 工厂模式使用)。</summary>
    public CountdownWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left && !AppSettings.Load().LockPosition)
                DragMove();
        };
    }

    public CountdownWindow(CountdownWidgetConfig config, FrameRenderScheduler scheduler) : this()
    {
        _config = config ?? new CountdownWidgetConfig();
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        ApplyConfig(_config);
        _scheduler.Subscribe(OnFrame);
    }

    /// <summary>
    /// 从 AppSettings 完整注入倒计时样式 + 窗口参数。
    /// MainWindow 工厂模式下调用。
    /// </summary>
    public void ApplyConfig(AppSettings settings)
    {
        var cfg = new CountdownWidgetConfig();
        cfg.Timer.TargetTime = settings.CountdownTarget ?? DateTime.Now.AddDays(1).ToUniversalTime();
        cfg.Timer.Title = settings.CountdownLabel;
        cfg.Timer.ShowTitle = settings.CountdownShowTitle;
        cfg.Timer.DisplayMode = settings.CountdownDisplayMode;
        cfg.Timer.EndAction = settings.CountdownEndAction;
        cfg.Timer.StopAtZero = settings.CountdownStopAtZero;

        cfg.Style.FontFamily = settings.CountdownFontFamily;
        cfg.Style.FontSize = settings.CountdownFontSize;
        cfg.Style.FontColor = string.IsNullOrEmpty(settings.CountdownFontColor) ? "#FFFFFFFF" : settings.CountdownFontColor;
        cfg.Style.StrokeEnabled = settings.CountdownStrokeEnabled;
        cfg.Style.StrokeThickness = settings.CountdownStrokeThickness;
        cfg.Style.StrokeColor = string.IsNullOrEmpty(settings.CountdownStrokeColor) ? "#FF000000" : settings.CountdownStrokeColor;
        cfg.Style.ShadowEnabled = settings.CountdownShadowEnabled;
        cfg.Style.ShadowSize = settings.CountdownShadowSize;
        cfg.Style.ShadowColor = string.IsNullOrEmpty(settings.CountdownShadowColor) ? "#FF000000" : settings.CountdownShadowColor;

        // CountdownOpacity 是用户在设置面板中调整的透明度值,优先于位置保存的 CountdownWindowOpacity
        cfg.Window.Opacity = settings.CountdownOpacity > 0 ? settings.CountdownOpacity : settings.CountdownWindowOpacity;
        cfg.Window.Topmost = settings.CountdownTopmost;

        ApplyConfig(cfg);
    }

    /// <summary>从 CountdownWidgetConfig 应用配置。</summary>
    public void ApplyConfig(CountdownWidgetConfig cfg)
    {
        _config = cfg;
        var win = cfg.Window;
        if (!double.IsNaN(win.Left)) Left = win.Left;
        if (!double.IsNaN(win.Top)) Top = win.Top;
        if (win.Width > 0) Width = win.Width;
        if (win.Height > 0) Height = win.Height;
        if (win.Opacity > 0) Opacity = win.Opacity;
        Topmost = win.Topmost;

        Widget.Initialize(cfg.Timer, cfg.Style);
        Widget.TitleText.Visibility = cfg.Timer.ShowTitle ? Visibility.Visible : Visibility.Collapsed;
        if (cfg.Timer.ShowTitle && !string.IsNullOrEmpty(cfg.Timer.Title))
            Widget.TitleText.Text = cfg.Timer.Title;
    }

    /// <summary>
    /// 多任务模式应用配置:从 AppSettings 读取 CountdownTasks 列表,
    /// 若列表非空则启用轮播,否则回退到单任务。
    /// </summary>
    public void ApplyConfigMulti(AppSettings settings)
    {
        // 先调用基础 ApplyConfig 完成样式与窗口参数
        ApplyConfig(settings);

        // 启用多任务轮播(列表非空时)
        if (settings.CountdownTasks != null && settings.CountdownTasks.Count > 0)
        {
            var cfg = new CountdownWidgetConfig();
            cfg.Timer.TargetTime = settings.CountdownTarget ?? DateTime.Now.AddDays(1).ToUniversalTime();
            cfg.Timer.Title = settings.CountdownLabel;
            cfg.Timer.ShowTitle = settings.CountdownShowTitle;
            cfg.Timer.DisplayMode = settings.CountdownDisplayMode;
            cfg.Timer.EndAction = settings.CountdownEndAction;
            cfg.Timer.StopAtZero = settings.CountdownStopAtZero;

            cfg.Style.FontFamily = settings.CountdownFontFamily;
            cfg.Style.FontSize = settings.CountdownFontSize;
            cfg.Style.FontColor = string.IsNullOrEmpty(settings.CountdownFontColor) ? "#FFFFFFFF" : settings.CountdownFontColor;
            cfg.Style.StrokeEnabled = settings.CountdownStrokeEnabled;
            cfg.Style.StrokeThickness = settings.CountdownStrokeThickness;
            cfg.Style.StrokeColor = string.IsNullOrEmpty(settings.CountdownStrokeColor) ? "#FF000000" : settings.CountdownStrokeColor;
            cfg.Style.ShadowEnabled = settings.CountdownShadowEnabled;
            cfg.Style.ShadowSize = settings.CountdownShadowSize;
            cfg.Style.ShadowColor = string.IsNullOrEmpty(settings.CountdownShadowColor) ? "#FF000000" : settings.CountdownShadowColor;

            Widget.InitializeMulti(cfg.Timer, cfg.Style, settings.CountdownTasks, settings.CountdownTaskRotationSeconds);
        }
    }

    /// <summary>
    /// 每帧刷新(由 FrameRenderScheduler 调用)。
    /// 外部订阅后会统一分发,MainWindow 订阅时需使用此公共方法。
    /// </summary>
    public void OnFrame(DateTime nowUtc)
    {
        if (!IsVisible) return;
        Dispatcher.BeginInvoke(() => Widget.OnFrame(nowUtc));
    }

    protected override void OnClosed(EventArgs e)
    {
        _scheduler?.Unsubscribe(OnFrame);
        base.OnClosed(e);
    }
}
