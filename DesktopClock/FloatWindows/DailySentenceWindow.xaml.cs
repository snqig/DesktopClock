using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopClock.Core;
using DesktopClock.Services;

namespace DesktopClock.FloatWindows;

/// <summary>
/// 每日一言悬浮窗口：跑马灯横向滚动显示名言/诗词,每日轮换一条。
/// 支持本地语录库与一言 API(失败回退本地)。
/// 配置键(通过 ComponentManager 配置的 Settings 注入):
///   "apiEnabled"  - 是否启用在线 API(默认 false)
///   "apiUrl"      - 一言 API 地址(默认 https://v1.hitokoto.cn/?c=d&c=i&c=k)
///   "speed"       - 滚动速度(像素/秒,默认 30)
///   "quotesJson"  - JSON 字符串数组(本地语录库)
/// </summary>
public partial class DailySentenceWindow : BaseFloatWindow
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    // 内置语录库(默认 20 条,覆盖励志/诗词/哲理)
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

    private List<string> _quotes = new(DefaultQuotes);
    private bool _apiEnabled;
    private string _apiUrl = "https://v1.hitokoto.cn/?c=d&c=i&c=k";
    private double _speed = 30.0;

    private double _offset;
    private DateTime _lastUpdate = DateTime.Now;
    private DateTime _lastQuoteDate = DateTime.MinValue;
    private bool _offsetInitialized;

    private readonly DispatcherTimer _rolloverTimer;

    public DailySentenceWindow()
    {
        ComponentId = "daily_sentence";
        InitializeComponent();
        QuoteText.Text = "";

        // 跑马灯滚动驱动
        CompositionTarget.Rendering += OnRendering;

        // 跨日轮换检测(渲染暂停时的兜底)
        _rolloverTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _rolloverTimer.Tick += (_, _) => CheckDayRollover();
        _rolloverTimer.Start();
    }

    /// <summary>检测日期变更,变更则刷新语录(API 启用时同时拉取)。</summary>
    private void CheckDayRollover()
    {
        if (DateTime.Today == _lastQuoteDate) return;
        PickQuoteForToday();
        if (_apiEnabled) _ = FetchFromApiAsync();
    }

    /// <summary>按日期确定性地选择今日本地语录(同一天显示同一条)。</summary>
    private void PickQuoteForToday()
    {
        if (_quotes.Count == 0)
        {
            QuoteText.Text = "";
            return;
        }
        int index = DateTime.Today.DayOfYear % _quotes.Count;
        QuoteText.Text = "  " + _quotes[index] + "  ";
        _lastQuoteDate = DateTime.Today;
        _offsetInitialized = false;
    }

    /// <summary>从一言 API 获取,失败回退本地语录。</summary>
    private async System.Threading.Tasks.Task FetchFromApiAsync()
    {
        try
        {
            var resp = await _http.GetStringAsync(_apiUrl);
            using var doc = JsonDocument.Parse(resp);
            if (doc.RootElement.TryGetProperty("hitokoto", out var hitoEl))
            {
                var text = hitoEl.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    var from = doc.RootElement.TryGetProperty("from", out var fromEl)
                        ? fromEl.GetString() : null;
                    var display = string.IsNullOrEmpty(from) ? text : $"{text}  ——{from}";
                    QuoteText.Text = "  " + display + "  ";
                    _lastQuoteDate = DateTime.Today;
                    _offsetInitialized = false;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("[DailySentenceWindow] API fetch failed", ex);
        }
        // 回退本地
        PickQuoteForToday();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        // 跨日即时刷新(渲染帧内检测,零点立即切换)
        if (DateTime.Today != _lastQuoteDate)
            CheckDayRollover();

        var now = DateTime.Now;
        double dt = (now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        if (string.IsNullOrEmpty(QuoteText.Text)) return;

        double scrollWidth = ScrollCanvas.ActualWidth;
        double scrollHeight = ScrollCanvas.ActualHeight;
        if (scrollWidth <= 0) return;

        double textWidth = QuoteText.ActualWidth;
        double textHeight = QuoteText.ActualHeight;
        if (textWidth <= 0) return;

        // 首次或文本更换后,文字从右边缘进入
        if (!_offsetInitialized)
        {
            _offset = scrollWidth;
            _offsetInitialized = true;
        }
        else
        {
            // 从右到左滚动
            _offset -= _speed * dt;

            // 完全滚出左侧后,从右边缘重新进入(无缝循环)
            double cycleWidth = textWidth + scrollWidth;
            while (_offset < -textWidth) _offset += cycleWidth;
        }

        Canvas.SetLeft(QuoteText, _offset);
        if (scrollHeight > 0 && textHeight > 0)
            Canvas.SetTop(QuoteText, (scrollHeight - textHeight) / 2);
    }

    public override void LoadFromConfig()
    {
        var cfg = ComponentManager.Instance.EnsureConfig(ComponentId);

        // 位置与尺寸
        if (!double.IsNaN(cfg.Left)) Left = cfg.Left;
        if (!double.IsNaN(cfg.Top)) Top = cfg.Top;
        Width = cfg.Width > 0 ? cfg.Width : 360;
        Height = cfg.Height > 0 ? cfg.Height : 40;
        ClampToScreen();

        IsTopmost = cfg.Topmost;
        DesktopWidgetMode = cfg.DesktopWidgetMode;
        IsLocked = cfg.LockPosition;
        WindowOpacity = cfg.Opacity;

        // 字体与颜色
        try { QuoteText.FontFamily = new FontFamily(cfg.FontFamily); } catch { }
        QuoteText.FontSize = cfg.FontSize;
        try
        {
            QuoteText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(cfg.FontColor));
        }
        catch { }

        // 组件专属参数
        var quotesJson = cfg.GetString("quotesJson", "");
        if (!string.IsNullOrEmpty(quotesJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(quotesJson);
                if (parsed != null && parsed.Count > 0) _quotes = parsed;
            }
            catch { }
        }

        _apiEnabled = cfg.GetBool("apiEnabled", false);

        var apiUrl = cfg.GetString("apiUrl", "https://v1.hitokoto.cn/?c=d&c=i&c=k");
        if (!string.IsNullOrEmpty(apiUrl)) _apiUrl = apiUrl;

        var speed = cfg.GetDouble("speed", 30.0);
        _speed = Math.Max(5, Math.Min(200, speed));

        // 选取今日语录并按需拉取 API
        PickQuoteForToday();
        if (_apiEnabled) _ = FetchFromApiAsync();
    }

    public override void SavePosition()
    {
        var cfg = ComponentManager.Instance.EnsureConfig(ComponentId);
        cfg.Left = Left;
        cfg.Top = Top;
        cfg.Width = ActualWidth > 0 ? ActualWidth : Width;
        cfg.Height = ActualHeight > 0 ? ActualHeight : Height;
        cfg.Topmost = IsTopmost;
        cfg.DesktopWidgetMode = DesktopWidgetMode;
        cfg.LockPosition = IsLocked;
        cfg.Opacity = WindowOpacity;
        ComponentManager.Instance.SaveConfig();
    }

    public override void ApplyConfigChange()
    {
        LoadFromConfig();
    }

    protected override void OnClosed(EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _rolloverTimer.Stop();
        base.OnClosed(e);
    }
}
