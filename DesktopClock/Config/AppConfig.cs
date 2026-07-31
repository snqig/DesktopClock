using System.Collections.Generic;

namespace DesktopClock.Config;

/// <summary>
/// 新版主配置实体(P0 配置系统重构的目标载体)。
/// 与现有 AppSettings 并存,通过 ConfigMigrator 双向同步,迁移完成后替换。
/// 兼容现有 settings.json 通过 ConfigMigrator 自动完成。
/// </summary>
public class AppConfig
{
    public int Version { get; set; } = 3;

    /// <summary>时钟挂件配置</summary>
    public ClockWidgetConfig Clock { get; set; } = new();

    /// <summary>倒计时挂件配置</summary>
    public CountdownWidgetConfig Countdown { get; set; } = new();

    /// <summary>全局快捷键</summary>
    public HotkeySetting Hotkey { get; set; } = new();

    /// <summary>已启用插件列表</summary>
    public List<string> EnabledPlugins { get; set; } = new();

    /// <summary>语言:zh/en</summary>
    public string Language { get; set; } = "zh";

    /// <summary>主题预设</summary>
    public string ThemePreset { get; set; } = "default";

    /// <summary>开机自启</summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>游戏模式:全屏应用自动隐藏</summary>
    public bool GameMode { get; set; } = false;
}

public class HotkeySetting
{
    /// <summary>显示/隐藏全部挂件</summary>
    public string ToggleAll { get; set; } = "Ctrl+H";

    /// <summary>切换全局穿透</summary>
    public string ToggleClickThrough { get; set; } = "";

    /// <summary>切换全局置顶</summary>
    public string ToggleTopmost { get; set; } = "";

    /// <summary>单独显示/隐藏倒计时</summary>
    public string ToggleCountdown { get; set; } = "";

    /// <summary>切换倒计时穿透</summary>
    public string ToggleCountdownClickThrough { get; set; } = "";

    /// <summary>切换倒计时置顶</summary>
    public string ToggleCountdownTopmost { get; set; } = "";
}
