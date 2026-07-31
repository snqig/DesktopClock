using System;
using System.Collections.Generic;
using System.Windows;

namespace DesktopClock.Skins;

/// <summary>
/// 表盘皮肤接口。所有可换肤的表盘(指针/缎带/双时区/赛博朋克等)都实现此接口。
/// 与 <see cref="Components.IClockComponent"/> 不同,皮肤面向视觉层,
/// 由 <see cref="SkinHost"/> 统一装载并驱动刷新。
/// </summary>
public interface IClockSkin
{
    string Id { get; }
    string DisplayName { get; }
    FrameworkElement View { get; }

    /// <summary>由 SkinHost 每秒驱动一次,用于刷新指针角度等。</summary>
    void UpdateTime(DateTime now);

    /// <summary>从配置字典加载皮肤专属参数(颜色/粗细/图片路径等)。</summary>
    void LoadConfig(Dictionary<string, object> config);

    /// <summary>导出当前皮肤配置,用于序列化/克隆。</summary>
    Dictionary<string, object> SaveConfig();
}
