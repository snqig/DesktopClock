using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopClock.Models;

namespace DesktopClock.Components;

public class SysMonComponent : IClockComponent
{
    public string Id => "sys_mon";
    public string DisplayName => "系统监控";
    public FrameworkElement View => _panel;
    public ComponentConfig Config { get; set; } = new();

    private readonly StackPanel _panel;
    private readonly TextBlock _cpuText;
    private readonly TextBlock _memText;
    private readonly TextBlock _netText;
    private readonly TextBlock _batteryText;

    private PerformanceCounter? _cpuCounter;
    private long _lastNetBytesSent;
    private DateTime _lastNetTime = DateTime.MinValue;
    private readonly DispatcherTimer _updateTimer;

    public SysMonComponent()
    {
        _panel = new StackPanel { Orientation = Orientation.Horizontal };
        _cpuText = CreateItem("CPU: --");
        _memText = CreateItem("MEM: --");
        _netText = CreateItem("NET: --");
        _batteryText = CreateItem("BAT: --");
        _panel.Children.Add(_cpuText);
        _panel.Children.Add(_memText);
        _panel.Children.Add(_netText);
        _panel.Children.Add(_batteryText);

        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // warmup
        }
        catch { }

        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _updateTimer.Tick += (_, _) => RefreshStats();
        _updateTimer.Start();
    }

    private TextBlock CreateItem(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 10, 0),
            FontFamily = new FontFamily("Consolas, Microsoft YaHei")
        };
    }

    public void Update(DateTime now) { }

    public void ApplyConfig()
    {
        bool showCpu = true, showMem = true, showNet = false, showBat = true;
        if (Config.Settings.TryGetValue("showCpu", out var v1) && v1 is bool b1) showCpu = b1;
        if (Config.Settings.TryGetValue("showMemory", out var v2) && v2 is bool b2) showMem = b2;
        if (Config.Settings.TryGetValue("showNetwork", out var v3) && v3 is bool b3) showNet = b3;
        if (Config.Settings.TryGetValue("showBattery", out var v4) && v4 is bool b4) showBat = b4;

        _cpuText.Visibility = showCpu ? Visibility.Visible : Visibility.Collapsed;
        _memText.Visibility = showMem ? Visibility.Visible : Visibility.Collapsed;
        _netText.Visibility = showNet ? Visibility.Visible : Visibility.Collapsed;
        _batteryText.Visibility = showBat ? Visibility.Visible : Visibility.Collapsed;

        if (Config.Settings.TryGetValue("fontColor", out var fc))
        {
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fc.ToString()!));
                _cpuText.Foreground = brush;
                _memText.Foreground = brush;
                _netText.Foreground = brush;
                _batteryText.Foreground = brush;
            }
            catch { }
        }
    }

    private void RefreshStats()
    {
        try
        {
            double cpu = 0;
            if (_cpuCounter != null)
            {
                cpu = _cpuCounter.NextValue();
                if (cpu < 0) cpu = 0;
                if (cpu > 100) cpu = 100;
            }
            _cpuText.Text = $"CPU: {cpu:F0}%";
        }
        catch { }

        try
        {
            var mem = GetMemoryInfo();
            long used = mem.Total - mem.Available;
            double pct = mem.Total > 0 ? (double)used / mem.Total * 100 : 0;
            _memText.Text = $"MEM: {pct:F0}%";
        }
        catch { }

        try
        {
            var net = GetNetworkSpeed();
            _netText.Text = $"NET: {FormatBytes(net)}/s";
        }
        catch { }

        try
        {
            var bat = System.Windows.Forms.SystemInformation.PowerStatus;
            float percent = bat.BatteryLifePercent;
            string status = bat.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online ? "⚡" : "🔋";
            _batteryText.Text = $"BAT: {status} {percent * 100:F0}%";
        }
        catch { }
    }

    private static string FormatBytes(double bytesPerSec)
    {
        if (bytesPerSec < 1024) return $"{bytesPerSec:F0}B";
        if (bytesPerSec < 1024 * 1024) return $"{bytesPerSec / 1024:F1}K";
        return $"{bytesPerSec / (1024 * 1024):F1}M";
    }

    private static (long Total, long Available) GetMemoryInfo()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
        if (GlobalMemoryStatusEx(ref status))
        {
            return ((long)status.ullTotalPhys, (long)status.ullAvailPhys);
        }
        return (0, 0);
    }

    private double GetNetworkSpeed()
    {
        long totalBytes = 0;
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            var stats = ni.GetIPv4Statistics();
            totalBytes += stats.BytesSent + stats.BytesReceived;
        }

        var now = DateTime.Now;
        if (_lastNetTime == DateTime.MinValue)
        {
            _lastNetBytesSent = totalBytes;
            _lastNetTime = now;
            return 0;
        }

        double deltaSec = (now - _lastNetTime).TotalSeconds;
        long deltaBytes = totalBytes - _lastNetBytesSent;
        _lastNetBytesSent = totalBytes;
        _lastNetTime = now;

        if (deltaSec > 0) return deltaBytes / deltaSec;
        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
