using System;

namespace DesktopClock.Services;

/// <summary>
/// 全局通知服务:组件通过事件触发桌面通知,由 MainWindow 订阅并调用托盘 ShowBalloonTip。
/// 解耦组件与 NotifyIcon,避免每个组件都持有托盘引用。
/// </summary>
public static class NotificationService
{
    /// <summary>通知请求事件,参数为标题与正文。</summary>
    public static event Action<string, string>? NotificationRequested;

    /// <summary>请求显示桌面通知(由 MainWindow 订阅并转发到托盘图标)。</summary>
    public static void Notify(string title, string body)
    {
        try { NotificationRequested?.Invoke(title, body); }
        catch { }
    }
}
