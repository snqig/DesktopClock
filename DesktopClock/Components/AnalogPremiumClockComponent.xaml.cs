using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using DesktopClock.Services;

namespace DesktopClock.Components;

public partial class AnalogPremiumClockComponent : UserControl, IClockComponent
{
    public string Id => "analog_premium_clock";
    public string DisplayName => "超精美模拟时钟";
    public FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    private bool _built;
    private bool _use24Hour = true;
    private bool _showSeconds = true;
    private readonly DispatcherTimer _smoothTimer;

    public AnalogPremiumClockComponent()
    {
        InitializeComponent();
        // 高频 timer 让秒针含毫秒平滑(机械连续)
        _smoothTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _smoothTimer.Tick += (_, _) => UpdateHands();
    }

    public void Update(DateTime now)
    {
        if (!_built) Build();
        UpdateHands();
        UpdateDigital(now);
    }

    public void ApplyConfig()
    {
        var s = SettingsProvider.Instance.Settings;
        _use24Hour = s.Use24Hour;
        _showSeconds = s.ShowSeconds;
    }

    private void Build()
    {
        if (_built) return;
        _built = true;
        ApplyConfig();
        BuildNumbers();
        BuildOrbitDots();
        StartAnimations();
        _smoothTimer.Start();
    }

    // 12 个数字按圆周定位(圆心 180,180,半径 147)
    private void BuildNumbers()
    {
        double cx = 180, cy = 180, r = 147;
        string[] week = { "12", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11" };
        for (int i = 0; i < 12; i++)
        {
            double angle = i * 30 * Math.PI / 180.0;
            double x = cx + r * Math.Sin(angle);
            double y = cy - r * Math.Cos(angle);

            bool isTop = i == 0;
            var tb = new TextBlock
            {
                Text = week[i],
                FontSize = isTop ? 16 : 15,
                FontWeight = isTop ? FontWeights.Bold : FontWeights.Medium,
                Foreground = isTop
                    ? new SolidColorBrush(Color.FromArgb(0x8C, 0xFF, 0xD7, 0x96))
                    : new SolidColorBrush(Color.FromArgb(0x47, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double w = tb.DesiredSize.Width, h = tb.DesiredSize.Height;
            Canvas.SetLeft(tb, x - w / 2);
            Canvas.SetTop(tb, y - h / 2);
            Numbers.Children.Add(tb);
        }
    }

    // 8 个装饰轨道点(半径 174,均匀分布在 45° 间隔)
    private void BuildOrbitDots()
    {
        double cx = 180, cy = 180, r = 174;
        for (int i = 0; i < 8; i++)
        {
            double angle = i * 45 * Math.PI / 180.0;
            double x = cx + r * Math.Sin(angle) - 2;
            double y = cy - r * Math.Cos(angle) - 2;
            var dot = new Ellipse
            {
                Width = 4,
                Height = 4,
                Opacity = 0.25,
                Fill = new RadialGradientBrush(
                    Color.FromRgb(0x8a, 0xa0, 0xff),
                    Color.FromRgb(0xfc, 0x5c, 0x7d)),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 10, ShadowDepth = 0,
                    Color = Color.FromRgb(0x6a, 0x82, 0xfb), Opacity = 0.2
                }
            };
            Canvas.SetLeft(dot, x);
            Canvas.SetTop(dot, y);
            Orbit.Children.Add(dot);
        }
    }

    private void StartAnimations()
    {
        // 光环 18s 一周
        var haloAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(18))
        { RepeatBehavior = RepeatBehavior.Forever };
        HaloRotate.BeginAnimation(RotateTransform.AngleProperty, haloAnim);

        // 装饰轨道点 30s 一周
        var orbitAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(30))
        { RepeatBehavior = RepeatBehavior.Forever };
        OrbitRotate.BeginAnimation(RotateTransform.AngleProperty, orbitAnim);
    }

    // 指针角度(含毫秒平滑)
    private void UpdateHands()
    {
        var now = DateTime.Now;
        double s = now.Second + now.Millisecond / 1000.0;
        double m = now.Minute + now.Second / 60.0;
        double h = (now.Hour % 12) + now.Minute / 60.0;

        SecondRotate.Angle = s * 6;
        MinuteRotate.Angle = m * 6;
        HourRotate.Angle = h * 30;
    }

    private void UpdateDigital(DateTime now)
    {
        int hour = _use24Hour ? now.Hour : (now.Hour % 12 == 0 ? 12 : now.Hour % 12);
        string secPart = _showSeconds ? $":{now.Second:D2}" : "";
        TimeText.Text = $"{hour:D2}:{now.Minute:D2}{secPart}";

        string[] week = { "日", "一", "二", "三", "四", "五", "六" };
        DateText.Text = $"{now.Year}年{now.Month:D2}月{now.Day:D2}日 周{week[(int)now.DayOfWeek]}";
    }
}
