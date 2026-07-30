using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopClock.Services;

/// <summary>
/// Windows 11 Mica / Acrylic 背景效果辅助类。
/// 通过 DWM API 设置窗口系统背景类型(SystemBackdrop)。
/// </summary>
public static class WindowBackdrop
{
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private enum DWMSBT
    {
        Auto = 0,
        None = 1,
        MainWindow = 2,      // Mica
        TransientWindow = 3, // Acrylic / MicaAlt
        TabbedWindow = 4     // Tabbed
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    /// <summary>
    /// 应用系统背景效果。仅对 Windows 11 (Build >= 22000) 生效。
    /// </summary>
    public static void Apply(Window window, BackdropType type, bool darkMode)
    {
        if (!IsWindows11()) return;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        // 先设置暗色/亮色模式,确保 Mica/Acrylic 色调正确
        int dark = darkMode ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        int backdrop = type switch
        {
            BackdropType.Mica => (int)DWMSBT.MainWindow,
            BackdropType.Acrylic => (int)DWMSBT.TransientWindow,
            BackdropType.Tabbed => (int)DWMSBT.TabbedWindow,
            _ => (int)DWMSBT.None
        };

        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
    }

    /// <summary>
    /// 清除系统背景效果,恢复为普通窗口。
    /// </summary>
    public static void Clear(Window window)
    {
        if (!IsWindows11()) return;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int none = (int)DWMSBT.None;
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref none, sizeof(int));
    }

    public static bool IsWindows11()
    {
        try
        {
            var os = Environment.OSVersion;
            return os.Platform == PlatformID.Win32NT && os.Version.Build >= 22000;
        }
        catch { return false; }
    }
}

public enum BackdropType
{
    None,
    Mica,
    Acrylic,
    Tabbed
}
