using System;
using System.Collections.Generic;
using System.Windows;
using DesktopClock.Config;
using DesktopClock.Core;

namespace DesktopClock.Services;

/// <summary>
/// 挂件生命周期管理器:统一管理 Clock/Countdown 等挂件实例的创建、显示、隐藏、销毁。
/// 程序启动时根据 AppConfig 启用对应挂件,退出时统一释放。
/// </Affirma>
/// </summary>
public sealed class WidgetManager : IDisposable
{
    private readonly WindowManager _windowManager;
    private readonly Dictionary<string, Func<Window>> _factories = new();
    private readonly Dictionary<string, bool> _enabledStates = new();
    private bool _disposed;

    /// <summary>底层窗口管理器(只读访问,用于 MainWindow 读取倒计时窗口句柄等)</summary>
    public WindowManager WindowManager => _windowManager;

    /// <summary>可选:关联的帧调度器(用于 StopAll 时通知停止)</summary>
    public DesktopClock.Render.FrameRenderScheduler? Scheduler { get; set; }

    public WidgetManager(WindowManager windowManager)
    {
        _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
    }

    /// <summary>
    /// 便捷构造:同时注入 WindowManager 和 FrameRenderScheduler。
    /// MainWindow 调用此构造以获得完整能力。
    /// </summary>
    public WidgetManager(WindowManager windowManager, DesktopClock.Render.FrameRenderScheduler scheduler)
        : this(windowManager)
    {
        Scheduler = scheduler;
    }

    /// <summary>注册挂件创建工厂(在启动时调用)。</summary>
    /// <param name="widgetId">挂件 ID(如 "clock","countdown")</param>
    /// <param name="factory">创建挂件窗口的工厂方法</param>
    /// <param name="enabledByDefault">默认是否启用</param>
    public void RegisterFactory(string widgetId, Func<Window> factory, bool enabledByDefault = false)
    {
        _factories[widgetId] = factory;
        _enabledStates[widgetId] = enabledByDefault;
    }

    /// <summary>启动指定挂件(创建并显示窗口)。</summary>
    public void Start(string widgetId, WindowSetting? setting = null)
    {
        if (!_factories.TryGetValue(widgetId, out var factory))
            throw new InvalidOperationException($"Widget '{widgetId}' factory not registered");
        if (_windowManager.Get(widgetId) != null) return; // 已在运行

        var window = factory();
        _windowManager.Register(widgetId, window, setting);
        _enabledStates[widgetId] = true;
    }

    /// <summary>停止指定挂件(关闭并释放)。</summary>
    public void Stop(string widgetId)
    {
        _windowManager.Unregister(widgetId);
        _enabledStates[widgetId] = false;
    }

    /// <summary>显示挂件(已存在则 Show,不存在则创建)。</summary>
    public void Show(string widgetId)
    {
        if (_windowManager.Get(widgetId) == null)
            Start(widgetId);
        else
            _windowManager.Show(widgetId);
        _enabledStates[widgetId] = true;
    }

    /// <summary>隐藏挂件(不释放)。</summary>
    public void Hide(string widgetId)
    {
        _windowManager.Hide(widgetId);
        _enabledStates[widgetId] = false;
    }

    /// <summary>显示/隐藏全部已注册挂件。</summary>
    public void SetAllVisible(bool visible)
    {
        foreach (var id in _factories.Keys)
        {
            if (visible) Show(id);
            else _windowManager.Hide(id);
        }
    }

    /// <summary>查询挂件是否启用(在运行)。</summary>
    public bool IsEnabled(string widgetId)
        => _enabledStates.TryGetValue(widgetId, out var v) && v;

    /// <summary>枚举所有已注册挂件 ID。</summary>
    public IEnumerable<string> RegisteredIds => _factories.Keys;

    /// <summary>枚举当前正在运行的挂件 ID。</summary>
    public IEnumerable<string> RunningIds
    {
        get
        {
            foreach (var id in _factories.Keys)
                if (_windowManager.Get(id) != null) yield return id;
        }
    }

    /// <summary>停止所有正在运行的挂件(不 Dispose,可再次 Start)。</summary>
    public void StopAll()
    {
        foreach (var id in RunningIds.ToArray())
            Stop(id);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopAll();
        _windowManager.Dispose();
        _factories.Clear();
        _enabledStates.Clear();
    }
}
