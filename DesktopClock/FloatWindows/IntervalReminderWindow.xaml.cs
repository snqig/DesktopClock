using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopClock.Components;
using DesktopClock.Core;
using DesktopClock.Services;

namespace DesktopClock.FloatWindows;

/// <summary>
/// 间隔提醒悬浮窗口(P1)：周期性健康提醒(喝水/站立/眼操)。
/// 支持多条目管理、倒计时显示、桌面通知、工作时段限制。
/// </summary>
public partial class IntervalReminderWindow : BaseFloatWindow
{
    private readonly DispatcherTimer _timer;
    private List<IntervalReminderItem> _items = new();
    private int _workStartHour;
    private int _workEndHour = 24;
    private readonly Dictionary<string, DateTime> _nextFire = new();
    private DateTime _firedDate = DateTime.Today;

    public IntervalReminderWindow()
    {
        ComponentId = "interval_reminder";
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateReminder();
        _timer.Start();
    }

    private void UpdateReminder()
    {
        var cfg = ComponentManager.Instance.GetConfig(ComponentId);

        // 加载提醒条目
        var itemsJson = cfg?.GetString("items", "") ?? "";
        if (!string.IsNullOrEmpty(itemsJson))
        {
            try
            {
                _items = JsonSerializer.Deserialize<List<IntervalReminderItem>>(itemsJson, AppSettings.JsonOpts) ?? new();
            }
            catch { }
        }

        _workStartHour = cfg?.GetInt("workStartHour", 0) ?? 0;
        _workEndHour = cfg?.GetInt("workEndHour", 24) ?? 24;

        // 0:00 重置
        if (DateTime.Today != _firedDate)
        {
            _nextFire.Clear();
            _firedDate = DateTime.Today;
        }

        var now = DateTime.Now;
        var enabledItems = _items.FindAll(i => i.Enabled);
        if (enabledItems.Count == 0)
        {
            TitleText.Text = "健康提醒";
            CountdownText.Text = "无提醒";
            return;
        }

        // 初始化下次触发
        foreach (var item in enabledItems)
            if (!_nextFire.ContainsKey(item.Id))
                _nextFire[item.Id] = now.AddMinutes(item.IntervalMinutes);

        // 检查到点触发
        TimeSpan nearest = TimeSpan.MaxValue;
        string nearestLabel = "";

        foreach (var item in enabledItems)
        {
            if (_nextFire.TryGetValue(item.Id, out var fireTime))
            {
                if (now >= fireTime)
                {
                    bool inWorkHours = _workStartHour == 0 && _workEndHour == 24
                        || (now.Hour >= _workStartHour && now.Hour < _workEndHour);
                    if (inWorkHours)
                        NotificationService.Notify("健康提醒", item.Label);
                    _nextFire[item.Id] = now.AddMinutes(item.IntervalMinutes);
                    fireTime = _nextFire[item.Id];
                }

                var remaining = fireTime - now;
                if (remaining < nearest)
                {
                    nearest = remaining;
                    nearestLabel = item.Label;
                }
            }
        }

        TitleText.Text = $"下次: {nearestLabel}";
        if (nearest.TotalSeconds <= 0)
            CountdownText.Text = "00:00";
        else
            CountdownText.Text = $"{(int)nearest.TotalMinutes:00}:{nearest.Seconds:00}";
    }

    public override void LoadFromConfig()
    {
        var cfg = ComponentManager.Instance.EnsureConfig(ComponentId);

        if (!double.IsNaN(cfg.Left)) Left = cfg.Left;
        if (!double.IsNaN(cfg.Top)) Top = cfg.Top;
        Width = cfg.Width > 0 ? cfg.Width : 200;
        Height = cfg.Height > 0 ? cfg.Height : 80;
        ClampToScreen();

        IsTopmost = cfg.Topmost;
        DesktopWidgetMode = cfg.DesktopWidgetMode;
        IsLocked = cfg.LockPosition;
        WindowOpacity = cfg.Opacity;

        try { CountdownText.FontFamily = new FontFamily(cfg.FontFamily); } catch { }
        CountdownText.FontSize = cfg.FontSize;
        try { CountdownText.Foreground = new SolidColorBrush(
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
        cfg.DesktopWidgetMode = DesktopWidgetMode;
        cfg.LockPosition = IsLocked;
        cfg.Opacity = WindowOpacity;
        ComponentManager.Instance.SaveConfig();
    }

    public override void ApplyConfigChange()
    {
        LoadFromConfig();
        UpdateReminder();
    }
}
