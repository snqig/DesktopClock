using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DesktopClock.Config;
using DesktopClock.Models;
using DesktopClock.Render;
using DesktopClock.Services;

namespace DesktopClock.Views.Widgets;

/// <summary>
/// 倒计时挂件视图。
/// 订阅 FrameRenderScheduler 每帧刷新,样式由 CountdownStyle 绑定。
/// 支持单任务和多任务轮播两种模式。
/// </summary>
public partial class CountdownWidget : UserControl
{
    private CountdownEngine? _engine;
    private CountdownStyle? _style;
    private bool _blinkOn;
    private DateTime _lastBlinkToggle;

    // 多任务轮播
    private List<CountdownTimerSetting>? _tasks;
    private int _taskIndex;
    private DateTime _lastRotation = DateTime.UtcNow;
    private int _rotationSeconds = 10;

    public CountdownWidget()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    /// <summary>初始化引擎与样式(单任务模式)。</summary>
    public void Initialize(CountdownTimerSetting timerSetting, CountdownStyle style)
    {
        _style = style;
        _engine = new CountdownEngine(timerSetting);
        _engine.Ended += OnCountdownEnded;
        _tasks = null;
        ApplyStyle(style);
    }

    /// <summary>
    /// 初始化多任务轮播模式。
    /// 当 tasks 非空时按 rotationSeconds 周期循环切换显示;
    /// 空列表则回退到单任务(timerSetting)。
    /// </summary>
    public void InitializeMulti(CountdownTimerSetting timerSetting, CountdownStyle style,
        List<CountdownTask> tasks, int rotationSeconds)
    {
        _style = style;
        _rotationSeconds = Math.Max(3, rotationSeconds);
        if (tasks != null && tasks.Count > 0)
        {
            _tasks = new List<CountdownTimerSetting>();
            foreach (var t in tasks)
            {
                if (!t.Enabled) continue;
                _tasks.Add(new CountdownTimerSetting
                {
                    TargetTime = t.TargetTimeUtc,
                    Title = t.Title,
                    ShowTitle = true,
                    DisplayMode = string.IsNullOrEmpty(t.DisplayMode) ? "days" : t.DisplayMode,
                    EndAction = string.IsNullOrEmpty(t.EndAction) ? "blink" : t.EndAction,
                    StopAtZero = true
                });
            }
            if (_tasks.Count == 0) _tasks = null;
        }
        // 总是初始化默认引擎(用于单任务模式或多任务首项)
        var first = _tasks != null && _tasks.Count > 0 ? _tasks[0] : timerSetting;
        _engine = new CountdownEngine(first);
        _engine.Ended += OnCountdownEnded;
        _taskIndex = 0;
        ApplyStyle(style);
    }

    /// <summary>每帧刷新(由外部 FrameRenderScheduler 调用)。</summary>
    public void OnFrame(DateTime nowUtc)
    {
        if (_engine == null) return;

        // 多任务轮播:到达间隔后切到下一任务
        if (_tasks != null && _tasks.Count > 1 &&
            (nowUtc - _lastRotation).TotalSeconds >= _rotationSeconds)
        {
            _lastRotation = nowUtc;
            _taskIndex = (_taskIndex + 1) % _tasks.Count;
            var setting = _tasks[_taskIndex];
            _engine.UpdateSetting(setting);
            if (TitleText != null && !string.IsNullOrEmpty(setting.Title))
                TitleText.Text = setting.Title;
            Logger.Information($"[Countdown] rotate to task #{_taskIndex}: {setting.Title}");
        }

        var text = _engine.Render(nowUtc);

        // 倒计时结束后,根据动作闪烁文字
        if (_engine.EndAction == "blink")
        {
            if (nowUtc - _lastBlinkToggle > TimeSpan.FromMilliseconds(500))
            {
                _blinkOn = !_blinkOn;
                _lastBlinkToggle = nowUtc;
            }
            CountdownText.Opacity = _blinkOn ? 1.0 : 0.2;
        }
        else
        {
            CountdownText.Opacity = 1.0;
        }

        if (CountdownText.Text != text)
            CountdownText.Text = text;
    }

    private void ApplyStyle(CountdownStyle s)
    {
        // 字体
        try { CountdownText.FontFamily = new FontFamily(s.FontFamily); } catch { }
        CountdownText.FontSize = s.FontSize;
        try { CountdownText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s.FontColor)); }
        catch { CountdownText.Foreground = Brushes.White; }

        // 描边:WPF 原生 TextBlock 无描边,用 BitmapEffect 替代(简单方案)
        // 若需精确描边,需用 Geometry 或自定义控件,后续优化
        if (s.StrokeEnabled && s.StrokeThickness > 0)
        {
            // 简化方案:用 DropShadow 模拟描边
            CountdownText.Effect = new DropShadowEffect
            {
                Color = TryParseColor(s.StrokeColor, Colors.Black),
                ShadowDepth = 0,
                BlurRadius = s.StrokeThickness * 2,
                Opacity = 1.0
            };
        }
        else if (s.ShadowEnabled)
        {
            CountdownText.Effect = new DropShadowEffect
            {
                Color = TryParseColor(s.ShadowColor, Colors.Black),
                ShadowDepth = s.ShadowSize / 2,
                BlurRadius = s.ShadowSize,
                Opacity = 0.8
            };
        }
        else
        {
            CountdownText.Effect = null;
        }
    }

    private static Color TryParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private void OnCountdownEnded()
    {
        // 结束动作的具体实现在 OnFrame 中处理 blink
        // alert/sound 在此扩展
        Logger.Information($"[Countdown] ended, label={_engine?.Title}, action={_engine?.EndAction}");
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (_engine == null) return;
                switch (_engine.EndAction)
                {
                    case "alert":
                        MessageBox.Show($"倒计时结束:{_engine.Title}", "DesktopClock",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    case "sound":
                        System.Media.SystemSounds.Asterisk.Play();
                        break;
                }
            }
            catch { }
        });
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 可扩展:按窗口大小自动缩放字体
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_engine != null) _engine.Ended -= OnCountdownEnded;
    }
}

