using System;

namespace DesktopClock.Config;

/// <summary>
/// 挂件窗口通用配置(时钟、倒计时共用)。
/// 与现有 AppSettings 的 flat 字段并存,迁移完成后再合并到 AppConfig。
/// </summary>
public class WindowSetting
{
    /// <summary>窗口左边距(逻辑像素)</summary>
    public double Left { get; set; } = double.NaN;

    /// <summary>窗口上边距(逻辑像素)</summary>
    public double Top { get; set; } = double.NaN;

    /// <summary>窗口宽度</summary>
    public double Width { get; set; } = 400;

    /// <summary>窗口高度</summary>
    public double Height { get; set; } = 200;

    /// <summary>窗口整体透明度 0~1</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>是否置顶</summary>
    public bool Topmost { get; set; } = true;

    /// <summary>是否鼠标穿透</summary>
    public bool ClickThrough { get; set; } = false;

    /// <summary>显示器唯一 ID(用于多显示器持久化)</summary>
    public string DisplayId { get; set; } = string.Empty;

    /// <summary>是否锁定位置</summary>
    public bool LockPosition { get; set; } = false;
}
