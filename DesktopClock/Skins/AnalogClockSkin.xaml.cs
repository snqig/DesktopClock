using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DesktopClock.Skins;

public partial class AnalogClockSkin : UserControl, IClockSkin
{
    public string Id => "analog_clock_skin";
    public string DisplayName => "指针表盘(自定义)";
    public FrameworkElement View => this;

    private readonly Dictionary<string, object> _config = new();

    // 默认配置
    private Color HourColor { get; set; } = Color.FromRgb(0x3a, 0x2a, 0x1a);
    private Color MinuteColor { get; set; } = Color.FromRgb(0x2a, 0x2a, 0x2a);
    private Color SecondColor { get; set; } = Color.FromRgb(0xcc, 0x33, 0x33);
    private double HandThickness { get; set; } = 1.0; // 相对基准的倍数
    private bool ShowSecondHand { get; set; } = true;
    private bool ShowTicks { get; set; } = true;
    private bool ShowCenterDot { get; set; } = true;
    private Color TickColor { get; set; } = Color.FromRgb(0x80, 0x80, 0x80);
    private string DialImagePath { get; set; } = string.Empty;
    private readonly DispatcherTimer _smoothTimer;

    public AnalogClockSkin()
    {
        InitializeComponent();
        EnsureDefaultDialImage();
        BuildTicks();

        _smoothTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _smoothTimer.Tick += (_, _) => UpdateSmoothHands();
        _smoothTimer.Start();

        this.Unloaded += (_, _) => _smoothTimer.Stop();
    }

    public void UpdateTime(DateTime now)
    {
        // 由 SkinHost 每秒调用一次,内部高频 timer 已负责平滑走针,
        // 此处仅做同步兜底,确保时间不会漂移。
        UpdateSmoothHands();
    }

    /// <summary>
    /// 使用 DateTime.Now 毫秒精度计算指针角度,实现平滑连续走针。
    /// </summary>
    private void UpdateSmoothHands()
    {
        var now = DateTime.Now;
        double ms = now.Millisecond / 1000.0;
        double sec = now.Second + ms;
        double min = now.Minute + sec / 60.0;
        double hour = (now.Hour % 12) + min / 60.0;

        HourRotate.Angle = hour * 30.0;
        MinuteRotate.Angle = min * 6.0;
        SecondRotate.Angle = sec * 6.0;
    }

    public void LoadConfig(Dictionary<string, object> config)
    {
        _config.Clear();
        foreach (var kv in config) _config[kv.Key] = kv.Value;

        if (TryColor("hourColor", out var hc)) HourColor = hc;
        if (TryColor("minuteColor", out var mc)) MinuteColor = mc;
        if (TryColor("secondColor", out var sc)) SecondColor = sc;
        if (TryDouble("handThickness", out var th)) HandThickness = Math.Max(0.3, Math.Min(3.0, th));
        if (TryBool("showSecondHand", out var ss)) ShowSecondHand = ss;
        if (TryBool("showTicks", out var st)) ShowTicks = st;
        if (TryBool("showCenterDot", out var cd)) ShowCenterDot = cd;
        if (TryColor("tickColor", out var tc)) TickColor = tc;
        if (TryString("dialImage", out var img)) DialImagePath = img ?? string.Empty;

        ApplyVisual();
    }

    public Dictionary<string, object> SaveConfig() => new(_config)
    {
        ["hourColor"] = HourColor.ToString(),
        ["minuteColor"] = MinuteColor.ToString(),
        ["secondColor"] = SecondColor.ToString(),
        ["handThickness"] = HandThickness,
        ["showSecondHand"] = ShowSecondHand,
        ["showTicks"] = ShowTicks,
        ["showCenterDot"] = ShowCenterDot,
        ["tickColor"] = TickColor.ToString(),
        ["dialImage"] = DialImagePath
    };

    private void ApplyVisual()
    {
        HourHand.Stroke = new SolidColorBrush(HourColor);
        MinuteHand.Stroke = new SolidColorBrush(MinuteColor);
        SecondHand.Stroke = new SolidColorBrush(SecondColor);

        HourHand.StrokeThickness = 6 * HandThickness;
        MinuteHand.StrokeThickness = 4 * HandThickness;
        SecondHand.StrokeThickness = 2 * HandThickness;

        SecondHand.Visibility = ShowSecondHand ? Visibility.Visible : Visibility.Collapsed;
        CenterDot.Visibility = ShowCenterDot ? Visibility.Visible : Visibility.Collapsed;
        TicksCanvas.Visibility = ShowTicks ? Visibility.Visible : Visibility.Collapsed;

        // 刻度颜色变化时重建刻度
        BuildTicks();

        LoadDialImage();
    }

    private void LoadDialImage()
    {
        string path = DialImagePath;
        if (string.IsNullOrWhiteSpace(path))
            path = DefaultDialPath;

        string fullPath = System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(AppContext.BaseDirectory, path);
        if (!File.Exists(fullPath))
        {
            DialImage.Source = null;
            return;
        }

        try
        {
            var bmp = new BitmapImage(new Uri(fullPath, UriKind.Absolute));
            DialImage.Source = bmp;
        }
        catch
        {
            DialImage.Source = null;
        }
    }

    private void BuildTicks()
    {
        TicksCanvas.Children.Clear();
        const double cx = 200, cy = 200, rOuter = 190, rInner = 180;
        var tickBrush = new SolidColorBrush(TickColor);
        for (int i = 0; i < 60; i++)
        {
            bool major = i % 5 == 0;
            double angle = i * 6.0 * Math.PI / 180.0;
            double rIn = major ? rInner - 8 : rInner;
            double x1 = cx + rOuter * Math.Sin(angle);
            double y1 = cy - rOuter * Math.Cos(angle);
            double x2 = cx + rIn * Math.Sin(angle);
            double y2 = cy - rIn * Math.Cos(angle);

            var line = new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = tickBrush,
                StrokeThickness = major ? 3 : 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = major ? 0.8 : 0.5
            };
            TicksCanvas.Children.Add(line);
        }
    }

    #region 默认透明底图生成

    // 嵌入资源中的用户提供的默认底图(由 .csproj 中 LogicalName 指定)
    private const string EmbeddedDialResourceName = "DesktopClock.Skins.1.png";

    private static string DefaultDialPath
    {
        get
        {
            // 优先使用嵌入资源中的用户提供图片,提取到临时目录后返回路径
            try
            {
                var assembly = typeof(AnalogClockSkin).Assembly;
                using (var stream = assembly.GetManifestResourceStream(EmbeddedDialResourceName))
                {
                    if (stream != null && stream.Length > 0)
                    {
                        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopClock");
                        Directory.CreateDirectory(tempDir);
                        var tempPath = System.IO.Path.Combine(tempDir, "default_dial.png");
                        // 每次启动覆盖,确保与内置资源一致
                        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                        {
                            stream.CopyTo(fs);
                        }
                        return tempPath;
                    }
                }
            }
            catch
            {
                // 嵌入资源加载失败时回退到动态生成
            }
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "Skins");
            return System.IO.Path.Combine(dir, "default_dial.png");
        }
    }

    private static void EnsureDefaultDialImage()
    {
        try
        {
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopClock");
            Directory.CreateDirectory(tempDir);
            var tempPath = System.IO.Path.Combine(tempDir, "default_dial.png");

            // 若存在且>0字节,直接跳过,避免重复提取导致文件占用 IOException
            if (File.Exists(tempPath))
            {
                try { var fi = new FileInfo(tempPath); if (fi.Length > 0) return; } catch { }
            }

            try
            {
                // 嵌入资源模式: 直接将内置 1.png 提取到临时目录,跳过动态生成
                var assembly = typeof(AnalogClockSkin).Assembly;
                using (var stream = assembly.GetManifestResourceStream(EmbeddedDialResourceName))
                {
                    if (stream != null && stream.Length > 0)
                    {
                        // 先写入临时文件名,再原子替换,避免并发提取冲突
                        var tmpFile = tempPath + ".tmp_" + Guid.NewGuid().ToString("N");
                        using (var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            stream.CopyTo(fs);
                        }
                        try { File.Move(tmpFile, tempPath, overwrite: true); }
                        catch
                        {
                            try { File.Delete(tmpFile); } catch { }
                        }
                        return;
                    }
                }
            }
            catch { }

            // 回退: 动态生成默认底图
            string path = DefaultDialPath;
            if (File.Exists(path))
            {
                try { var fi = new FileInfo(path); if (fi.Length > 0) return; } catch { }
            }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            GenerateDefaultDialPng(path);
        }
        catch { }
    }

    private static void GenerateDefaultDialPng(string path)
    {
        const int size = 400;
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            // 透明背景
            ctx.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, size, size));

            // 外圈细圆环
            var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 0x80, 0x80, 0x80)), 2);
            ctx.DrawEllipse(null, ringPen, new Point(size / 2, size / 2), size / 2 - 4, size / 2 - 4);

            // 刻度
            const double cx = size / 2, cy = size / 2, rOuter = size / 2 - 14, rInner = size / 2 - 24;
            for (int i = 0; i < 60; i++)
            {
                bool major = i % 5 == 0;
                double angle = i * 6.0 * Math.PI / 180.0;
                double rIn = major ? rInner - 8 : rInner;
                double x1 = cx + rOuter * Math.Sin(angle);
                double y1 = cy - rOuter * Math.Cos(angle);
                double x2 = cx + rIn * Math.Sin(angle);
                double y2 = cy - rIn * Math.Cos(angle);
                var pen = new Pen(Brushes.Gray, major ? 3 : 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                ctx.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
            }
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }

    #endregion

    #region 配置解析辅助

    private bool TryColor(string key, out Color color)
    {
        color = default;
        if (!_config.TryGetValue(key, out var v) || v == null) return false;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(v.ToString()!);
            return true;
        }
        catch { return false; }
    }

    private bool TryDouble(string key, out double value)
    {
        value = 0;
        return _config.TryGetValue(key, out var v) && double.TryParse(v?.ToString(), out value);
    }

    private bool TryBool(string key, out bool value)
    {
        value = false;
        return _config.TryGetValue(key, out var v) && bool.TryParse(v?.ToString(), out value);
    }

    private bool TryString(string key, out string? value)
    {
        value = null;
        return _config.TryGetValue(key, out var v) && (value = v?.ToString()) != null;
    }

    #endregion
}
