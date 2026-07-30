using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using DesktopClock.Services;

namespace DesktopClock.Components;

public partial class MechanicalClockComponent : UserControl, IClockComponent
{
    public string Id => "mechanical_clock";
    public string DisplayName => "机械时钟";
    public FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    private bool _built;
    private bool _use24Hour = true;
    private bool _showSeconds = true;
    private readonly DispatcherTimer _smoothTimer;

    public MechanicalClockComponent()
    {
        InitializeComponent();
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
        BuildGearTeeth();
        BuildRivets();
        BuildTicks();
        BuildSmallGears();
        StartAnimations();
        _smoothTimer.Start();
    }

    // 外圈齿轮齿(36 个,每 10°一个)
    private void BuildGearTeeth()
    {
        double cx = 200, cy = 200, r = 184;
        for (int i = 0; i < 36; i++)
        {
            double angle = i * 10 * Math.PI / 180.0;
            double x = cx + r * Math.Sin(angle);
            double y = cy - r * Math.Cos(angle);

            var tooth = new Rectangle
            {
                Width = 12,
                Height = 16,
                RadiusX = 3,
                RadiusY = 3,
                Fill = new LinearGradientBrush(
                    Color.FromRgb(0x8a, 0x7a, 0x5a),
                    Color.FromRgb(0x5a, 0x4a, 0x3a),
                    90),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 4, ShadowDepth = 1, Color = Black, Opacity = 0.5
                }
            };
            // 齿绕中心旋转到对应角度
            var rotate = new RotateTransform(i * 10, 6, 16);
            tooth.RenderTransform = rotate;
            Canvas.SetLeft(tooth, x - 6);
            Canvas.SetTop(tooth, y - 16);
            GearOuter.Children.Add(tooth);
        }
    }

    // 8 个铆钉(4 大 + 4 小)
    private void BuildRivets()
    {
        // 4 个大铆钉(12/3/6/9 点位置,半径 172)
        double cx = 200, cy = 200, rBig = 172;
        for (int i = 0; i < 4; i++)
        {
            double angle = i * 90 * Math.PI / 180.0;
            double x = cx + rBig * Math.Sin(angle);
            double y = cy - rBig * Math.Cos(angle);
            AddRivet(x, y, 8);
        }
        // 4 个小铆钉(45° 位置,半径 160)
        double rSmall = 160;
        for (int i = 0; i < 4; i++)
        {
            double angle = (45 + i * 90) * Math.PI / 180.0;
            double x = cx + rSmall * Math.Sin(angle);
            double y = cy - rSmall * Math.Cos(angle);
            AddRivet(x, y, 6);
        }
    }

    private void AddRivet(double x, double y, double size)
    {
        var rivet = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new RadialGradientBrush(
                Color.FromRgb(0xd4, 0xc4, 0xa0),
                Color.FromRgb(0x6a, 0x5a, 0x4a)),
            Effect = new DropShadowEffect
            {
                BlurRadius = 4, ShadowDepth = 0, Color = Black, Opacity = 0.6
            }
        };
        Canvas.SetLeft(rivet, x - size / 2);
        Canvas.SetTop(rivet, y - size / 2);
        Rivets.Children.Add(rivet);
    }

    // 60 个刻度(每 5 个一个大刻度)
    private void BuildTicks()
    {
        double cx = 200, cy = 200;
        double rOuter = 168, rMinor = 152, rMajor = 148;
        for (int i = 0; i < 60; i++)
        {
            bool major = i % 5 == 0;
            double angle = i * 6 * Math.PI / 180.0;
            double rOuter2 = major ? rMajor : rMinor;
            double x1 = cx + rOuter * Math.Sin(angle);
            double y1 = cy - rOuter * Math.Cos(angle);
            double x2 = cx + rOuter2 * Math.Sin(angle);
            double y2 = cy - rOuter2 * Math.Cos(angle);

            var tick = new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                StrokeThickness = major ? 3 : 2,
                Stroke = new SolidColorBrush(major
                    ? Color.FromArgb(0x8C, 0, 0, 0)
                    : Color.FromArgb(0x59, 0, 0, 0)),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            Ticks.Children.Add(tick);
        }
    }

    // 3 个小齿轮装饰
    private void BuildSmallGears()
    {
        BuildSmallGear(SmallGear1, 36, 12, 6);
        BuildSmallGear(SmallGear2, 30, 10, 5);
        BuildSmallGear(SmallGear3, 28, 10, 5);
    }

    private void BuildSmallGear(Canvas container, double size, int toothCount, double toothSize)
    {
        double half = size / 2;
        double r = half - 2;

        // 齿
        for (int i = 0; i < toothCount; i++)
        {
            double angle = i * (360.0 / toothCount) * Math.PI / 180.0;
            double x = half + r * Math.Sin(angle);
            double y = half - r * Math.Cos(angle);
            var tooth = new Rectangle
            {
                Width = toothSize,
                Height = toothSize,
                RadiusX = 1,
                RadiusY = 1,
                Fill = new RadialGradientBrush(
                    Color.FromRgb(0x8a, 0x7a, 0x5a),
                    Color.FromRgb(0x4a, 0x3a, 0x2a))
            };
            tooth.RenderTransform = new RotateTransform(i * (360.0 / toothCount), toothSize / 2, toothSize);
            Canvas.SetLeft(tooth, x - toothSize / 2);
            Canvas.SetTop(tooth, y - toothSize);
            container.Children.Add(tooth);
        }

        // 齿轮主体
        var body = new Ellipse
        {
            Width = size - 4,
            Height = size - 4,
            Fill = new RadialGradientBrush(
                Color.FromRgb(0x8a, 0x7a, 0x5a),
                Color.FromRgb(0x4a, 0x3a, 0x2a)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x5a, 0x4a, 0x3a)),
            StrokeThickness = 3,
            Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 0, Color = Black, Opacity = 0.3 }
        };
        Canvas.SetLeft(body, 2);
        Canvas.SetTop(body, 2);
        container.Children.Add(body);

        // 虚线内环
        var inner = new Ellipse
        {
            Width = size - 14,
            Height = size - 14,
            Stroke = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xD7, 0x8C)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 2, 2 }
        };
        Canvas.SetLeft(inner, 7);
        Canvas.SetTop(inner, 7);
        container.Children.Add(inner);

        // 中心
        var center = new Ellipse
        {
            Width = size - 24,
            Height = size - 24,
            Fill = new RadialGradientBrush(
                Color.FromRgb(0x5a, 0x4a, 0x3a),
                Color.FromRgb(0x2a, 0x2a, 0x2a)),
            Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 0, Color = Black, Opacity = 0.5 }
        };
        Canvas.SetLeft(center, 12);
        Canvas.SetTop(center, 12);
        container.Children.Add(center);
    }

    private void StartAnimations()
    {
        // 外圈齿轮 20s 一周
        var gearAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(20))
        { RepeatBehavior = RepeatBehavior.Forever };
        GearOuterRotate.BeginAnimation(RotateTransform.AngleProperty, gearAnim);

        // 小齿轮 1:6s 正向
        var sg1 = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(6))
        { RepeatBehavior = RepeatBehavior.Forever };
        SmallGear1Rotate.BeginAnimation(RotateTransform.AngleProperty, sg1);

        // 小齿轮 2:10s 反向
        var sg2 = new DoubleAnimation(0, -360, TimeSpan.FromSeconds(10))
        { RepeatBehavior = RepeatBehavior.Forever };
        SmallGear2Rotate.BeginAnimation(RotateTransform.AngleProperty, sg2);

        // 小齿轮 3:7s 正向
        var sg3 = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(7))
        { RepeatBehavior = RepeatBehavior.Forever };
        SmallGear3Rotate.BeginAnimation(RotateTransform.AngleProperty, sg3);
    }

    // 指针角度(含毫秒平滑)
    private void UpdateHands()
    {
        var now = DateTime.Now;
        double s = now.Second + now.Millisecond / 1000.0;
        double m = now.Minute + now.Second / 60.0;
        double h = (now.Hour % 12) + now.Minute / 60.0 + now.Second / 3600.0;

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

    private static Color Black => Colors.Black;
}
