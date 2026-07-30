using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopClock.Models;

namespace DesktopClock.Components;

public class CountdownComponent : IClockComponent
{
    public string Id => "countdown";
    public string DisplayName => "倒计时";
    public FrameworkElement View => _text;
    public ComponentConfig Config { get; set; } = new();

    private readonly TextBlock _text;
    private DateTime? _target;
    private string _label = "倒计时";

    public CountdownComponent()
    {
        _text = new TextBlock
        {
            Text = "",
            FontSize = 13,
            Foreground = Brushes.Orange,
            FontFamily = new FontFamily("Microsoft YaHei")
        };
    }

    public void Update(DateTime now)
    {
        if (_target == null)
        {
            _text.Text = "";
            return;
        }
        var diff = _target.Value - now;
        if (diff.TotalSeconds <= 0)
        {
            _text.Text = $"{_label}: 时间到!";
            return;
        }
        string fmt;
        if (diff.TotalDays >= 1)
            fmt = $"{_label}: {(int)diff.TotalDays}天 {diff.Hours:00}:{diff.Minutes:00}:{diff.Seconds:00}";
        else
            fmt = $"{_label}: {diff.Hours:00}:{diff.Minutes:00}:{diff.Seconds:00}";
        _text.Text = fmt;
    }

    public void ApplyConfig()
    {
        if (Config.Settings.TryGetValue("target", out var t))
        {
            if (t is DateTime dt) _target = dt;
            else if (t is string s && DateTime.TryParse(s, out var dtp)) _target = dtp;
        }
        if (Config.Settings.TryGetValue("label", out var l) && l is string ls)
            _label = ls;
        if (Config.Settings.TryGetValue("fontColor", out var fc))
        {
            try { _text.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fc.ToString()!)); }
            catch { }
        }
    }
}
