using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace DesktopClock.Render;

/// <summary>
/// 渲染调度器:统一管控所有挂件的帧率。
/// 替代到处 new DispatcherTimer 的旧模式,按场景切换帧率策略。
/// </summary>
public sealed class FrameRenderScheduler : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly List<Action<DateTime>> _subscribers = new();
    private FrameMode _mode = FrameMode.Normal;
    private bool _disposed;

    public FrameMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            ApplyInterval();
        }
    }

    public FrameRenderScheduler() : this(FrameMode.Normal) { }

    /// <summary>以指定初始帧率模式创建调度器,默认立即开始输出帧。</summary>
    public FrameRenderScheduler(FrameMode mode)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background);
        _timer.Tick += OnTick;
        _mode = mode;
        ApplyInterval();
        _timer.Start();
    }

    /// <summary>显式启动(若已启动则无操作),方便 MainWindow 语义对齐。</summary>
    public void Start() => Resume();

    /// <summary>停止输出帧(等同于 Pause)。</summary>
    public void Stop() => Pause();

    /// <summary>订阅每帧回调(传入当前 UTC 时间)。</summary>
    public void Subscribe(Action<DateTime> callback)
    {
        if (!_subscribers.Contains(callback))
            _subscribers.Add(callback);
    }

    /// <summary>取消订阅。</summary>
    public void Unsubscribe(Action<DateTime> callback)
        => _subscribers.Remove(callback);

    private void OnTick(object? sender, EventArgs e)
    {
        if (_subscribers.Count == 0) return;
        var now = DateTime.UtcNow;
        // 复制一份避免回调中修改集合
        foreach (var cb in _subscribers.ToArray())
        {
            try { cb(now); } catch { /* 单个挂件异常不影响整体 */ }
        }
    }

    private void ApplyInterval()
    {
        _timer.Interval = _mode switch
        {
            FrameMode.Normal => TimeSpan.FromSeconds(1),     // 1 FPS
            FrameMode.Interactive => TimeSpan.FromMilliseconds(33), // ~30 FPS
            FrameMode.Idle => TimeSpan.FromSeconds(5),       // 闲置降频
            _ => TimeSpan.FromSeconds(1)
        };
    }

    /// <summary>系统休眠/锁屏时调用,暂停渲染。</summary>
    public void Pause() => _timer.Stop();

    /// <summary>系统唤醒/解锁时调用,恢复渲染。</summary>
    public void Resume()
    {
        if (!_timer.IsEnabled) _timer.Start();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _subscribers.Clear();
    }
}

/// <summary>帧率模式</summary>
public enum FrameMode
{
    /// <summary>默认:1 FPS(时间变更刷新)</summary>
    Normal,
    /// <summary>交互:30 FPS(拖拽/编辑模式)</summary>
    Interactive,
    /// <summary>闲置:0.2 FPS(降耗)</summary>
    Idle
}
