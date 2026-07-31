using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopClock.Models;

namespace DesktopClock.Components;

/// <summary>
/// 倒计时组件(主窗口布局内嵌)。
/// 显示格式与 CountdownEngine 保持一致:
/// - displayMode=time: HH:MM:SS
/// - displayMode=days: D 天 HH:MM:SS
/// 到达目标时间后按 stopAtZero 显示 00:00:00 / 0 天 00:00:00。
/// 不再显示"倒计时:时间到!"这样的中文提示文本。
/// </summary>
public class CountdownComponent : IClockComponent
{
    public string Id => "countdown";
    public string DisplayName => "倒计时";
    public FrameworkElement View => _container;
    public ComponentConfig Config { get; set; } = new();

    private readonly StackPanel _container;
    private readonly TextBlock _titleText;
    private readonly TextBlock _countdownText;

    private DateTime? _targetLocal;
    private string _label = "倒计时";
    private string _displayMode = "days"; // days / time
    private bool _stopAtZero = true;
    private bool _showTitle = true;

    public CountdownComponent()
    {
        _titleText = new TextBlock
        {
            Text = "",
            FontSize = 12,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2),
            Visibility = Visibility.Collapsed
        };

        _countdownText = new TextBlock
        {
            Text = "",
            FontSize = 16,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = new FontFamily("Microsoft YaHei UI")
        };

        _container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _container.Children.Add(_titleText);
        _container.Children.Add(_countdownText);
    }

    /// <summary>
    /// 每帧由 LayoutEngine 调用,传入本地时间(与主时钟同步)。
    /// </summary>
    public void Update(DateTime nowLocal)
    {
        if (_targetLocal == null)
        {
            _countdownText.Text = "";
            return;
        }

        var remaining = _targetLocal.Value - nowLocal;

        if (remaining <= TimeSpan.Zero)
        {
            if (_stopAtZero)
                _countdownText.Text = _displayMode == "time" ? "00:00:00" : "0 天 00:00:00";
            else
                _countdownText.Text = "";
            return;
        }

        _countdownText.Text = _displayMode == "time"
            ? $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{remaining.Days:D1} 天 {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    public void ApplyConfig()
    {
        // 目标时间(本地时间)
        if (Config.Settings.TryGetValue("target", out var t))
        {
            if (t is DateTime dt)
                _targetLocal = dt;
            else if (t is DateTimeOffset dto)
                _targetLocal = dto.LocalDateTime;
            else if (t is string s && DateTime.TryParse(s, out var dtp))
                _targetLocal = dtp;
        }

        if (Config.Settings.TryGetValue("label", out var l) && l is string ls)
            _label = ls;

        if (Config.Settings.TryGetValue("displayMode", out var dm) && dm is string dms && !string.IsNullOrEmpty(dms))
            _displayMode = dms;

        if (Config.Settings.TryGetValue("stopAtZero", out var saz))
        {
            if (saz is bool sazb) _stopAtZero = sazb;
            else if (saz is string sazs && bool.TryParse(sazs, out var r)) _stopAtZero = r;
        }

        if (Config.Settings.TryGetValue("showTitle", out var st))
        {
            if (st is bool stb) _showTitle = stb;
            else if (st is string sts && bool.TryParse(sts, out var r)) _showTitle = r;
        }

        // 标题行显示
        if (_showTitle && !string.IsNullOrEmpty(_label))
        {
            _titleText.Text = _label;
            _titleText.Visibility = Visibility.Visible;
        }
        else
        {
            _titleText.Visibility = Visibility.Collapsed;
        }

        // 字体大小
        if (Config.Settings.TryGetValue("fontSize", out var fsz))
        {
            double size = 0;
            if (fsz is double dsz) size = dsz;
            else if (fsz is float flsz) size = flsz;
            else if (fsz is int isz) size = isz;
            else if (fsz is string ssz && double.TryParse(ssz, out var r)) size = r;
            if (size > 0) _countdownText.FontSize = size;
        }

        // 字体颜色
        if (Config.Settings.TryGetValue("fontColor", out var fc))
        {
            try { _countdownText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fc.ToString()!)); }
            catch { }
        }

        // 字体族
        if (Config.Settings.TryGetValue("fontFamily", out var ff) && ff is string ffs && !string.IsNullOrEmpty(ffs))
        {
            try { _countdownText.FontFamily = new FontFamily(ffs); }
            catch { }
        }
    }
}
