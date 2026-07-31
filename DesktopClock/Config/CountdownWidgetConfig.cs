using System;

namespace DesktopClock.Config;

/// <summary>
/// 倒计时挂件配置(新增需求)。
/// 完全独立于时钟配置,互不干扰。
/// </summary>
public class CountdownWidgetConfig
{
    /// <summary>挂件启用开关</summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>独立窗口配置</summary>
    public WindowSetting Window { get; set; } = new()
    {
        Width = 240,
        Height = 120
    };

    /// <summary>样式配置</summary>
    public CountdownStyle Style { get; set; } = new();

    /// <summary>计时配置</summary>
    public CountdownTimerSetting Timer { get; set; } = new();
}

/// <summary>倒计时文字样式</summary>
public class CountdownStyle
{
    public string FontFamily { get; set; } = "Microsoft YaHei UI";
    public double FontSize { get; set; } = 48;
    public string FontColor { get; set; } = "#FFFFFFFF";

    public bool StrokeEnabled { get; set; } = false;
    public double StrokeThickness { get; set; } = 1.0;
    public string StrokeColor { get; set; } = "#FF000000";

    public bool ShadowEnabled { get; set; } = true;
    public double ShadowSize { get; set; } = 4.0;
    public string ShadowColor { get; set; } = "#FF000000";
}

/// <summary>倒计时计时与动作配置</summary>
public class CountdownTimerSetting
{
    /// <summary>目标时间(UTC)</summary>
    public DateTime TargetTime { get; set; } = DateTime.UtcNow.AddDays(1);

    /// <summary>标题文本</summary>
    public string Title { get; set; } = "倒计时";

    /// <summary>是否显示标题</summary>
    public bool ShowTitle { get; set; } = true;

    /// <summary>显示模式: "days" = D 天 H:M:S, "time" = 仅 H:M:S</summary>
    public string DisplayMode { get; set; } = "days";

    /// <summary>倒计时结束动作: none/blink/alert/sound</summary>
    public string EndAction { get; set; } = "blink";

    /// <summary>到达目标时间后是否归零显示 00:00:00</summary>
    public bool StopAtZero { get; set; } = true;
}
