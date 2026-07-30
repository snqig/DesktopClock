using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DesktopClock.Services;

namespace DesktopClock.Components;

public partial class BinaryClockComponent : UserControl, IClockComponent
{
    // 改用单例 SettingsProvider,与主窗口设置实时同步(原 SettingsService 是启动快照,设置改动后不更新)
    private AppSettings Settings => SettingsProvider.Instance.Settings;
    private Ellipse[,]? _dots;
    private bool _built;
    private TextBlock? _colon1, _colon2;

    public string Id => "binary_clock";
    public string DisplayName => "二进制时钟";
    public System.Windows.FrameworkElement View => this;
    public Models.ComponentConfig Config { get; set; } = new();

    public BinaryClockComponent()
    {
        InitializeComponent();
    }

    public void Update(DateTime now)
    {
        BuildPanel();
        if (_dots == null) return;

        var settings = Settings;
        int h = settings.Use24Hour ? now.Hour : (now.Hour % 12 == 0 ? 12 : now.Hour % 12);
        int[] digits = { h / 10, h % 10, now.Minute / 10, now.Minute % 10, now.Second / 10, now.Second % 10 };

        Brush lit;
        try { lit = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.FontColor)); }
        catch { lit = new SolidColorBrush(Color.FromRgb(0x00, 0xd4, 0xff)); }
        var unlit = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

        for (int d = 0; d < 6; d++)
            for (int b = 0; b < 4; b++)
                _dots[d, 3 - b].Fill = (digits[d] & (1 << b)) != 0 ? lit : unlit;

        if (_colon1 != null) _colon1.Foreground = lit;
        if (_colon2 != null) _colon2.Foreground = lit;
    }

    private void BuildPanel()
    {
        if (_built) return;
        _built = true;

        var sv = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center };
        _dots = new Ellipse[6, 4];
        var unlit = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

        for (int d = 0; d < 6; d++)
        {
            if (d == 2 || d == 4)
            {
                var c = new TextBlock { Text = ":", FontSize = 20, VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new System.Windows.Thickness(3, 0, 3, 0), Foreground = unlit };
                if (d == 2) _colon1 = c; else _colon2 = c;
                sv.Children.Add(c);
            }
            var col = new Grid { Width = 22, Margin = new System.Windows.Thickness(2) };
            for (int r = 0; r < 4; r++) col.RowDefinitions.Add(new RowDefinition { Height = System.Windows.GridLength.Auto });
            for (int r = 0; r < 4; r++)
            {
                var dot = new Ellipse { Width = 14, Height = 14, Fill = unlit, Margin = new System.Windows.Thickness(2), StrokeThickness = 0 };
                Grid.SetRow(dot, r);
                col.Children.Add(dot);
                _dots[d, r] = dot;
            }
            sv.Children.Add(col);
        }
        BinaryContainer.Children.Add(sv);
    }

    public void ApplyConfig() { }
}
