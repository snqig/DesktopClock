using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DesktopClock.Skins;

public partial class CyberpunkNeonSkin : UserControl, IClockSkin
{
    public string Id => "cyberpunk_neon_clock_skin";
    public string DisplayName => "赛博朋克霓虹发光指针";
    public FrameworkElement View => this;

    // === 配置项(与 指针表盘一致,支持显隐外边框/刻度/秒针/中心点) ===
    private readonly Dictionary<string, object> _config = new();
    private bool ShowOuterRing { get; set; } = true;
    private bool ShowOuterRing2 { get; set; } = true;
    private bool ShowOuterRing3 { get; set; } = true;
    private bool ShowTicks { get; set; } = true;
    private bool ShowNumbers { get; set; } = true;
    private bool ShowSecondHand { get; set; } = true;
    private bool ShowCenterDot { get; set; } = true;

    // 主颜色(可自定义,默认霓虹色板)
    private Color HourColor { get; set; } = Color.FromRgb(0x00, 0xff, 0xff);
    private Color MinuteColor { get; set; } = Color.FromRgb(0xff, 0x2b, 0xd6);
    private Color SecondColor { get; set; } = Color.FromRgb(0xff, 0xa3, 0x00);
    private Color OuterRingColor { get; set; } = Color.FromRgb(0x00, 0xff, 0xff);
    private Color TickColor { get; set; } = Color.FromRgb(0x7b, 0xff, 0xff);
    private Color NumberColor { get; set; } = Color.FromRgb(0x00, 0xf0, 0xff);

    private readonly DispatcherTimer _smoothTimer;

    public CyberpunkNeonSkin()
    {
        InitializeComponent();
        BuildTicks();
        BuildNumbers();

        _smoothTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) }; // 更丝滑的毫秒级
        _smoothTimer.Tick += (_, _) => UpdateSmoothHands();
        _smoothTimer.Start();

        this.Loaded += (_, _) =>
        {
            if (TryFindResource("CenterBreath") is Storyboard sb)
                sb.Begin();
            ApplyConfigVisual();
        };

        this.Unloaded += (_, _) => _smoothTimer.Stop();
    }

    // === 时间驱动(高频timer) ===
    public void UpdateTime(DateTime now) => UpdateSmoothHands();

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

    // === 构建刻度(冷光霓虹,整点加粗) - 画布 340×340,中心(170,170),外边框半径 168 ===
    private void BuildTicks()
    {
        TicksLayer.Children.Clear();
        for (int i = 0; i < 60; i++)
        {
            double angle = i * 6; // 每 6° 一个刻度
            bool isHour = i % 5 == 0;
            // 刻度落在外边框(半径168)内侧:outerR=160,innerR=148/154
            double innerR = isHour ? 148 : 154;
            double outerR = 160;
            const double cx = 170, cy = 170;

            double rad = angle * Math.PI / 180.0;
            double x1 = cx + innerR * Math.Sin(rad);
            double y1 = cy - innerR * Math.Cos(rad);
            double x2 = cx + outerR * Math.Sin(rad);
            double y2 = cy - outerR * Math.Cos(rad);

            var line = new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = new SolidColorBrush(TickColor),
                StrokeThickness = isHour ? 3.0 : 1.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = isHour ? 0.95 : 0.55,
                Effect = new DropShadowEffect
                {
                    Color = TickColor,
                    BlurRadius = isHour ? 10 : 5,
                    ShadowDepth = 0,
                    Opacity = 0.85
                }
            };
            TicksLayer.Children.Add(line);
        }
    }

    // === 构建数字(1~12) - 画布 340×340,中心(170,170),刻度内侧 r=128 ===
    private void BuildNumbers()
    {
        NumbersLayer.Children.Clear();
        const double r = 128;
        const double cx = 170, cy = 170;
        int[] markers = { 12, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        for (int i = 0; i < markers.Length; i++)
        {
            int n = markers[i];
            double angle = i * 30 * Math.PI / 180.0;
            double x = cx + r * Math.Sin(angle);
            double y = cy - r * Math.Cos(angle);
            var tb = new TextBlock
            {
                Text = n.ToString(),
                Foreground = new SolidColorBrush(NumberColor),
                FontFamily = new FontFamily("Orbitron, Consolas, Microsoft YaHei"),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Effect = new DropShadowEffect
                {
                    Color = NumberColor,
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.95
                }
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = tb.DesiredSize;
            Canvas.SetLeft(tb, x - size.Width / 2);
            Canvas.SetTop(tb, y - size.Height / 2);
            NumbersLayer.Children.Add(tb);
        }
    }

    // === 配置接口 ===
    public void LoadConfig(Dictionary<string, object> config)
    {
        _config.Clear();
        if (config != null) foreach (var kv in config) _config[kv.Key] = kv.Value;

        if (TryBool("showOuterRing", out var o1)) ShowOuterRing = o1;
        if (TryBool("showOuterRing2", out var o2)) ShowOuterRing2 = o2;
        if (TryBool("showOuterRing3", out var o3)) ShowOuterRing3 = o3;
        if (TryBool("showTicks", out var st)) ShowTicks = st;
        if (TryBool("showNumbers", out var sn)) ShowNumbers = sn;
        if (TryBool("showSecondHand", out var ss)) ShowSecondHand = ss;
        if (TryBool("showCenterDot", out var cd)) ShowCenterDot = cd;

        if (TryColor("hourColor", out var hc)) HourColor = hc;
        if (TryColor("minuteColor", out var mc)) MinuteColor = mc;
        if (TryColor("secondColor", out var sc)) SecondColor = sc;
        if (TryColor("outerRingColor", out var orc)) OuterRingColor = orc;
        if (TryColor("tickColor", out var tc)) TickColor = tc;
        if (TryColor("numberColor", out var nc)) NumberColor = nc;

        if (IsLoaded) ApplyConfigVisual();
    }

    public Dictionary<string, object> SaveConfig() => new(_config)
    {
        ["showOuterRing"] = ShowOuterRing,
        ["showOuterRing2"] = ShowOuterRing2,
        ["showOuterRing3"] = ShowOuterRing3,
        ["showTicks"] = ShowTicks,
        ["showNumbers"] = ShowNumbers,
        ["showSecondHand"] = ShowSecondHand,
        ["showCenterDot"] = ShowCenterDot,
        ["hourColor"] = ColorToHex(HourColor),
        ["minuteColor"] = ColorToHex(MinuteColor),
        ["secondColor"] = ColorToHex(SecondColor),
        ["outerRingColor"] = ColorToHex(OuterRingColor),
        ["tickColor"] = ColorToHex(TickColor),
        ["numberColor"] = ColorToHex(NumberColor),
    };

    // === 把配置反映到视觉 ===
    private void ApplyConfigVisual()
    {
        // 可见性
        OuterRing.Visibility = ShowOuterRing ? Visibility.Visible : Visibility.Collapsed;
        OuterRing2.Visibility = ShowOuterRing2 ? Visibility.Visible : Visibility.Collapsed;
        OuterRing3.Visibility = ShowOuterRing3 ? Visibility.Visible : Visibility.Collapsed;
        TicksLayer.Visibility = ShowTicks ? Visibility.Visible : Visibility.Collapsed;
        NumbersLayer.Visibility = ShowNumbers ? Visibility.Visible : Visibility.Collapsed;
        SecondHand.Visibility = ShowSecondHand ? Visibility.Visible : Visibility.Collapsed;
        CenterGlow.Visibility = CenterGlowInner.Visibility = CenterDot.Visibility =
            ShowCenterDot ? Visibility.Visible : Visibility.Collapsed;

        // 指针颜色(带动画效果)
        ApplyHandColor(HourHand, HourColor);
        ApplyHandColor(MinuteHand, MinuteColor);
        ApplyHandColor(SecondHand, SecondColor);

        // 外边框颜色
        var mainBrush = new SolidColorBrush(OuterRingColor);
        OuterRing.Stroke = mainBrush;
        OuterRing.Effect = new DropShadowEffect
        {
            Color = OuterRingColor,
            BlurRadius = 22,
            ShadowDepth = 0,
            Opacity = 0.95
        };

        // 刻度与数字(重绘)
        BuildTicks();
        BuildNumbers();
    }

    private static void ApplyHandColor(Line hand, Color color)
    {
        hand.Stroke = new SolidColorBrush(color);
        hand.Effect = new DropShadowEffect
        {
            Color = color,
            BlurRadius = 16,
            ShadowDepth = 0,
            Opacity = 0.9
        };
    }

    // === 辅助方法 ===
    private static string ColorToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    private bool TryBool(string key, out bool v)
    {
        v = default;
        if (!_config.TryGetValue(key, out var o)) return false;
        if (o is bool b) { v = b; return true; }
        if (bool.TryParse(o?.ToString(), out var r)) { v = r; return true; }
        return false;
    }

    private bool TryColor(string key, out Color c)
    {
        c = default;
        if (!_config.TryGetValue(key, out var o) || o == null) return false;
        try
        {
            c = (Color)ColorConverter.ConvertFromString(o.ToString()!);
            return true;
        }
        catch { return false; }
    }
}
