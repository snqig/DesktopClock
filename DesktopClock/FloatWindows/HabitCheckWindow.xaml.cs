using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DesktopClock.Components;
using DesktopClock.Core;

namespace DesktopClock.FloatWindows;

/// <summary>
/// 习惯打卡悬浮窗口：HabitTrackerComponent 的独立窗口版本。
/// 每日习惯追踪，可点击打卡 + 7 天热力图 + 今日进度。
/// 配置键(通过 ComponentManager 配置的 Settings 注入)：
///   "habitsJson"  - JSON 字符串，HabitItem 列表
///   "recordsJson" - JSON 字符串，打卡记录 Dictionary&lt;string, List&lt;string&gt;&gt;
///                   key=habitId, value=日期字符串列表(yyyy-MM-dd)
/// </summary>
public partial class HabitCheckWindow : BaseFloatWindow
{
    private List<HabitItem> _habits = new();
    private Dictionary<string, List<string>> _records = new();
    private double _fontSize = 12;
    private string _fontColor = "#FFD1D1D6";
    private string _fontFamily = "Microsoft YaHei UI";
    private readonly TextBlock _progressText;

    public HabitCheckWindow()
    {
        ComponentId = "habit_check";
        InitializeComponent();

        // 今日进度(底部)
        _progressText = new TextBlock
        {
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 11,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            Text = "今日 0/0"
        };
        MainPanel.Children.Add(_progressText);

        LoadFromConfig();
    }

    public override void LoadFromConfig()
    {
        var cfg = ComponentManager.Instance.EnsureConfig(ComponentId);

        // 位置与尺寸
        if (!double.IsNaN(cfg.Left)) Left = cfg.Left;
        if (!double.IsNaN(cfg.Top)) Top = cfg.Top;
        Width = cfg.Width > 0 ? cfg.Width : 220;
        Height = cfg.Height > 0 ? cfg.Height : 200;
        ClampToScreen();

        // 状态
        IsTopmost = cfg.Topmost;
        IsLocked = cfg.LockPosition;
        WindowOpacity = cfg.Opacity;

        // 字体与颜色
        if (!string.IsNullOrEmpty(cfg.FontFamily)) _fontFamily = cfg.FontFamily;
        _fontSize = cfg.FontSize > 0 ? cfg.FontSize : 12;
        if (!string.IsNullOrEmpty(cfg.FontColor)) _fontColor = cfg.FontColor;

        try { TitleText.FontFamily = new FontFamily(_fontFamily); } catch { }
        TitleText.FontSize = _fontSize;
        try { TitleText.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(_fontColor)); } catch { }

        try { _progressText.FontFamily = new FontFamily(_fontFamily); } catch { }
        _progressText.FontSize = _fontSize;
        try { _progressText.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(_fontColor)); } catch { }

        // 读取习惯列表
        var habitsJson = cfg.GetString("habitsJson", "");
        if (!string.IsNullOrEmpty(habitsJson))
        {
            try { _habits = JsonSerializer.Deserialize<List<HabitItem>>(habitsJson) ?? new(); }
            catch { _habits = new(); }
        }

        // 读取打卡记录
        var recordsJson = cfg.GetString("recordsJson", "");
        if (!string.IsNullOrEmpty(recordsJson))
        {
            try { _records = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(recordsJson) ?? new(); }
            catch { _records = new(); }
        }

        RenderHabits();
    }

    private void RenderHabits()
    {
        HabitsPanel.Children.Clear();

        if (_habits.Count == 0)
        {
            HabitsPanel.Children.Add(new TextBlock
            {
                Text = "请在设置中添加习惯",
                FontSize = _fontSize,
                FontFamily = new FontFamily(_fontFamily),
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            _progressText.Text = "今日 0/0";
            return;
        }

        var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
        int completedToday = 0;

        foreach (var habit in _habits)
        {
            if (!habit.Enabled) continue;

            var recordList = _records.GetValueOrDefault(habit.Id) ?? new List<string>();
            bool doneToday = recordList.Contains(todayStr);
            if (doneToday) completedToday++;

            // 每行：习惯名(可点击) + 7 天热力图色块
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };

            var nameBlock = new TextBlock
            {
                Text = (doneToday ? "✅ " : "⬜ ") + habit.Name,
                FontSize = _fontSize,
                FontFamily = new FontFamily(_fontFamily),
                Foreground = doneToday ? Brushes.LimeGreen : new SolidColorBrush((Color)ColorConverter.ConvertFromString(_fontColor)),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var capturedHabit = habit;
            nameBlock.MouseLeftButtonDown += (_, _) => ToggleHabit(capturedHabit);
            row.Children.Add(nameBlock);

            // 7 天热力图：绿色=已打卡，灰色=未打卡
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                var dateStr = date.ToString("yyyy-MM-dd");
                bool done = recordList.Contains(dateStr);
                var rect = new Rectangle
                {
                    Width = 8,
                    Height = 8,
                    Fill = done ? Brushes.LimeGreen : Brushes.DarkGray,
                    Margin = new Thickness(1, 0, 1, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = $"{date:MM-dd} {(done ? "已完成" : "未完成")}"
                };
                row.Children.Add(rect);
            }

            HabitsPanel.Children.Add(row);
        }

        // 今日进度
        var activeCount = _habits.Count(h => h.Enabled);
        _progressText.Text = $"今日 {completedToday}/{activeCount}";
    }

    private void ToggleHabit(HabitItem habit)
    {
        var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
        if (!_records.ContainsKey(habit.Id))
            _records[habit.Id] = new List<string>();

        var list = _records[habit.Id];
        if (list.Contains(todayStr))
            list.Remove(todayStr);
        else
            list.Add(todayStr);

        // 立即保存回配置
        var cfg = ComponentManager.Instance.EnsureConfig(ComponentId);
        cfg.Settings["recordsJson"] = JsonSerializer.Serialize(_records);
        ComponentManager.Instance.SaveConfig();

        RenderHabits();
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
    }
}
