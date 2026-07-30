using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DesktopClock.Skins;

public partial class RibbonClockSkin : UserControl, IClockSkin
{
    public string Id => "ribbon_clock_skin";
    public string DisplayName => "缎带流光";
    public FrameworkElement View => this;

    private readonly Dictionary<string, object> _config = new();
    private readonly List<Path> _ribbons = new();
    private readonly DispatcherTimer _animTimer;
    private readonly Random _rand = new();

    public RibbonClockSkin()
    {
        InitializeComponent();
        BuildRibbons();

        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _animTimer.Tick += (_, _) => AnimateRibbons();
        _animTimer.Start();
        this.Unloaded += (_, _) => _animTimer.Stop();
    }

    private void BuildRibbons()
    {
        const int count = 6;
        for (int i = 0; i < count; i++)
        {
            var path = new Path
            {
                Stroke = new SolidColorBrush(Color.FromArgb(120, (byte)(100 + _rand.Next(155)), 150, 255)),
                StrokeThickness = 2 + _rand.NextDouble() * 2,
                Opacity = 0.4 + _rand.NextDouble() * 0.4,
                Data = new PathGeometry(),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            _ribbons.Add(path);
            RibbonCanvas.Children.Insert(0, path);
        }
    }

    private double _phase;
    private void AnimateRibbons()
    {
        _phase += 0.02;
        const double cx = 200, cy = 200;
        for (int i = 0; i < _ribbons.Count; i++)
        {
            var pg = new PathGeometry();
            var pf = new PathFigure { StartPoint = new Point(cx, cy) };
            double radius = 60 + i * 25;
            double speed = 1 + i * 0.3;
            double amp = 10 + i * 5;
            for (double a = 0; a <= Math.PI * 2; a += 0.15)
            {
                double r = radius + Math.Sin(a * 3 + _phase * speed) * amp;
                double x = cx + Math.Cos(a + _phase * 0.5 * (i % 2 == 0 ? 1 : -1)) * r;
                double y = cy + Math.Sin(a + _phase * 0.5 * (i % 2 == 0 ? 1 : -1)) * r;
                if (a == 0) pf.StartPoint = new Point(x, y);
                else pf.Segments.Add(new LineSegment(new Point(x, y), true));
            }
            pf.IsClosed = true;
            pg.Figures.Add(pf);
            _ribbons[i].Data = pg;

            // 呼吸效果: 周期性改变透明度
            double breath = 0.5 + 0.5 * Math.Sin(_phase * 2 + i);
            _ribbons[i].Opacity = 0.2 + breath * 0.5;
        }

        // 中心光晕呼吸
        var glowScale = 1.0 + 0.1 * Math.Sin(_phase * 3);
        CenterGlow.RenderTransform = new ScaleTransform(glowScale, glowScale);
    }

    public void UpdateTime(DateTime now)
    {
        TimeText.Text = now.ToString("HH:mm:ss");
    }

    public void LoadConfig(Dictionary<string, object> config)
    {
        _config.Clear();
        foreach (var kv in config) _config[kv.Key] = kv.Value;
    }

    public Dictionary<string, object> SaveConfig() => new(_config);
}
