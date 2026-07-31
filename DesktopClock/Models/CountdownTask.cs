using System;

namespace DesktopClock.Models;

/// <summary>
/// 倒计时任务实体:支持多个倒计时任务,在同一挂件窗口内循环切换显示。
/// 与 AppSettings.CountdownTasks 列表持久化。
/// </summary>
public class CountdownTask
{
    /// <summary>任务唯一 ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>任务标题(如 "新年"、"考试")</summary>
    public string Title { get; set; } = "倒计时";

    /// <summary>目标时间(UTC)</summary>
    public DateTime TargetTimeUtc { get; set; } = DateTime.UtcNow.AddDays(1);

    /// <summary>是否启用(未启用的任务不参与轮播)</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>显示模式:"days" = D 天 H:M:S, "time" = 仅 H:M:S</summary>
    public string DisplayMode { get; set; } = "days";

    /// <summary>到达目标时间后的动作:none/blink/alert/sound</summary>
    public string EndAction { get; set; } = "blink";

    /// <summary>本地时间显示用(序列化时不持久化)</summary>
    public DateTime TargetTimeLocal => TargetTimeUtc.ToLocalTime();
}
