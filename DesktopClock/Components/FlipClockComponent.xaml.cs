using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DesktopClock.Services;

namespace DesktopClock.Components;

public partial class FlipClockComponent : UserControl, IClockComponent
{
    private int[] _oldDigits = { -1, -1, -1, -1, -1, -1 };

    public string Id => "flip_clock";
    public string DisplayName => "翻牌时钟";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public FlipClockComponent()
    {
        InitializeComponent();
        ApplyConfig();
        SettingsProvider.Instance.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        ApplyConfig();
    }

    public void Update(DateTime now)
    {
        var settings = SettingsProvider.Instance.Settings;
        int h = settings.Use24Hour ? now.Hour : (now.Hour % 12 == 0 ? 12 : now.Hour % 12);
        int m = now.Minute;
        int s = now.Second;

        int[] digits = { h / 10, h % 10, m / 10, m % 10, s / 10, s % 10 };
        TextBlock[] texts = { FlipH1, FlipH2, FlipM1, FlipM2, FlipS1, FlipS2 };
        Border[] borders = { FlipBorderH1, FlipBorderH2, FlipBorderM1, FlipBorderM2, FlipBorderS1, FlipBorderS2 };

        for (int i = 0; i < 6; i++)
        {
            if (digits[i] != _oldDigits[i])
            {
                texts[i].Text = digits[i].ToString();
                AnimateFlip(borders[i]);
                _oldDigits[i] = digits[i];
            }
        }
        FlipColon1.Text = ":";
        FlipColon2.Text = ":";
    }

    private void AnimateFlip(Border border)
    {
        var scale = new ScaleTransform(1, 1);
        border.RenderTransform = scale;
        border.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        var shrink = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(80));
        shrink.Completed += (_, _) =>
        {
            var grow = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(80));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        };
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
    }

    public void ApplyConfig()
    {
        var settings = SettingsProvider.Instance.Settings;
        var fg = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.FontColor));
        foreach (var tb in new TextBlock[] { FlipH1, FlipH2, FlipM1, FlipM2, FlipS1, FlipS2, FlipColon1, FlipColon2 })
            tb.Foreground = fg;
    }
}
