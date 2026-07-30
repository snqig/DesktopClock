using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using DesktopClock.Services;

namespace DesktopClock.Components;

public partial class AnalogClockComponent : UserControl, IClockComponent
{
    public string Id => "analog_clock";
    public string DisplayName => "模拟时钟";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    private bool _built;
    private bool _use24Hour = true;
    private bool _showSeconds = true;

    public AnalogClockComponent()
    {
        InitializeComponent();
        // 不依赖 Loaded,Update 首次调用时构建(此时元素已在可视树中)
    }

    public void Update(DateTime now)
    {
        if (!_built) BuildAndAnimate();
        _built = true;

        int hour = _use24Hour ? now.Hour : (now.Hour % 12 == 0 ? 12 : now.Hour % 12);
        HmText.Text = $"{hour:D2}:{now.Minute:D2}";
        if (_showSeconds)
        {
            SecText.Text = now.Second.ToString("D2");
            SecText.Visibility = System.Windows.Visibility.Visible;
        }
        else
        {
            SecText.Visibility = System.Windows.Visibility.Collapsed;
        }
        DateText.Text = $"{now.Year}年{now.Month:D2}月{now.Day:D2}日";
    }

    public void ApplyConfig()
    {
        var s = SettingsProvider.Instance.Settings;
        _use24Hour = s.Use24Hour;
        _showSeconds = s.ShowSeconds;
    }

    private void BuildAndAnimate()
    {
        ApplyConfig();

        // 轨道 1:12 个点,半径 144,均匀分布
        // 对应 HTML 中 12 个 .dot 位置(按 12 个钟点位置)
        var orbit1Positions = new (double angleDeg, double size, double opacity)[]
        {
            (0,   8, 0.5),
            (30,  8, 0.5),
            (60,  8, 0.5),
            (90,  8, 0.5),
            (120, 8, 0.5),
            (150, 8, 0.5),
            (180, 8, 0.5),
            (210, 8, 0.5),
            (240, 8, 0.5),
            (270, 8, 0.5),
            (300, 5, 0.3),
            (330, 5, 0.3),
        };
        double r1 = 144;
        double cx1 = 160, cy1 = 160;
        foreach (var (angle, size, opacity) in orbit1Positions)
        {
            double rad = (angle - 90) * Math.PI / 180.0;
            double x = cx1 + r1 * Math.Cos(rad) - size / 2;
            double y = cy1 + r1 * Math.Sin(rad) - size / 2;
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Opacity = opacity,
                Fill = new RadialGradientBrush(
                    Color.FromRgb(0xfc, 0x5c, 0x7d),
                    Color.FromRgb(0x6a, 0x82, 0xfb)),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 15, ShadowDepth = 0,
                    Color = Color.FromRgb(0x6a, 0x82, 0xfb), Opacity = 0.4
                }
            };
            Canvas.SetLeft(dot, x);
            Canvas.SetTop(dot, y);
            Orbit1.Children.Add(dot);
        }

        // 轨道 2:4 个点,半径 108,反向旋转
        double r2 = 108;
        double cx2 = 112, cy2 = 112;
        for (int i = 0; i < 4; i++)
        {
            double angle = i * 90;
            double rad = (angle - 90) * Math.PI / 180.0;
            double x = cx2 + r2 * Math.Cos(rad) - 2;
            double y = cy2 + r2 * Math.Sin(rad) - 2;
            var dot = new Ellipse
            {
                Width = 4,
                Height = 4,
                Opacity = 0.3,
                Fill = new SolidColorBrush(Color.FromRgb(0xf0, 0xc2, 0x7f)),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 10, ShadowDepth = 0,
                    Color = Color.FromRgb(0xf0, 0xc2, 0x7f), Opacity = 0.3
                }
            };
            Canvas.SetLeft(dot, x);
            Canvas.SetTop(dot, y);
            Orbit2.Children.Add(dot);
        }

        // 动画:外圈光晕 10s 一周
        var haloAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(10))
        { RepeatBehavior = RepeatBehavior.Forever };
        HaloRotate.BeginAnimation(RotateTransform.AngleProperty, haloAnim);

        // 轨道 1:20s 一周(正向)
        var orbit1Anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(20))
        { RepeatBehavior = RepeatBehavior.Forever };
        Orbit1Rotate.BeginAnimation(RotateTransform.AngleProperty, orbit1Anim);

        // 轨道 2:30s 一周(反向)
        var orbit2Anim = new DoubleAnimation(0, -360, TimeSpan.FromSeconds(30))
        { RepeatBehavior = RepeatBehavior.Forever };
        Orbit2Rotate.BeginAnimation(RotateTransform.AngleProperty, orbit2Anim);

        // 脉冲发光:主圆盘 DropShadow 的 BlurRadius/Opacity 在 4s 内往返
        var blurAnim = new DoubleAnimation(60, 80, TimeSpan.FromSeconds(2))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        DiscGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, blurAnim);
    }
}
