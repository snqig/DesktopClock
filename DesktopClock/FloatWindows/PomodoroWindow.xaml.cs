using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopClock.Core;
using DesktopClock.Services;

namespace DesktopClock.FloatWindows;

/// <summary>
/// 番茄钟悬浮窗口(P2)：25 分钟专注 + 5 分钟休息循环，每 longBreakInterval 个番茄触发长休息。
/// 独立窗口版本，由 BaseFloatWindow 提供拖拽、置顶、透明度、位置持久化等通用能力。
/// 配置键(通过 ComponentWindowConfig.Settings 注入)：
///   "focusMinutes"      - 专注时长(默认 25)
///   "shortBreakMinutes" - 短休息(默认 5)
///   "longBreakMinutes"  - 长休息(默认 15)
///   "longBreakInterval" - 长休息间隔番茄数(默认 4)
///   "autoStart"         - 阶段切换后自动开始(默认 false)
/// 右键菜单：开始 / 暂停 / 重置 / 跳过 + 锁定 / 置顶 / 设置 / 关闭。
/// </summary>
public partial class PomodoroWindow : BaseFloatWindow
{
    /// <summary>番茄钟阶段</summary>
    public enum Phase { Idle, Focus, ShortBreak, LongBreak }

    private readonly DispatcherTimer _timer;

    // 运行状态
    private Phase _currentPhase = Phase.Idle;
    private DateTime _phaseEndTime = DateTime.MinValue;
    private bool _isPaused;
    private TimeSpan _pausedRemaining = TimeSpan.Zero;
    private int _completedPomodoros;        // 今日完成番茄数
    private DateTime _completedDate = DateTime.Today;

    // 配置(从 Settings 读取)
    private int _focusMinutes = 25;
    private int _shortBreakMinutes = 5;
    private int _longBreakMinutes = 15;
    private int _longBreakInterval = 4;
    private bool _autoStart;

    public PomodoroWindow()
    {
        ComponentId = "pomodoro";
        InitializeComponent();

        // 用 PreviewMouseRightButtonDown 构建完整自定义菜单(包含基础项 + 番茄钟控制)，
        // 标记 Handled 以阻止基类 OnRightClick 再次弹出菜单。
        PreviewMouseRightButtonDown += OnPreviewRightClick;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateDisplay();
        _timer.Start();

        UpdateDisplay();
    }

    // ==================== 阶段控制 ====================

    /// <summary>开始 / 从暂停恢复。</summary>
    public void Start()
    {
        if (_currentPhase == Phase.Idle)
        {
            _currentPhase = Phase.Focus;
            _phaseEndTime = DateTime.Now.AddMinutes(_focusMinutes);
            _isPaused = false;
            _pausedRemaining = TimeSpan.Zero;
        }
        else if (_isPaused)
        {
            _phaseEndTime = DateTime.Now + _pausedRemaining;
            _isPaused = false;
            _pausedRemaining = TimeSpan.Zero;
        }
        UpdateDisplay();
    }

    /// <summary>暂停当前阶段，冻结倒计时。</summary>
    public void Pause()
    {
        if (_currentPhase == Phase.Idle || _isPaused) return;
        var remaining = _phaseEndTime - DateTime.Now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        _pausedRemaining = remaining;
        _isPaused = true;
        UpdateDisplay();
    }

    /// <summary>重置为空闲状态并清零今日完成数。</summary>
    public void Reset()
    {
        _currentPhase = Phase.Idle;
        _phaseEndTime = DateTime.MinValue;
        _isPaused = false;
        _pausedRemaining = TimeSpan.Zero;
        _completedPomodoros = 0;
        UpdateDisplay();
    }

    /// <summary>跳过当前阶段，立即进入下一阶段。</summary>
    public void Skip()
    {
        AdvancePhase(DateTime.Now);
        UpdateDisplay();
    }

    /// <summary>
    /// 阶段切换：Focus→(短/长)休息→Focus 循环，每 longBreakInterval 个番茄触发长休息。
    /// 非自动开始时进入暂停态等待用户手动开始。
    /// </summary>
    private void AdvancePhase(DateTime now)
    {
        if (_currentPhase == Phase.Focus)
        {
            _completedPomodoros++;
            bool isLongBreak = _longBreakInterval > 0 && _completedPomodoros % _longBreakInterval == 0;
            _currentPhase = isLongBreak ? Phase.LongBreak : Phase.ShortBreak;
            var mins = isLongBreak ? _longBreakMinutes : _shortBreakMinutes;
            _phaseEndTime = now.AddMinutes(mins);
            NotificationService.Notify("番茄钟", isLongBreak ? "长休息时间到，放松一下吧" : "短休息时间到");
            if (!_autoStart) PauseAt(now);
            else _isPaused = false;
        }
        else if (_currentPhase == Phase.ShortBreak || _currentPhase == Phase.LongBreak)
        {
            _currentPhase = Phase.Focus;
            _phaseEndTime = now.AddMinutes(_focusMinutes);
            NotificationService.Notify("番茄钟", "休息结束，开始专注");
            if (!_autoStart) PauseAt(now);
            else _isPaused = false;
        }
    }

    /// <summary>进入暂停态并保留从 now 起的完整剩余时长，便于 Start() 恢复。</summary>
    private void PauseAt(DateTime now)
    {
        _pausedRemaining = _phaseEndTime - now;
        if (_pausedRemaining < TimeSpan.Zero) _pausedRemaining = TimeSpan.Zero;
        _isPaused = true;
    }

    // ==================== 显示刷新 ====================

    private void UpdateDisplay()
    {
        var now = DateTime.Now;

        // 跨天重置今日完成数
        if (now.Date != _completedDate)
        {
            _completedPomodoros = 0;
            _completedDate = now.Date;
        }

        if (_currentPhase == Phase.Idle)
        {
            PhaseText.Text = "🍅 番茄钟(空闲)";
            TimeText.Text = $"{_focusMinutes:D2}:00";
            return;
        }

        if (_isPaused)
        {
            var r = _pausedRemaining;
            if (r < TimeSpan.Zero) r = TimeSpan.Zero;
            PhaseText.Text = PhaseLabel() + " ⏸";
            TimeText.Text = $"{(int)r.TotalMinutes:D2}:{r.Seconds:D2}";
            return;
        }

        // 阶段结束：自动推进
        if (_phaseEndTime != DateTime.MinValue && now >= _phaseEndTime)
        {
            AdvancePhase(now);
        }

        var remaining = _phaseEndTime - now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

        PhaseText.Text = PhaseLabel();
        TimeText.Text = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
    }

    private string PhaseLabel()
    {
        return _currentPhase switch
        {
            Phase.Focus => $"🍅 专注 (今日 {_completedPomodoros})",
            Phase.ShortBreak => "☕ 短休息",
            Phase.LongBreak => "🌴 长休息",
            _ => "🍅 番茄钟"
        };
    }

    // ==================== 配置读写 ====================

    /// <summary>从 ComponentWindowConfig.Settings 读取番茄钟专属参数。</summary>
    private void LoadPomodoroSettings()
    {
        var cfg = ComponentManager.Instance.GetConfig(ComponentId);
        if (cfg == null) return;

        _focusMinutes = cfg.GetInt("focusMinutes", 25);
        _shortBreakMinutes = cfg.GetInt("shortBreakMinutes", 5);
        _longBreakMinutes = cfg.GetInt("longBreakMinutes", 15);
        _longBreakInterval = cfg.GetInt("longBreakInterval", 4);
        _autoStart = cfg.GetBool("autoStart", false);
    }

    // ==================== BaseFloatWindow 实现 ====================

    public override void LoadFromConfig()
    {
        var cfg = ComponentManager.Instance.EnsureConfig(ComponentId);

        // 位置 / 尺寸
        if (!double.IsNaN(cfg.Left)) Left = cfg.Left;
        if (!double.IsNaN(cfg.Top)) Top = cfg.Top;
        Width = cfg.Width > 0 ? cfg.Width : 180;
        Height = cfg.Height > 0 ? cfg.Height : 90;
        ClampToScreen();

        IsTopmost = cfg.Topmost;
        DesktopWidgetMode = cfg.DesktopWidgetMode;
        IsLocked = cfg.LockPosition;
        WindowOpacity = cfg.Opacity;

        // 样式(应用到主时间文本)
        try { TimeText.FontFamily = new FontFamily(cfg.FontFamily); } catch { }
        TimeText.FontSize = cfg.FontSize;
        try
        {
            TimeText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(cfg.FontColor));
        }
        catch { }

        // 番茄钟专属参数
        LoadPomodoroSettings();
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
        UpdateDisplay();
    }

    // ==================== 右键菜单 ====================

    private void OnPreviewRightClick(object sender, MouseButtonEventArgs e)
    {
        // 阻止基类 OnRightClick 弹出默认菜单，改用完整自定义菜单
        e.Handled = true;

        var menu = new ContextMenu();

        // 番茄钟控制
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
        menu.Items.Add(new Separator());

        // 窗口控制
        var miLock = new MenuItem { Header = IsLocked ? "解锁位置" : "锁定位置" };
        miLock.Click += (_, _) => { IsLocked = !IsLocked; SavePosition(); };
        menu.Items.Add(miLock);

        var miTopmost = new MenuItem
        {
            Header = IsTopmost ? "取消置顶" : "窗口置顶",
            IsCheckable = true,
            IsChecked = IsTopmost
        };
        miTopmost.Click += (_, _) =>
        {
            IsTopmost = !IsTopmost;
            var h = new WindowInteropHelper(this).Handle;
            if (h != IntPtr.Zero) NativeMethods.SetTopmost(h, IsTopmost);
            SavePosition();
        };
        menu.Items.Add(miTopmost);

        var miSettings = new MenuItem { Header = "组件设置" };
        miSettings.Click += (_, _) => OpenComponentSettings();
        menu.Items.Add(miSettings);

        menu.Items.Add(new Separator());

        var miClose = new MenuItem { Header = "关闭组件" };
        miClose.Click += (_, _) => Close();
        menu.Items.Add(miClose);

        menu.IsOpen = true;
    }
}
