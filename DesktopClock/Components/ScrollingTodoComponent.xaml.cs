using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DesktopClock.Models;

namespace DesktopClock.Components;

public class ScrollingTodoComponent : IClockComponent
{
    public string Id => "scrolling_todo";
    public string DisplayName => "待办滚动";
    public FrameworkElement View => _canvas;
    public ComponentConfig Config { get; set; } = new();

    private readonly Canvas _canvas;
    private readonly TextBlock _textBlock;
    private double _speed = 40.0; // pixels per second
    private double _offset;
    private DateTime _lastUpdate = DateTime.Now;

    public ScrollingTodoComponent()
    {
        _canvas = new Canvas { ClipToBounds = true, Height = 20, Width = 300 };
        _textBlock = new TextBlock
        {
            FontSize = 12,
            Foreground = Brushes.LightYellow,
            FontFamily = new FontFamily("Microsoft YaHei"),
            Text = ""
        };
        _canvas.Children.Add(_textBlock);
        Canvas.SetLeft(_textBlock, 0);
        Canvas.SetTop(_textBlock, 2);

        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        double dt = (now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        if (string.IsNullOrEmpty(_textBlock.Text)) return;
        if (_canvas.ActualWidth <= 0) return;

        _offset -= _speed * dt;
        double textWidth = _textBlock.ActualWidth;
        if (textWidth <= 0) return;

        // 循环滚动
        double cycleWidth = textWidth + 60;
        while (_offset < -cycleWidth) _offset += cycleWidth;

        Canvas.SetLeft(_textBlock, _offset);
    }

    public void Update(DateTime now) { }

    public void ApplyConfig()
    {
        if (Config.Settings.TryGetValue("text", out var t) && t is string s)
            _textBlock.Text = s;
        if (Config.Settings.TryGetValue("speed", out var sp) && sp is double d)
            _speed = Math.Max(5, Math.Min(200, d));
        if (Config.Settings.TryGetValue("fontColor", out var fc))
        {
            try { _textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fc.ToString()!)); }
            catch { }
        }
    }
}
