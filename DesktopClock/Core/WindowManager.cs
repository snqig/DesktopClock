using System;
using System.Collections.Generic;
using System.Windows;
using DesktopClock.Config;

namespace DesktopClock.Core;

/// <summary>
/// 窗口管理器:统一管理所有 LayeredWindow 实例的生命周期与状态。
/// 双挂件(Clock/Countdown)各自独立窗口,互不干扰。
/// 禁止 View 直接调用 Win32 API,全部经此入口。
/// </summary>
public sealed class WindowManager : IDisposable
{
    private readonly Dictionary<string, LayeredWindow> _windows = new();
    private bool _disposed;

    /// <summary>注册并显示一个挂件窗口。</summary>
    public LayeredWindow Register(string widgetId, Window window, WindowSetting? setting = null)
    {
        if (_windows.ContainsKey(widgetId))
            throw new InvalidOperationException($"Widget '{widgetId}' already registered");

        var layered = new LayeredWindow(window, widgetId);
        _windows[widgetId] = layered;

        if (setting != null) layered.ApplyWindowSetting(setting);

        window.Show();
        return layered;
    }

    /// <summary>注销挂件窗口(关闭并释放)。</summary>
    public void Unregister(string widgetId)
    {
        if (!_windows.TryGetValue(widgetId, out var layered)) return;
        try { layered.Window.Close(); } catch { }
        layered.Dispose();
        _windows.Remove(widgetId);
    }

    /// <summary>获取指定挂件窗口(可能为 null)。</summary>
    public LayeredWindow? Get(string widgetId)
        => _windows.TryGetValue(widgetId, out var w) ? w : null;

    /// <summary>显示指定挂件。</summary>
    public void Show(string widgetId)
    {
        var w = Get(widgetId);
        if (w != null && w.Window.Visibility != Visibility.Visible)
            w.Window.Show();
    }

    /// <summary>隐藏指定挂件(不释放)。</summary>
    public void Hide(string widgetId)
    {
        var w = Get(widgetId);
        if (w != null) w.Window.Hide();
    }

    /// <summary>显示/隐藏全部挂件。</summary>
    public void SetAllVisible(bool visible)
    {
        foreach (var w in _windows.Values)
        {
            if (visible) w.Window.Show();
            else w.Window.Hide();
        }
    }

    /// <summary>切换指定挂件穿透。</summary>
    public void SetClickThrough(string widgetId, bool transparent)
        => Get(widgetId)?.SetClickThrough(transparent);

    /// <summary>切换指定挂件置顶。</summary>
    public void SetTopmost(string widgetId, bool topmost)
        => Get(widgetId)?.SetTopmost(topmost);

    /// <summary>枚举当前所有挂件窗口(只读)。</summary>
    public IEnumerable<LayeredWindow> All => _windows.Values;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var id in new List<string>(_windows.Keys))
            Unregister(id);
    }
}
