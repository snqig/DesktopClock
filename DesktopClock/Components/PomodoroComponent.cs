using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopClock.Models;
using DesktopClock.Services;

namespace DesktopClock.Components;

/// <summary>
/// 番茄钟组件(P2):25 分钟专注 + 5 分钟休息循环,每 4 个番茄触发长休息。
/// 主窗口内嵌,显示当前阶段 + 剩余时间。通过右键菜单控制开始/暂停/重置/跳过。
/// 配置键(通过 Config.Settings 注入):
///   "focusMinutes"     - 专注时长(默认 25)
///   "shortBreakMinutes" - 短休息(默认 5)
///   "longBreakMinutes"  - 长休息(默认 15)
///   "longBreakInterval" - 长休息间隔番茄数(默认 4)
///   "autoStart"        - 阶段切换后自动开始(默认 false)
///   "fontSize"         - 字号
///   "fontColor"        - 颜色
///   "fontFamily"       - 字体
/// 控制方法(由右键菜单/热键调用):
///   Start() / Pause() / Reset() / Skip()
/// </summary>
public class PomodoroComponent : IClockComponent
{
    public string Id => "pomodoro";
    public string DisplayName => "番茄钟";
    public FrameworkElement View => _container;
    public ComponentConfig Config { get; set; } = new();

    private readonly StackPanel _container;
    private readonly TextBlock _phaseText;
    private readonly TextBlock _timeText;

    public enum Phase { Idle, Focus, ShortBreak, LongBreak }

    private Phase _currentPhase = Phase.Idle;
    private DateTime _phaseEndTime = DateTime.MinValue;
    private DateTime _lastTick = DateTime.Now;
    private int _completedPomodoros; // 今日完成番茄数
    private DateTime _completedDate = DateTime.Today;

    // 配置
    private int _focusMinutes = 25;
    private int _shortBreakMinutes = 5;
    private int _longBreakMinutes = 15;
    private int _longBreakInterval = 4;
    private bool _autoStart;
    private double _fontSize = 16;
    private string _fontColor = "#FF66BB6A";
    private string _fontFamily = "Microsoft YaHei UI";

    public PomodoroComponent()
    {
        _phaseText = new TextBlock
        {
            Text = "🍅 番茄钟",
            FontSize = 11,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        };

        _timeText = new TextBlock
        {
            Text = "25:00",
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
        _container.Children.Add(_phaseText);
        _container.Children.Add(_timeText);

        // 右键菜单:开始/暂停/重置/跳过
        var menu = new ContextMenu();
        var miStart = new MenuItem { Header = "开始" };
        miStart.Click += (_, _) => Start();
        var miPause = new MenuItem { Header = "暂停" };
        miPause.Click += (_, _) => Pause();
        var miReset = new MenuItem { Header = "重置" };
        miReset.Click += (_, _) => Reset();
        var miSkip = new MenuItem { Header = "跳过当前阶段" };
        miSkip.Click += (_, _) => Skip();
        menu.Items.Add(miStart);
        menu.Items.Add(miPause);
        menu.Items.Add(miReset);
        menu.Items.Add(miSkip);
        _container.ContextMenu = menu;
    }

    public void Start()
    {
        if (_currentPhase == Phase.Idle)
        {
            _currentPhase = Phase.Focus;
            _phaseEndTime = DateTime.Now.AddMinutes(_focusMinutes);
            _isPaused = false;
        }
        else if (_isPaused)
        {
            // 从暂停恢复:剩余时间继续
            var remaining = _phaseEndTime - _lastTick;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            _phaseEndTime = DateTime.Now + remaining;
            _isPaused = false;
        }
    }

    public void Pause()
    {
        if (_currentPhase == Phase.Idle) return;
        // 暂停:记录当前时刻,Update 中检测 _isPaused 后不再推进倒计时
        _lastTick = DateTime.Now;
        _isPaused = true;
    }

    public void Reset()
    {
        _currentPhase = Phase.Idle;
        _phaseEndTime = DateTime.MinValue;
        _completedPomodoros = 0;
        _timeText.Text = $"{_focusMinutes:D2}:00";
        _phaseText.Text = "🍅 番茄钟";
    }

    public void Skip()
    {
        AdvancePhase(DateTime.Now);
    }

    private void AdvancePhase(DateTime now)
    {
        if (_currentPhase == Phase.Focus)
        {
            _completedPomodoros++;
            bool isLongBreak = _completedPomodoros % _longBreakInterval == 0;
            _currentPhase = isLongBreak ? Phase.LongBreak : Phase.ShortBreak;
            var mins = isLongBreak ? _longBreakMinutes : _shortBreakMinutes;
            _phaseEndTime = now.AddMinutes(mins);
            NotificationService.Notify("番茄钟", isLongBreak ? "长休息时间到,放松一下吧" : "短休息时间到");
            if (!_autoStart) _phaseEndTime = now; // 暂停等待手动开始
        }
        else if (_currentPhase == Phase.ShortBreak || _currentPhase == Phase.LongBreak)
        {
            _currentPhase = Phase.Focus;
            _phaseEndTime = now.AddMinutes(_focusMinutes);
            NotificationService.Notify("番茄钟", "休息结束,开始专注");
            if (!_autoStart) _phaseEndTime = now;
        }
    }

    public void Update(DateTime nowLocal)
    {
        // 0:00 重置今日完成数
        if (nowLocal.Date != _completedDate)
        {
            _completedPomodoros = 0;
            _completedDate = nowLocal.Date;
        }

        if (_currentPhase == Phase.Idle)
        {
            _timeText.Text = $"{_focusMinutes:D2}:00";
            _phaseText.Text = "🍅 番茄钟(空闲)";
            return;
        }

        // 暂停时冻结倒计时:显示固定剩余时间,不推进
        if (_isPaused)
        {
            var pausedRemaining = _phaseEndTime - _lastTick;
            if (pausedRemaining < TimeSpan.Zero) pausedRemaining = TimeSpan.Zero;
            _timeText.Text = $"{(int)pausedRemaining.TotalMinutes:D2}:{pausedRemaining.Seconds:D2} ⏸";
            return;
        }

        _lastTick = nowLocal;

        if (nowLocal >= _phaseEndTime && _phaseEndTime != DateTime.MinValue)
        {
            AdvancePhase(nowLocal);
        }

        var remaining = _phaseEndTime - nowLocal;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

        var phaseLabel = _currentPhase switch
        {
            Phase.Focus => $"🍅 专注 (今日 {_completedPomodoros})",
            Phase.ShortBreak => "☕ 短休息",
            Phase.LongBreak => "🌴 长休息",
            _ => "🍅 番茄钟"
        };
        _phaseText.Text = phaseLabel;
        _timeText.Text = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
    }

    private bool _isPaused;

    public void ApplyConfig()
    {
        if (Config.Settings.TryGetValue("focusMinutes", out var f))
        {
            if (f is int fi) _focusMinutes = fi;
            else if (f is string fs && int.TryParse(fs, out var r)) _focusMinutes = r;
        }
        if (Config.Settings.TryGetValue("shortBreakMinutes", out var sb))
        {
            if (sb is int sbi) _shortBreakMinutes = sbi;
            else if (sb is string sbs && int.TryParse(sbs, out var r)) _shortBreakMinutes = r;
        }
        if (Config.Settings.TryGetValue("longBreakMinutes", out var lb))
        {
            if (lb is int lbi) _longBreakMinutes = lbi;
            else if (lb is string lbs && int.TryParse(lbs, out var r)) _longBreakMinutes = r;
        }
        if (Config.Settings.TryGetValue("longBreakInterval", out var li))
        {
            if (li is int lii) _longBreakInterval = lii;
            else if (li is string lis && int.TryParse(lis, out var r)) _longBreakInterval = r;
        }
        if (Config.Settings.TryGetValue("autoStart", out var a))
        {
            if (a is bool ab) _autoStart = ab;
            else if (a is string ass && bool.TryParse(ass, out var r)) _autoStart = r;
        }

        if (Config.Settings.TryGetValue("fontSize", out var fsz))
        {
            double size = 0;
            if (fsz is double d) size = d;
            else if (fsz is string ss && double.TryParse(ss, out var r)) size = r;
            if (size > 0) { _fontSize = size; _timeText.FontSize = size; }
        }

        if (Config.Settings.TryGetValue("fontColor", out var fc))
        {
            _fontColor = fc.ToString()!;
            try { _timeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_fontColor)); }
            catch { }
        }

        if (Config.Settings.TryGetValue("fontFamily", out var ff) && ff is string ffs && !string.IsNullOrEmpty(ffs))
        {
            _fontFamily = ffs;
            try { _timeText.FontFamily = new FontFamily(ffs); }
            catch { }
        }
    }
}
