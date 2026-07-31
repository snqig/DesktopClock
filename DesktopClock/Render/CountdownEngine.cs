using System;
using DesktopClock.Config;

namespace DesktopClock.Render;

/// <summary>
/// 倒计时计算与格式化引擎。
/// 输入 CountdownTimerSetting,输出展示字符串与结束事件。
/// </summary>
public sealed class CountdownEngine
{
    private CountdownTimerSetting _setting;
    private bool _ended;

    public event Action? Ended;

    /// <summary>当前结束动作(透传 setting.EndAction)。</summary>
    public string EndAction => _setting.EndAction;

    /// <summary>当前标题(透传 setting.Title)。</summary>
    public string Title => _setting.Title;

    /// <summary>当前显示模式(透传 setting.DisplayMode)。</summary>
    public string DisplayMode => _setting.DisplayMode;

    public CountdownEngine(CountdownTimerSetting setting)
    {
        _setting = setting ?? new CountdownTimerSetting();
    }

    /// <summary>更新设置(运行时切换配置)。会重置结束状态以支持新目标时间的重新计时。</summary>
    public void UpdateSetting(CountdownTimerSetting setting)
    {
        if (setting == null) return;
        // 如果目标时间发生变化,重置结束标志,使新目标时间能正常倒计时
        if (setting.TargetTime != _setting.TargetTime) _ended = false;
        // 整体替换设置对象,确保标题/显示模式/结束动作/归零策略全部生效
        _setting = setting;
    }

    /// <summary>计算剩余时间并返回格式化字符串。</summary>
    public string Render(DateTime nowUtc)
    {
        var remaining = _setting.TargetTime - nowUtc;

        if (remaining <= TimeSpan.Zero)
        {
            if (!_ended)
            {
                _ended = true;
                Ended?.Invoke();
            }
            return _setting.StopAtZero ? FormatZero() : string.Empty;
        }

        return _setting.DisplayMode == "time"
            ? $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{remaining.Days:D1} 天 {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    private string FormatZero()
        => _setting.DisplayMode == "time" ? "00:00:00" : "0 天 00:00:00";
}
