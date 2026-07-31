using System;
using System.Runtime.InteropServices;

namespace DesktopClock.Core;

/// <summary>
/// Win32 API 封装:分层窗口、扩展样式、窗口位置、穿透控制。
/// 所有窗口操作统一入口,禁止 View 直接调用。
/// </summary>
internal static class NativeMethods
{
    // === 扩展样式 ===
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOPMOST = 0x00000008;

    // === SetWindowPos 标志 ===
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_HIDEWINDOW = 0x0080;
    public const int HWND_TOPMOST = -1;
    public const int HWND_NOTOPMOST = -2;

    // === Layered 窗口 ===
    public const int ULW_ALPHA = 0x00000002;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    public static extern bool IsIconic(IntPtr hWnd);

    /// <summary>设置窗口鼠标穿透(WS_EX_TRANSPARENT),实时开关。</summary>
    public static void SetClickThrough(IntPtr hWnd, bool transparent)
    {
        int ex = GetWindowLong(hWnd, GWL_EXSTYLE);
        if (transparent) ex |= WS_EX_TRANSPARENT;
        else ex &= ~WS_EX_TRANSPARENT;
        SetWindowLong(hWnd, GWL_EXSTYLE, ex);
    }

    /// <summary>设置窗口置顶。</summary>
    public static void SetTopmost(IntPtr hWnd, bool topmost)
    {
        SetWindowPos(hWnd, topmost ? HWND_TOPMOST : HWND_NOTOPMOST,
            0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>设置分层窗口透明度(0~255)。</summary>
    public static void SetLayeredAlpha(IntPtr hWnd, byte alpha)
    {
        const uint LWA_ALPHA = 0x00000002;
        SetLayeredWindowAttributes(hWnd, 0, alpha, LWA_ALPHA);
    }

    /// <summary>确保窗口拥有分层样式(Layered)。</summary>
    public static void EnsureLayered(IntPtr hWnd)
    {
        int ex = GetWindowLong(hWnd, GWL_EXSTYLE);
        if ((ex & WS_EX_LAYERED) == 0)
            SetWindowLong(hWnd, GWL_EXSTYLE, ex | WS_EX_LAYERED);
    }

    /// <summary>设置 ToolWindow 样式(不在任务栏显示)。</summary>
    public static void EnsureToolWindow(IntPtr hWnd)
    {
        int ex = GetWindowLong(hWnd, GWL_EXSTYLE);
        if ((ex & WS_EX_TOOLWINDOW) == 0)
            SetWindowLong(hWnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW);
    }
}
