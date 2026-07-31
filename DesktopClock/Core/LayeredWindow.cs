using System;
using System.Windows;
using System.Windows.Interop;
using DesktopClock.Config;

namespace DesktopClock.Core;

/// <summary>
/// 分层窗口管理器:对一个 WPF Window 实例提供
/// 透明度/穿透/置顶/位置的统一管控,替代 AllowsTransparency 方案。
/// 通过 WindowManager 创建,不直接 new。
/// </summary>
public sealed class LayeredWindow : IDisposable
{
    public Window Window { get; }
    public IntPtr Handle { get; private set; }
    public string WidgetId { get; }

    private bool _clickThrough;
    private bool _topmost = true;
    private byte _alpha = 255;
    private bool _disposed;

    internal LayeredWindow(Window window, string widgetId)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
        WidgetId = widgetId;
        window.SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        Handle = new WindowInteropHelper(Window).Handle;
        if (Handle == IntPtr.Zero) return;

        NativeMethods.EnsureLayered(Handle);
        NativeMethods.EnsureToolWindow(Handle);
        NativeMethods.SetLayeredAlpha(Handle, _alpha);
        NativeMethods.SetClickThrough(Handle, _clickThrough);
        NativeMethods.SetTopmost(Handle, _topmost);
    }

    /// <summary>设置鼠标穿透(实时生效)。</summary>
    public void SetClickThrough(bool transparent)
    {
        _clickThrough = transparent;
        if (Handle != IntPtr.Zero)
            NativeMethods.SetClickThrough(Handle, transparent);
    }

    /// <summary>设置置顶。</summary>
    public void SetTopmost(bool topmost)
    {
        _topmost = topmost;
        if (Handle != IntPtr.Zero)
            NativeMethods.SetTopmost(Handle, topmost);
    }

    /// <summary>设置整体透明度(0~1)。</summary>
    public void SetOpacity(double opacity)
    {
        _alpha = (byte)Math.Clamp((int)(opacity * 255), 0, 255);
        if (Handle != IntPtr.Zero)
            NativeMethods.SetLayeredAlpha(Handle, _alpha);
    }

    /// <summary>应用 WindowSetting 全部属性。</summary>
    public void ApplyWindowSetting(WindowSetting setting)
    {
        if (setting == null) return;
        SetOpacity(setting.Opacity);
        SetClickThrough(setting.ClickThrough);
        SetTopmost(setting.Topmost);
        if (!double.IsNaN(setting.Left)) Window.Left = setting.Left;
        if (!double.IsNaN(setting.Top)) Window.Top = setting.Top;
        if (setting.Width > 0) Window.Width = setting.Width;
        if (setting.Height > 0) Window.Height = setting.Height;
    }

    /// <summary>读取当前窗口位置到 WindowSetting。</summary>
    public WindowSetting CaptureWindowSetting()
    {
        return new WindowSetting
        {
            Left = Window.Left,
            Top = Window.Top,
            Width = Window.ActualWidth > 0 ? Window.ActualWidth : Window.Width,
            Height = Window.ActualHeight > 0 ? Window.ActualHeight : Window.Height,
            Opacity = _alpha / 255.0,
            Topmost = _topmost,
            ClickThrough = _clickThrough,
            LockPosition = false
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Window.SourceInitialized -= OnSourceInitialized;
    }
}
