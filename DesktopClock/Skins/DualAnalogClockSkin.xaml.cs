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

public partial class DualAnalogClockSkin : UserControl, IClockSkin
{
    public string Id => "dual_analog_clock_skin";
    public string DisplayName => "双时区指针";
    public FrameworkElement View => this;

    private readonly Dictionary<string, object> _config = new();

    private Color HourColor { get; set; } = Color.FromRgb(0x3a, 0x2a, 0x1a);
    private Color MinuteColor { get; set; } = Color.FromRgb(0x2a, 0x2a, 0x2a);
    private Color SecondColor { get; set; } = Color.FromRgb(0xcc, 0x33, 0x33);
    private double HandThickness { get; set; } = 1.0;
    private bool ShowSecondHand { get; set; } = true;
    private bool ShowTicks { get; set; } = true;
    private bool ShowCenterDot { get; set; } = true;
    private string DialImagePath { get; set; } = string.Empty;
    private string SecondTimeZone { get; set; } = "Eastern Standard Time";
    private string SecondLabel { get; set; } = "纽约";
    private readonly DispatcherTimer _smoothTimer;

    public DualAnalogClockSkin()
    {
        InitializeComponent();
        BuildTicks(LocalTicksCanvas, 95, 95, 90);
        BuildTicks(RemoteTicksCanvas, 95, 95, 90);
        EnsureDefaultDialImage();

        _smoothTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _smoothTimer.Tick += (_, _) => UpdateSmoothHands();
        _smoothTimer.Start();

        this.Unloaded += (_, _) => _smoothTimer.Stop();
    }

    public void UpdateTime(DateTime now)
    {
        UpdateSmoothHands();
    }

    private void UpdateSmoothHands()
    {
        var now = DateTime.Now;
        double ms = now.Millisecond / 1000.0;
        double sec = now.Second + ms;
        double min = now.Minute + sec / 60.0;
        double hour = (now.Hour % 12) + min / 60.0;

        LocalHourRotate.Angle = hour * 30.0;
        LocalMinuteRotate.Angle = min * 6.0;
        LocalSecondRotate.Angle = sec * 6.0;

        DateTime remoteNow;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(SecondTimeZone);
            remoteNow = TimeZoneInfo.ConvertTime(now, tz);
        }
        catch
        {
            remoteNow = now;
        }

        double rsec = remoteNow.Second + ms;
        double rmin = remoteNow.Minute + rsec / 60.0;
        double rhour = (remoteNow.Hour % 12) + rmin / 60.0;

        RemoteHourRotate.Angle = rhour * 30.0;
        RemoteMinuteRotate.Angle = rmin * 6.0;
        RemoteSecondRotate.Angle = rsec * 6.0;
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
        if (TryString("dialImage", out var img)) DialImagePath = img ?? string.Empty;
        if (TryString("secondTimeZone", out var tz)) SecondTimeZone = tz ?? "Eastern Standard Time";
        if (TryString("secondLabel", out var lbl)) SecondLabel = lbl ?? "纽约";

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
        ["dialImage"] = DialImagePath,
        ["secondTimeZone"] = SecondTimeZone,
        ["secondLabel"] = SecondLabel
    };

    private void ApplyVisual()
    {
        LocalHourHand.Stroke = new SolidColorBrush(HourColor);
        LocalHourHand.StrokeThickness = 5 * HandThickness;
        LocalMinuteHand.Stroke = new SolidColorBrush(MinuteColor);
        LocalMinuteHand.StrokeThickness = 3.5 * HandThickness;
        LocalSecondHand.Stroke = new SolidColorBrush(SecondColor);
        LocalSecondHand.StrokeThickness = 1.5 * HandThickness;
        LocalSecondHand.Visibility = ShowSecondHand ? Visibility.Visible : Visibility.Collapsed;
        LocalCenterDot.Visibility = ShowCenterDot ? Visibility.Visible : Visibility.Collapsed;
        LocalTicksCanvas.Visibility = ShowTicks ? Visibility.Visible : Visibility.Collapsed;

        RemoteHourHand.Stroke = new SolidColorBrush(HourColor);
        RemoteHourHand.StrokeThickness = 5 * HandThickness;
        RemoteMinuteHand.Stroke = new SolidColorBrush(MinuteColor);
        RemoteMinuteHand.StrokeThickness = 3.5 * HandThickness;
        RemoteSecondHand.Stroke = new SolidColorBrush(SecondColor);
        RemoteSecondHand.StrokeThickness = 1.5 * HandThickness;
        RemoteSecondHand.Visibility = ShowSecondHand ? Visibility.Visible : Visibility.Collapsed;
        RemoteCenterDot.Visibility = ShowCenterDot ? Visibility.Visible : Visibility.Collapsed;
        RemoteTicksCanvas.Visibility = ShowTicks ? Visibility.Visible : Visibility.Collapsed;

        RemoteLabel.Text = SecondLabel;

        LoadDialImages();
    }

    private void LoadDialImages()
    {
        string path = DialImagePath;
        if (string.IsNullOrWhiteSpace(path))
            path = DefaultDialPath;

        string fullPath = System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(AppContext.BaseDirectory, path);
        if (File.Exists(fullPath))
        {
            try
            {
                var bmp = new BitmapImage(new Uri(fullPath, UriKind.Absolute));
                LocalDialImage.Source = bmp;
                RemoteDialImage.Source = bmp;
            }
            catch
            {
                LocalDialImage.Source = null;
                RemoteDialImage.Source = null;
            }
        }
        else
        {
            LocalDialImage.Source = null;
            RemoteDialImage.Source = null;
        }
    }

    private static void BuildTicks(Canvas canvas, double cx, double cy, double rOuter)
    {
        canvas.Children.Clear();
        double rInner = rOuter - 10;
        for (int i = 0; i < 60; i++)
        {
            bool major = i % 5 == 0;
            double angle = i * 6.0 * Math.PI / 180.0;
            double rIn = major ? rInner - 4 : rInner;
            double x1 = cx + rOuter * Math.Sin(angle);
            double y1 = cy - rOuter * Math.Cos(angle);
            double x2 = cx + rIn * Math.Sin(angle);
            double y2 = cy - rIn * Math.Cos(angle);

            var line = new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = Brushes.Gray,
                StrokeThickness = major ? 2.5 : 1.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = major ? 0.6 : 0.35
            };
            canvas.Children.Add(line);
        }
    }

    #region 默认透明底图

    private const string EmbeddedDialResourceName = "DesktopClock.Skins.1.png";

    private static string DefaultDialPath
    {
        get
        {
            try
            {
                var assembly = typeof(DualAnalogClockSkin).Assembly;
                using (var stream = assembly.GetManifestResourceStream(EmbeddedDialResourceName))
                {
                    if (stream != null && stream.Length > 0)
                    {
                        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopClock");
                        Directory.CreateDirectory(tempDir);
                        var tempPath = System.IO.Path.Combine(tempDir, "default_dial.png");
                        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                        {
                            stream.CopyTo(fs);
                        }
                        return tempPath;
                    }
                }
            }
            catch { }
            return System.IO.Path.Combine(AppContext.BaseDirectory, "Skins", "default_dial.png");
        }
    }

    private static void EnsureDefaultDialImage()
    {
        try
        {
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopClock");
            Directory.CreateDirectory(tempDir);
            var tempPath = System.IO.Path.Combine(tempDir, "default_dial.png");

            if (File.Exists(tempPath))
            {
                try { var fi = new FileInfo(tempPath); if (fi.Length > 0) return; } catch { }
            }

            var assembly = typeof(DualAnalogClockSkin).Assembly;
            using (var stream = assembly.GetManifestResourceStream(EmbeddedDialResourceName))
            {
                if (stream != null && stream.Length > 0)
                {
                    var tmpFile = tempPath + ".tmp_" + Guid.NewGuid().ToString("N");
                    using (var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        stream.CopyTo(fs);
                    }
                    try { File.Move(tmpFile, tempPath, overwrite: true); }
                    catch { try { File.Delete(tmpFile); } catch { } }
                    return;
                }
            }
        }
        catch { }
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
