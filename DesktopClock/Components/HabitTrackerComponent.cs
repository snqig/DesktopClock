using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopClock.Models;

namespace DesktopClock.Components;

/// <summary>
/// 习惯打卡组件(P4):每日习惯追踪,可视化打卡 + 7 天热力图。
/// 主窗口内嵌,点击习惯名即可打卡/取消,色块展示最近 7 天完成情况。
/// 配置键(通过 Config.Settings 注入):
///   "habitsJson" - JSON 字符串,HabitItem 列表
///   "recordsJson" - JSON 字符串,打卡记录 Dictionary&lt;string, List&lt;string&gt;&gt;
///                   key=habitId, value=日期字符串列表(yyyy-MM-dd)
///   "fontSize"   - 字号
///   "fontColor"  - 颜色
///   "fontFamily" - 字体
/// </summary>
public class HabitTrackerComponent : IClockComponent
{
    public string Id => "habit_tracker";
    public string DisplayName => "习惯打卡";
    public FrameworkElement View => _container;
    public ComponentConfig Config { get; set; } = new();

    private readonly StackPanel _container;
    private readonly TextBlock _titleText;
    private readonly StackPanel _habitsPanel;

    private List<HabitItem> _habits = new();
    private Dictionary<string, List<string>> _records = new();
    private double _fontSize = 12;
    private string _fontColor = "#FFD1D1D6";
    private string _fontFamily = "Microsoft YaHei UI";
    // 配置回写回调:打卡后通知 MainWindow 持久化
    public Action? OnRecordsChanged { get; set; }

    public HabitTrackerComponent()
    {
        _titleText = new TextBlock
        {
            Text = "习惯打卡",
            FontSize = 11,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };

        _habitsPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _container.Children.Add(_titleText);
        _container.Children.Add(_habitsPanel);
    }

    public void Update(DateTime nowLocal)
    {
        // 习惯打卡是交互式组件,不需要每秒更新
    }

    public void ApplyConfig()
    {
        if (Config.Settings.TryGetValue("habitsJson", out var hj))
        {
            var json = hj is string s ? s : hj.ToString() ?? "[]";
            try { _habits = JsonSerializer.Deserialize<List<HabitItem>>(json) ?? new(); }
            catch { _habits = new(); }
        }

        if (Config.Settings.TryGetValue("recordsJson", out var rj))
        {
            var json = rj is string s ? s : rj.ToString() ?? "{}";
            try { _records = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json) ?? new(); }
            catch { _records = new(); }
        }

        if (Config.Settings.TryGetValue("fontSize", out var fsz))
        {
            double size = 0;
            if (fsz is double d) size = d;
            else if (fsz is string ss && double.TryParse(ss, out var r)) size = r;
            if (size > 0) _fontSize = size;
        }

        if (Config.Settings.TryGetValue("fontColor", out var fc))
            _fontColor = fc.ToString()!;

        if (Config.Settings.TryGetValue("fontFamily", out var ff) && ff is string ffs && !string.IsNullOrEmpty(ffs))
            _fontFamily = ffs;

        RenderHabits();
    }

    private void RenderHabits()
    {
        _habitsPanel.Children.Clear();

        if (_habits.Count == 0)
        {
            _habitsPanel.Children.Add(new TextBlock
            {
                Text = "请在设置中添加习惯",
                FontSize = _fontSize,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            return;
        }

        var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
        int completedToday = 0;

        foreach (var habit in _habits)
        {
            if (!habit.Enabled) continue;

            var recordList = _records.GetValueOrDefault(habit.Id) ?? new();
            bool doneToday = recordList.Contains(todayStr);
            if (doneToday) completedToday++;

            // 每行:习惯名 + 7 天色块 + 打卡按钮
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };

            // 习惯名(可点击打卡)
            var nameBlock = new TextBlock
            {
                Text = (doneToday ? "✅ " : "⬜ ") + habit.Name,
                FontSize = _fontSize,
                Foreground = doneToday ? Brushes.LimeGreen : new SolidColorBrush((Color)ColorConverter.ConvertFromString(_fontColor)),
                FontFamily = new FontFamily(_fontFamily),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var capturedHabit = habit;
            nameBlock.MouseLeftButtonDown += (_, _) => ToggleHabit(capturedHabit);
            row.Children.Add(nameBlock);

            // 7 天热力图色块
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                var dateStr = date.ToString("yyyy-MM-dd");
                bool done = recordList.Contains(dateStr);
                var block = new Border
                {
                    Width = 8,
                    Height = 8,
                    Background = done ? Brushes.LimeGreen : Brushes.DarkGray,
                    Margin = new Thickness(1, 0, 1, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = $"{date:MM-dd} {(done ? "已完成" : "未完成")}"
                };
                row.Children.Add(block);
            }

            _habitsPanel.Children.Add(row);
        }

        // 今日进度
        var activeCount = _habits.Count(h => h.Enabled);
        if (activeCount > 0)
        {
            var progress = (double)completedToday / activeCount;
            var barLen = (int)Math.Round(progress * 10);
            var bar = new string('█', barLen) + new string('░', 10 - barLen);
            _titleText.Text = $"习惯打卡 今日 {completedToday}/{activeCount} {bar} {progress * 100:F0}%";
        }
    }

    private void ToggleHabit(HabitItem habit)
    {
        var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
        if (!_records.ContainsKey(habit.Id))
            _records[habit.Id] = new();

        var list = _records[habit.Id];
        if (list.Contains(todayStr))
            list.Remove(todayStr);
        else
            list.Add(todayStr);

        // 回写配置(供 MainWindow 持久化)
        Config.Settings["recordsJson"] = JsonSerializer.Serialize(_records);
        OnRecordsChanged?.Invoke();
        RenderHabits();
    }

    /// <summary>获取当前打卡记录 JSON(供外部持久化)。</summary>
    public string GetRecordsJson() => JsonSerializer.Serialize(_records);
}

/// <summary>习惯条目</summary>
public class HabitItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "习惯";
    public bool Enabled { get; set; } = true;
}
