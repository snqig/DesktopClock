using System;

namespace DesktopClock.Models;

/// <summary>
/// 一套完整指针方案:时针 + 分针 + 秒针各自独立配置,可分类管理。
/// 支持混搭组合(A方案时针 + B方案分针 + C方案秒针)另存为新方案。
/// </summary>
public class PointerSet
{
    /// <summary>方案唯一 ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>方案名称(如 "赛博朋克霓虹")</summary>
    public string Name { get; set; } = "未命名方案";

    /// <summary>分类标签:简约 / 复古 / 夜光 / 极简 / 自制 / 商务</summary>
    public string Category { get; set; } = "自制";

    /// <summary>时针配置</summary>
    public SinglePointerStyle HourStyle { get; set; } = new();

    /// <summary>分针配置</summary>
    public SinglePointerStyle MinuteStyle { get; set; } = new();

    /// <summary>秒针配置</summary>
    public SinglePointerStyle SecondStyle { get; set; } = new();

    /// <summary>备注</summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>是否收藏(置顶)</summary>
    public bool Favorite { get; set; } = false;

    /// <summary>深拷贝(复制方案用)</summary>
    public PointerSet Clone() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = Name + " 副本",
        Category = Category,
        HourStyle = HourStyle.Clone(),
        MinuteStyle = MinuteStyle.Clone(),
        SecondStyle = SecondStyle.Clone(),
        Note = Note,
        CreatedAt = DateTime.Now,
        Favorite = false
    };

    /// <summary>从混搭创建新方案:A时针 + B分针 + C秒针</summary>
    public static PointerSet FromMix(
        string name,
        string category,
        SinglePointerStyle hour,
        SinglePointerStyle minute,
        SinglePointerStyle second) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = name,
        Category = category,
        HourStyle = hour.Clone(),
        MinuteStyle = minute.Clone(),
        SecondStyle = second.Clone(),
        CreatedAt = DateTime.Now
    };
}
