using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopClock.Models;

namespace DesktopClock.Components;

/// <summary>
/// 待办滚动组件 — 跑马灯横向滚动
/// 宽度由外部 SetScrollWidth 设置(与时间组件右边缘对齐),
/// 文字从右边缘进入,向左滚动,形成无缝循环。
/// </summary>
public class ScrollingTodoComponent : IClockComponent
{
    public string Id => "scrolling_todo";
    public string DisplayName => "待办滚动";
    public FrameworkElement View => _scrollHost;
    public ComponentConfig Config { get; set; } = new();

    private readonly Canvas _canvas;
    private readonly Border _scrollHost;
    private readonly TextBlock _textBlock;

    private double _speed = 40.0;
    private double _offset;
    private DateTime _lastUpdate = DateTime.Now;
    private double _scrollWidth; // 跑马灯可视区域宽度

    public ScrollingTodoComponent()
    {
        _textBlock = new TextBlock
        {
            FontSize = 12,
            Foreground = Brushes.LightYellow,
            FontFamily = new FontFamily("Microsoft YaHei"),
            Text = "",
            VerticalAlignment = VerticalAlignment.Center
        };

        _canvas = new Canvas
        {
            Height = 24,
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _canvas.Children.Add(_textBlock);
        Canvas.SetLeft(_textBlock, 0);
        Canvas.SetTop(_textBlock, 2);

        _scrollHost = new Border
        {
            Child = _canvas,
            Height = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
            Background = Brushes.Transparent
        };

        CompositionTarget.Rendering += OnRendering;
    }

    /// <summary>
    /// 由 MainWindow 调用,设置跑马灯可视宽度(与时间组件等宽)。
    /// </summary>
    public void SetScrollWidth(double width)
    {
        if (width <= 0) return;
        _scrollWidth = width;
        _scrollHost.Width = width;
        _canvas.Width = width;
        // 文字从右边缘开始
        _offset = width;
        Canvas.SetLeft(_textBlock, _offset);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        double dt = (now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        if (string.IsNullOrEmpty(_textBlock.Text)) return;
        if (_scrollWidth <= 0) return;

        double textWidth = _textBlock.ActualWidth;
        if (textWidth <= 0) return;

        // 从右到左滚动
        _offset -= _speed * dt;

        // 无缝循环:文字完全滚出左侧后,从右边缘重新进入
        double cycleWidth = textWidth + _scrollWidth;
        while (_offset < -textWidth) _offset += cycleWidth;

        Canvas.SetLeft(_textBlock, _offset);
    }

    public void Update(DateTime now) { }

    public void ApplyConfig()
    {
        if (Config.Settings.TryGetValue("text", out var t) && t is string s)
            _textBlock.Text = s;

        if (Config.Settings.TryGetValue("speed", out var sp))
        {
            double speed = 40;
            if (sp is double d) speed = d;
            else if (sp is string ss2 && double.TryParse(ss2, out var r)) speed = r;
            _speed = Math.Max(5, Math.Min(200, speed));
        }

        if (Config.Settings.TryGetValue("fontColor", out var fc))
        {
            try { _textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fc.ToString()!)); }
            catch { }
        }

        if (Config.Settings.TryGetValue("fontSize", out var fsz))
        {
            double size = 12;
            if (fsz is double d) size = d;
            else if (fsz is string ss2 && double.TryParse(ss2, out var r)) size = r;
            if (size > 0)
            {
                _textBlock.FontSize = size;
                _canvas.Height = Math.Max(20, size + 8);
                _scrollHost.Height = _canvas.Height;
            }
        }

        if (Config.Settings.TryGetValue("fontFamily", out var ff) && ff is string ffs && !string.IsNullOrEmpty(ffs))
        {
            try { _textBlock.FontFamily = new FontFamily(ffs); }
            catch { }
        }
    }
}
