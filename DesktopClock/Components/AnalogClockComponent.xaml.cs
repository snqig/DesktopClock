using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace DesktopClock.Components;

public partial class AnalogClockComponent : UserControl, IClockComponent
{
    private readonly AppSettings _settings;
    private Rectangle? _hourHand, _minuteHand, _secondHand;
    private bool _built;

    public string Id => "analog_clock";
    public string DisplayName => "模拟时钟";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public AnalogClockComponent(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
    }

    public void Update(DateTime now)
    {
        BuildClock();
        double h = now.Hour % 12, m = now.Minute, s = now.Second;
        if (_hourHand != null) _hourHand.RenderTransform = new RotateTransform((h + m / 60.0 + s / 3600.0) * 30);
        if (_minuteHand != null) _minuteHand.RenderTransform = new RotateTransform((m + s / 60.0) * 6);
        if (_secondHand != null) _secondHand.RenderTransform = new RotateTransform(s * 6);
    }

    private void BuildClock()
    {
        if (_built) return;
        _built = true;

        var canvas = new Canvas { Width = 280, Height = 280, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center };
        double cx = 140, cy = 140;
        var faceBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x35));
        var innerBrush = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x2e));
        var markerBrush = new SolidColorBrush(Color.FromRgb(0x6a, 0x7a, 0x8a));
        var textBrush = new SolidColorBrush(Color.FromRgb(0x8a, 0x9a, 0xaa));

        var outer = new Ellipse { Width = 280, Height = 280, Fill = faceBrush };
        outer.Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 6, Color = Colors.Black, Opacity = 0.5 };
        Canvas.SetLeft(outer, 0); Canvas.SetTop(outer, 0); canvas.Children.Add(outer);

        var inner = new Ellipse { Width = 258, Height = 258, Fill = innerBrush, Stroke = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x45)), StrokeThickness = 1 };
        Canvas.SetLeft(inner, 11); Canvas.SetTop(inner, 11); canvas.Children.Add(inner);

        for (int i = 1; i <= 12; i++)
        {
            double a = i * 30 * Math.PI / 180;
            double mr = 118, nr = 92;
            bool major = i % 3 == 0;
            var dot = new Ellipse { Width = major ? 8 : 5, Height = major ? 8 : 5, Fill = markerBrush };
            Canvas.SetLeft(dot, cx + mr * Math.Sin(a) - dot.Width / 2);
            Canvas.SetTop(dot, cy - mr * Math.Cos(a) - dot.Height / 2);
            canvas.Children.Add(dot);

            var tb = new TextBlock { Text = i.ToString(), FontSize = 16, FontWeight = major ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal, Foreground = textBrush };
            tb.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, cx + nr * Math.Sin(a) - tb.DesiredSize.Width / 2);
            Canvas.SetTop(tb, cy - nr * Math.Cos(a) - tb.DesiredSize.Height / 2);
            canvas.Children.Add(tb);
        }

        _hourHand = new Rectangle { Width = 6, Height = 50, Fill = new SolidColorBrush(Color.FromRgb(0x4a, 0x5a, 0x6a)), RadiusX = 3, RadiusY = 3, RenderTransformOrigin = new System.Windows.Point(0.5, 1) };
        Canvas.SetLeft(_hourHand, cx - 3); Canvas.SetTop(_hourHand, cy - 50); canvas.Children.Add(_hourHand);

        _minuteHand = new Rectangle { Width = 4, Height = 75, Fill = new SolidColorBrush(Color.FromRgb(0x7a, 0x8a, 0x9a)), RadiusX = 2, RadiusY = 2, RenderTransformOrigin = new System.Windows.Point(0.5, 1) };
        Canvas.SetLeft(_minuteHand, cx - 2); Canvas.SetTop(_minuteHand, cy - 75); canvas.Children.Add(_minuteHand);

        _secondHand = new Rectangle { Width = 2, Height = 90, Fill = new SolidColorBrush(Color.FromRgb(0xe6, 0x5e, 0x5e)), RadiusX = 1, RadiusY = 1, RenderTransformOrigin = new System.Windows.Point(0.5, 1) };
        Canvas.SetLeft(_secondHand, cx - 1); Canvas.SetTop(_secondHand, cy - 90); canvas.Children.Add(_secondHand);

        var pin = new Ellipse { Width = 18, Height = 18, Fill = faceBrush };
        pin.Effect = new DropShadowEffect { BlurRadius = 4, ShadowDepth = 2, Color = Colors.Black, Opacity = 0.4 };
        Canvas.SetLeft(pin, cx - 9); Canvas.SetTop(pin, cy - 9); canvas.Children.Add(pin);
        var pinInner = new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(Color.FromRgb(0x4a, 0x5a, 0x6a)) };
        Canvas.SetLeft(pinInner, cx - 3); Canvas.SetTop(pinInner, cy - 3); canvas.Children.Add(pinInner);

        AnalogContainer.Children.Add(canvas);
    }

    public void ApplyConfig() { }
}
