using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopClock.Core;
using DesktopClock.Models;

namespace DesktopClock.FloatWindows;

/// <summary>
/// 多事件倒计时悬浮窗口(P1)。
/// 支持多任务列表 + 自动轮播切换，每条任务独立启用/显示模式。
/// </summary>
public partial class CountdownWindow : BaseFloatWindow
{
    private readonly DispatcherTimer _timer;
    private List<CountdownTask> _tasks = new();
    private int _rotationSeconds = 10;
    private DateTime _lastRotation = DateTime.Now;
    private int _currentIndex = -1;

    public CountdownWindow()
    {
        ComponentId = "countdown";
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateCountdown();
        _timer.Start();
    }

    private void UpdateCountdown()
    {
        var cfg = ComponentManager.Instance.GetConfig(ComponentId);

        // 加载任务列表
        var tasksJson = cfg?.GetString("tasks", "") ?? "";
        if (!string.IsNullOrEmpty(tasksJson))
        {
            try
            {
                _tasks = JsonSerializer.Deserialize<List<CountdownTask>>(tasksJson, AppSettings.JsonOpts) ?? new();
            }
            catch { }
        }

        var rsi = cfg?.GetInt("rotationSeconds", 10) ?? 10;
        _rotationSeconds = rsi > 0 ? rsi : 10;

        var enabledTasks = _tasks.Where(t => t.Enabled).ToList();
        if (enabledTasks.Count == 0)
        {
            TitleText.Text = "";
            CountdownText.Text = "无倒计时任务";
            return;
        }

        // 轮播切换
        if (DateTime.Now - _lastRotation >= TimeSpan.FromSeconds(_rotationSeconds) || _currentIndex < 0)
        {
            _currentIndex = (_currentIndex + 1) % enabledTasks.Count;
            _lastRotation = DateTime.Now;
        }

        var task = enabledTasks[_currentIndex];
        var targetLocal = task.TargetTimeUtc.ToLocalTime();
        var remaining = targetLocal - DateTime.Now;

        TitleText.Text = task.Title;

        if (remaining.TotalSeconds <= 0)
        {
            CountdownText.Text = task.DisplayMode == "days" ? "0 天 00:00:00" : "00:00:00";
        }
        else
        {
            var days = (int)remaining.TotalDays;
            var hh = remaining.Hours;
            var mm = remaining.Minutes;
            var ss = remaining.Seconds;
            CountdownText.Text = task.DisplayMode == "days"
                ? $"{days} 天 {hh:00}:{mm:00}:{ss:00}"
                : $"{hh + days * 24:00}:{mm:00}:{ss:00}";
        }
    }

    public override void LoadFromConfig()
    {
        var cfg = ComponentManager.Instance.EnsureConfig(ComponentId);

        if (!double.IsNaN(cfg.Left)) Left = cfg.Left;
        if (!double.IsNaN(cfg.Top)) Top = cfg.Top;
        Width = cfg.Width > 0 ? cfg.Width : 260;
        Height = cfg.Height > 0 ? cfg.Height : 100;
        ClampToScreen();

        IsTopmost = cfg.Topmost;
        IsLocked = cfg.LockPosition;
        WindowOpacity = cfg.Opacity;

        try { CountdownText.FontFamily = new FontFamily(cfg.FontFamily); } catch { }
        CountdownText.FontSize = cfg.FontSize;
        try { CountdownText.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(cfg.FontColor)); } catch { }
        try { TitleText.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(cfg.FontColor)); } catch { }

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
        UpdateCountdown();
    }
}
