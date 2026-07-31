using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopClock.Models;
using DesktopClock.Services;

namespace DesktopClock.Components;

/// <summary>
/// 间隔提醒组件(P1):周期性健康提醒(喝水/站立/眼保健操...)。
/// 主窗口内嵌,显示距最近下一条提醒的倒计时,到点触发桌面通知。
/// 配置键(通过 Config.Settings 注入):
///   "items"        - JSON 字符串,IntervalReminderItem 列表
///   "workStartHour"- 工作时段起始小时(int,0-23,默认 0=不限制)
///   "workEndHour"  - 工作时段结束小时(int,0-23,默认 24=不限制)
///   "fontSize"     - 字号(double)
///   "fontColor"    - 颜色(string)
///   "fontFamily"   - 字体(string)
/// </summary>
public class HealthReminderComponent : IClockComponent
{
    public string Id => "health_reminder";
    public string DisplayName => "健康提醒";
    public FrameworkElement View => _container;
    public ComponentConfig Config { get; set; } = new();

    private readonly StackPanel _container;
    private readonly TextBlock _titleText;
    private readonly TextBlock _countdownText;

    private List<IntervalReminderItem> _items = new();
    private int _workStartHour;
    private int _workEndHour = 24;
    private double _fontSize = 13;
    private string _fontColor = "#FFD1D1D6";
    private string _fontFamily = "Microsoft YaHei UI";

    // 已触发记录:今日已触发次数(按 id 累计,0:00 重置)
    private readonly Dictionary<string, int> _firedCount = new();
    private DateTime _firedDate = DateTime.Today;
    // 下次触发时间点(本地时间)
    private readonly Dictionary<string, DateTime> _nextFire = new();

    public HealthReminderComponent()
    {
        _titleText = new TextBlock
        {
            Text = "健康提醒",
            FontSize = 11,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        };

        _countdownText = new TextBlock
        {
            Text = "",
            FontSize = _fontSize,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_fontColor)),
            FontFamily = new FontFamily(_fontFamily),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _container.Children.Add(_titleText);
        _container.Children.Add(_countdownText);
    }

    public void Update(DateTime nowLocal)
    {
        // 0:00 重置今日计数
        if (nowLocal.Date != _firedDate)
        {
            _firedCount.Clear();
            _firedDate = nowLocal.Date;
            _nextFire.Clear();
        }

        if (_items.Count == 0)
        {
            _countdownText.Text = "";
            return;
        }

        // 初始化下次触发时间
        foreach (var item in _items)
        {
            if (!item.Enabled) continue;
            if (!_nextFire.ContainsKey(item.Id))
                _nextFire[item.Id] = nowLocal.AddMinutes(item.IntervalMinutes);
        }

        // 检查到点触发
        bool anyFired = false;
        foreach (var item in _items)
        {
            if (!item.Enabled) continue;
            if (!_nextFire.TryGetValue(item.Id, out var fireTime)) continue;

            if (nowLocal >= fireTime)
            {
                // 工作时段限制
                bool inWorkHours = _workStartHour == 0 && _workEndHour == 24
                    || (nowLocal.Hour >= _workStartHour && nowLocal.Hour < _workEndHour);
                if (inWorkHours)
                {
                    NotificationService.Notify("健康提醒", item.Label);
                    _firedCount[item.Id] = _firedCount.GetValueOrDefault(item.Id) + 1;
                    anyFired = true;
                }
                _nextFire[item.Id] = nowLocal.AddMinutes(item.IntervalMinutes);
            }
        }

        // 找最近一条待触发
        DateTime? nearest = null;
        string? nearestLabel = null;
        foreach (var item in _items)
        {
            if (!item.Enabled) continue;
            if (!_nextFire.TryGetValue(item.Id, out var t)) continue;
            if (nearest == null || t < nearest)
            {
                nearest = t;
                nearestLabel = item.Label;
            }
        }

        if (nearest != null && nearestLabel != null)
        {
            var remaining = nearest.Value - nowLocal;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            var count = _firedCount.GetValueOrDefault(nearestLabel!);
            _countdownText.Text = $"{nearestLabel} {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}" +
                                  (count > 0 ? $"  (今日 {count})" : "");
        }
    }

    public void ApplyConfig()
    {
        if (Config.Settings.TryGetValue("items", out var it))
        {
            var json = it is string s ? s : it.ToString() ?? "[]";
            try { _items = JsonSerializer.Deserialize<List<IntervalReminderItem>>(json) ?? new(); }
            catch { _items = new(); }
            _nextFire.Clear();
        }

        if (Config.Settings.TryGetValue("workStartHour", out var ws))
        {
            if (ws is int wsi) _workStartHour = wsi;
            else if (ws is string wss && int.TryParse(wss, out var r)) _workStartHour = r;
        }
        if (Config.Settings.TryGetValue("workEndHour", out var we))
        {
            if (we is int wei) _workEndHour = wei;
            else if (we is string wes && int.TryParse(wes, out var r)) _workEndHour = r;
        }

        if (Config.Settings.TryGetValue("fontSize", out var fsz))
        {
            double size = 0;
            if (fsz is double d) size = d;
            else if (fsz is string ss && double.TryParse(ss, out var r)) size = r;
            if (size > 0) { _fontSize = size; _countdownText.FontSize = size; }
        }

        if (Config.Settings.TryGetValue("fontColor", out var fc))
        {
            _fontColor = fc.ToString()!;
            try { _countdownText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_fontColor)); }
            catch { }
        }

        if (Config.Settings.TryGetValue("fontFamily", out var ff) && ff is string ffs && !string.IsNullOrEmpty(ffs))
        {
            _fontFamily = ffs;
            try { _countdownText.FontFamily = new FontFamily(ffs); }
            catch { }
        }
    }
}

/// <summary>间隔提醒条目</summary>
public class IntervalReminderItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = "提醒";
    public int IntervalMinutes { get; set; } = 60;
    public bool Enabled { get; set; } = true;
}
