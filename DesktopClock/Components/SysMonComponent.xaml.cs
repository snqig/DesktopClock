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

/// <summary>
/// 系统监控组件 — 跑马灯横向滚动
/// 宽度由外部 SetScrollWidth 设置(与时间组件右边缘对齐),
/// 文字从右边缘进入,向左滚动,形成无缝循环。
/// 支持 CPU / 内存 / 网速 / 电池 四项指标,可配置显示项、颜色、字体。
/// </summary>
public class SysMonComponent : IClockComponent
{
    public string Id => "sys_mon";
    public string DisplayName => "系统监控";
    public FrameworkElement View => _scrollHost;
    public ComponentConfig Config { get; set; } = new();

    private readonly Canvas _canvas;
    private readonly Border _scrollHost;
    private readonly TextBlock _marqueeText;

    private bool _showCpu = true;
    private bool _showMem = true;
    private bool _showNet = false;
    private bool _showBat = true;
    private double _scrollSpeed = 60;
    private string _fontColor = "#FFD1D1D6";
    private double _fontSize = 12;
    private string _fontFamily = "Consolas, Microsoft YaHei";

    private PerformanceCounter? _cpuCounter;
    private long _lastNetBytesTotal;
    private DateTime _lastNetTime = DateTime.MinValue;
    private readonly DispatcherTimer _refreshTimer;

    private double _offset;
    private double _scrollWidth;
    private DateTime _lastAnimTick = DateTime.Now;
    private string _currentMarqueeText = "";

    public SysMonComponent()
    {
        _marqueeText = new TextBlock
        {
            Text = "",
            FontSize = _fontSize,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_fontColor)),
            FontFamily = new FontFamily(_fontFamily),
            VerticalAlignment = VerticalAlignment.Center
        };

        _canvas = new Canvas
        {
            Height = 24,
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _canvas.Children.Add(_marqueeText);
        Canvas.SetLeft(_marqueeText, 0);
        Canvas.SetTop(_marqueeText, 0);

        _scrollHost = new Border
        {
            Child = _canvas,
            Height = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
            Background = Brushes.Transparent
        };

        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue();
        }
        catch { }

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += (_, _) =>
        {
            try { RefreshStatsAndText(); }
            catch { }
        };
        _refreshTimer.Start();

        CompositionTarget.Rendering += OnRendering;
        RefreshStatsAndText();
    }

    /// <summary>
    /// 由 MainWindow 调用,设置跑马灯可视宽度(与时间组件等宽)。
    /// </summary>
    public void SetScrollWidth(double width)
    {
        if (width <= 0) return;
        _scrollWidth = width;
        _scrollHost.Width = width;
        _canvas.Width = width;
        _offset = width;
        Canvas.SetLeft(_marqueeText, _offset);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_currentMarqueeText)) return;
        if (_scrollWidth <= 0) return;

        double textWidth = _marqueeText.ActualWidth;
        if (textWidth <= 0) return;

        var now = DateTime.Now;
        double dt = (now - _lastAnimTick).TotalSeconds;
        _lastAnimTick = now;
        if (dt <= 0 || dt > 0.5) dt = 0.033;

        // 从右到左滚动
        _offset -= _scrollSpeed * dt;

        // 无缝循环:文字完全滚出左侧后,从右边缘重新进入
        double cycleWidth = textWidth + _scrollWidth;
        while (_offset < -textWidth) _offset += cycleWidth;

        Canvas.SetLeft(_marqueeText, _offset);
    }

    public void Update(DateTime now) { }

    public void ApplyConfig()
    {
        if (Config.Settings.TryGetValue("showCpu", out var v1) && v1 is bool b1) _showCpu = b1;
        if (Config.Settings.TryGetValue("showMemory", out var v2) && v2 is bool b2) _showMem = b2;
        if (Config.Settings.TryGetValue("showNetwork", out var v3) && v3 is bool b3) _showNet = b3;
        if (Config.Settings.TryGetValue("showBattery", out var v4) && v4 is bool b4) _showBat = b4;

        if (Config.Settings.TryGetValue("fontColor", out var fc))
        {
            _fontColor = fc.ToString()!;
            try { _marqueeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_fontColor)); }
            catch { }
        }

        if (Config.Settings.TryGetValue("fontSize", out var fsz))
        {
            double size = 12;
            if (fsz is double d) size = d;
            else if (fsz is string ss2 && double.TryParse(ss2, out var r)) size = r;
            if (size > 0)
            {
                _fontSize = size;
                _marqueeText.FontSize = size;
                _canvas.Height = Math.Max(20, size + 8);
                _scrollHost.Height = _canvas.Height;
            }
        }

        if (Config.Settings.TryGetValue("fontFamily", out var ff) && ff is string ffs && !string.IsNullOrEmpty(ffs))
        {
            _fontFamily = ffs;
            try { _marqueeText.FontFamily = new FontFamily(ffs); }
            catch { }
        }

        RefreshStatsAndText();
    }

    private void RefreshStatsAndText()
    {
        var parts = new System.Collections.Generic.List<string>();

        if (_showCpu)
        {
            double cpu = 0;
            try
            {
                if (_cpuCounter != null)
                {
                    cpu = _cpuCounter.NextValue();
                    if (cpu < 0) cpu = 0;
                    if (cpu > 100) cpu = 100;
                }
            }
            catch { }
            parts.Add($"CPU {cpu:F0}%");
        }

        if (_showMem)
        {
            try
            {
                var mem = GetMemoryInfo();
                long used = mem.Total - mem.Available;
                double pct = mem.Total > 0 ? (double)used / mem.Total * 100 : 0;
                parts.Add($"MEM {pct:F0}%");
            }
            catch { parts.Add("MEM --"); }
        }

        if (_showNet)
        {
            try
            {
                var speed = GetNetworkSpeed();
                parts.Add($"NET {FormatBytes(speed)}/s");
            }
            catch { parts.Add("NET --"); }
        }

        if (_showBat)
        {
            try
            {
                var bat = System.Windows.Forms.SystemInformation.PowerStatus;
                float percent = bat.BatteryLifePercent;
                string status = bat.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online ? "⚡" : "🔋";
                parts.Add($"BAT {status} {percent * 100:F0}%");
            }
            catch { parts.Add("BAT --"); }
        }

        if (parts.Count == 0)
        {
            _marqueeText.Text = "";
            _currentMarqueeText = "";
            return;
        }

        var combined = string.Join("  •  ", parts);
        combined = "    " + combined + "    ";
        _marqueeText.Text = combined;
        _currentMarqueeText = combined;
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
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                var stats = ni.GetIPv4Statistics();
                totalBytes += stats.BytesSent + stats.BytesReceived;
            }
        }
        catch { }

        var now = DateTime.Now;
        if (_lastNetTime == DateTime.MinValue)
        {
            _lastNetBytesTotal = totalBytes;
            _lastNetTime = now;
            return 0;
        }

        double deltaSec = (now - _lastNetTime).TotalSeconds;
        long deltaBytes = totalBytes - _lastNetBytesTotal;
        _lastNetBytesTotal = totalBytes;
        _lastNetTime = now;

        if (deltaSec > 0 && deltaBytes >= 0) return deltaBytes / deltaSec;
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
