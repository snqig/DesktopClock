using System;

namespace DesktopClock.Config;

/// <summary>
/// 时钟挂件配置(包装现有 AppSettings 中时钟相关字段)。
/// 不破坏现有 AppSettings,作为新 AppConfig 的时钟配置实体存在。
/// 迁移期间由 ConfigMigrator 从 AppSettings 映射过来。
/// </summary>
public class ClockWidgetConfig
{
    /// <summary>挂件启用开关</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>独立窗口配置</summary>
    public WindowSetting Window { get; set; } = new();

    /// <summary>显示模式:digital/flip/binary/progress/analog_skin/cyberpunk/pointer_editor/minimal 等</summary>
    public string DisplayMode { get; set; } = "digital";

    /// <summary>时间格式</summary>
    public string DateTimeFormat { get; set; } = "HH:mm:ss";

    /// <summary>是否 24 小时制</summary>
    public bool Use24Hour { get; set; } = true;

    /// <summary>是否显示秒</summary>
    public bool ShowSeconds { get; set; } = true;

    /// <summary>主字体</summary>
    public string FontFamily { get; set; } = "DS-Digital";

    /// <summary>主字号</summary>
    public double FontSize { get; set; } = 64;

    /// <summary>主文字颜色</summary>
    public string FontColor { get; set; } = "#00d4ff";

    /// <summary>背景透明度(0~1,0=完全透明)</summary>
    public double BackgroundOpacity { get; set; } = 0.0;

    /// <summary>背景类型:none/solid/gradient</summary>
    public string BackgroundType { get; set; } = "none";

    /// <summary>当前激活的指针方案 ID</summary>
    public string ActivePointerSetId { get; set; } = string.Empty;

    /// <summary>是否显示日期</summary>
    public bool ShowDate { get; set; } = true;

    /// <summary>是否显示农历</summary>
    public bool LunarEnabled { get; set; } = false;
}
