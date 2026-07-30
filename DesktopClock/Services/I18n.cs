using System;
using System.Windows;

namespace DesktopClock.Services;

/// <summary>
/// 多语言国际化服务,基于 WPF ResourceDictionary 切换。
/// 支持 zh / en / ja 三语,资源文件位于 Resources/Strings.{lang}.xaml。
/// 切换语言时替换 Application.Resources.MergedDictionaries 中的语言字典,
/// 所有使用 {DynamicResource key} 绑定的元素会自动刷新。
/// </summary>
public static class I18n
{
    /// <summary>
    /// 当前语言代码(zh / en / ja)。未知值回退为 zh。
    /// </summary>
    public static string CurrentLanguage { get; private set; } = "zh";

    /// <summary>
    /// 应用指定语言。若与当前语言相同则不重复切换。
    /// </summary>
    public static void Apply(string language)
    {
        var lang = Normalize(language);
        if (lang == CurrentLanguage && GetLanguageDict() != null) return;

        var dict = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Resources/Strings.{lang}.xaml", UriKind.Absolute)
        };

        // 移除旧的语言字典(以 Strings. 开头的来源)
        var dicts = Application.Current?.Resources?.MergedDictionaries;
        if (dicts == null) return;

        for (int i = dicts.Count - 1; i >= 0; i--)
        {
            var d = dicts[i];
            if (d.Source != null && d.Source.OriginalString.Contains("/Resources/Strings."))
                dicts.RemoveAt(i);
        }

        dicts.Add(dict);
        CurrentLanguage = lang;
    }

    /// <summary>
    /// 获取当前加载的语言字典(可能为 null,启动前调用)。
    /// </summary>
    private static ResourceDictionary? GetLanguageDict()
    {
        var dicts = Application.Current?.Resources?.MergedDictionaries;
        if (dicts == null) return null;
        foreach (var d in dicts)
        {
            if (d.Source != null && d.Source.OriginalString.Contains("/Resources/Strings."))
                return d;
        }
        return null;
    }

    /// <summary>
    /// 取本地化字符串,缺失时返回 key 本身。
    /// </summary>
    public static string GetString(string key)
    {
        return Application.Current?.TryFindResource(key) as string ?? key;
    }

    private static string Normalize(string? language)
    {
        if (string.IsNullOrEmpty(language)) return "zh";
        var lower = language.ToLowerInvariant();
        return lower switch
        {
            "zh" or "zh-cn" or "zh-hans" or "chinese" => "zh",
            "en" or "en-us" or "english" => "en",
            "ja" or "ja-jp" or "japanese" => "ja",
            _ => "zh"
        };
    }
}
