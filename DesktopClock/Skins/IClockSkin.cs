using System.Collections.Generic;
using System.Windows;

namespace DesktopClock.Skins;

/// <summary>
/// 时钟表盘皮肤接口。每种主表盘(数字/指针/机械等)实现此接口,
/// 由 SkinHost 统一驱动时间更新与配置加载。
/// </summary>
public interface IClockSkin
{
    string Id { get; }
    string DisplayName { get; }
    FrameworkElement View { get; }

    /// <summary>刷新时间显示(含指针角度、数字等)</summary>
    void UpdateTime(DateTime now);

    /// <summary>加载皮肤自有配置</summary>
    void LoadConfig(Dictionary<string, object> config);

    /// <summary>保存皮肤自有配置</summary>
    Dictionary<string, object> SaveConfig();
}
