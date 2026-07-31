using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopClock.Models;

namespace DesktopClock.Components;

/// <summary>
/// 每日一言组件(P3):每天轮换一条名言/诗词,跑马灯横向滚动显示。
/// 复用 ScrollingTodoComponent 的跑马灯模式(从右到左无缝循环)。
/// 配置键(通过 Config.Settings 注入):
///   "quotesJson" - JSON 字符串,string 列表(本地语录库)
///   "apiEnabled" - 是否启用在线 API(默认 false)
///   "apiUrl"     - 一言 API 地址
///   "speed"      - 滚动速度(像素/秒,默认 30)
///   "fontSize"   - 字号
///   "fontColor"  - 颜色
///   "fontFamily" - 字体
/// </summary>
public class DailyQuoteComponent : IClockComponent
{
    public string Id => "daily_quote";
    public string DisplayName => "每日一言";
    public FrameworkElement View => _scrollHost;
    public ComponentConfig Config { get; set; } = new();

    private readonly Canvas _canvas;
    private readonly Border _scrollHost;
    private readonly TextBlock _textBlock;

    private List<string> _quotes = new();
    private bool _apiEnabled;
    private string _apiUrl = "https://v1.hitokoto.cn/?c=d&c=i&c=k";
    private double _speed = 30.0;
    private double _offset;
    private DateTime _lastUpdate = DateTime.Now;
    private double _scrollWidth;
    private DateTime _lastQuoteDate = DateTime.MinValue;
    private int _currentIndex;

    // 内置语录库(默认 20 条,分类覆盖励志/诗词/哲理)
    private static readonly List<string> DefaultQuotes = new()
    {
        "不积跬步,无以至千里;不积小流,无以成江海。 ——荀子",
        "千里之行,始于足下。 ——老子",
        "路漫漫其修远兮,吾将上下而求索。 ——屈原",
        "宝剑锋从磨砺出,梅花香自苦寒来。 ——古训",
        "天行健,君子以自强不息。 ——《周易》",
        "海纳百川,有容乃大;壁立千仞,无欲则刚。 ——林则徐",
        "业精于勤,荒于嬉;行成于思,毁于随。 ——韩愈",
        "三人行,必有我师焉。 ——《论语》",
        "学而不思则罔,思而不学则殆。 ——《论语》",
        "知之者不如好之者,好之者不如乐之者。 ——《论语》",
        "生活不是等待风暴过去,而是学会在雨中翩翩起舞。",
        "星光不问赶路人,时光不负有心人。",
        "你现在的努力,是未来你感谢自己的理由。",
        "所有的幸运,都是努力埋下的伏笔。",
        "愿你有前进一寸的勇气,亦有后退一尺的从容。",
        "山高路远,看世界,也找自己。",
        "把每一天活成想要的样子,便是对生活最好的交代。",
        "种一棵树最好的时间是十年前,其次是现在。",
        "心怀浪漫宇宙,也珍惜人间日常。",
        "凡是过往,皆为序章。 ——莎士比亚"
    };

    public DailyQuoteComponent()
    {
        _textBlock = new TextBlock
        {
            FontSize = 12,
            Foreground = Brushes.LightSkyBlue,
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
        _quotes = new List<string>(DefaultQuotes);
        PickQuoteForToday();
    }

    /// <summary>由 MainWindow 调用,设置跑马灯可视宽度(与时间组件等宽)。</summary>
    public void SetScrollWidth(double width)
    {
        if (width <= 0) return;
        _scrollWidth = width;
        _scrollHost.Width = width;
        _canvas.Width = width;
        _offset = width;
        Canvas.SetLeft(_textBlock, _offset);
    }

    private void PickQuoteForToday()
    {
        if (_quotes.Count == 0)
        {
            _textBlock.Text = "";
            return;
        }
        // 按日期确定性地选择今日语录(同一天显示同一条)
        var seed = DateTime.Today.DayOfYear;
        _currentIndex = seed % _quotes.Count;
        _textBlock.Text = "  " + _quotes[_currentIndex] + "  ";
        _lastQuoteDate = DateTime.Today;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        // 每天轮换
        if (DateTime.Today != _lastQuoteDate)
            PickQuoteForToday();

        var now = DateTime.Now;
        double dt = (now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        if (string.IsNullOrEmpty(_textBlock.Text)) return;
        if (_scrollWidth <= 0) return;

        double textWidth = _textBlock.ActualWidth;
        if (textWidth <= 0) return;

        _offset -= _speed * dt;
        double cycleWidth = textWidth + _scrollWidth;
        while (_offset < -textWidth) _offset += cycleWidth;

        Canvas.SetLeft(_textBlock, _offset);
    }

    public void Update(DateTime now) { }

    public void ApplyConfig()
    {
        if (Config.Settings.TryGetValue("quotesJson", out var qj))
        {
            var json = qj is string s ? s : qj.ToString() ?? "[]";
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                if (parsed != null && parsed.Count > 0) _quotes = parsed;
            }
            catch { }
            PickQuoteForToday();
        }

        if (Config.Settings.TryGetValue("apiEnabled", out var ae))
        {
            if (ae is bool aeb) _apiEnabled = aeb;
            else if (ae is string aes && bool.TryParse(aes, out var r)) _apiEnabled = r;
        }

        if (Config.Settings.TryGetValue("apiUrl", out var au) && au is string aus)
            _apiUrl = aus;

        if (Config.Settings.TryGetValue("speed", out var sp))
        {
            double speed = 30;
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
