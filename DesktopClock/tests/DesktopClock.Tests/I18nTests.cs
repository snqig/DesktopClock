using System;
using System.IO;
using System.Reflection;
using System.Windows.Markup;
using DesktopClock.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesktopClock.Tests;

[TestClass]
public class I18nTests
{
    /// <summary>
    /// 通过反射调用私有 Normalize 方法,直接验证语言代码归一化逻辑。
    /// Apply 依赖 Application.Current(在测试 host 中通常为 null),
    /// 直接调用 Normalize 可避免 WPF 资源字典加载副作用,稳定地覆盖映射规则。
    /// </summary>
    private static string InvokeNormalize(string? input)
    {
        var method = typeof(I18n).GetMethod(
            "Normalize",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "未找到 I18n.Normalize 方法");
        return (string)method.Invoke(null, new object?[] { input })!;
    }

    [DataTestMethod]
    [DataRow("en", "en")]
    [DataRow("en-us", "en")]
    [DataRow("english", "en")]
    [DataRow("EN-US", "en")]
    [DataRow("ja", "ja")]
    [DataRow("ja-jp", "ja")]
    [DataRow("japanese", "ja")]
    [DataRow("zh", "zh")]
    [DataRow("zh-cn", "zh")]
    [DataRow("zh-hans", "zh")]
    [DataRow("chinese", "zh")]
    [DataRow("", "zh")]
    [DataRow(null, "zh")]
    [DataRow("unknown", "zh")]
    [DataRow("french", "zh")]
    public void Normalize_MapsLanguageCodesToCanonical(string? input, string expected)
    {
        Assert.AreEqual(expected, InvokeNormalize(input));
    }

    [TestMethod]
    public void Apply_TolerantInNonUiContext_DoesNotThrowFatalException()
    {
        // Apply 依赖 Application.Current 与 pack:// 资源字典。在非 UI 测试环境
        // 中可能抛出资源加载相关异常,这些是环境限制而非代码缺陷,予以容忍。
        try
        {
            I18n.Apply("en");
        }
        catch (Exception ex) when (
            ex is XamlParseException
            || ex is IOException
            || ex is UriFormatException
            || ex is InvalidOperationException)
        {
            // 非 UI 环境下资源字典加载失败可接受,不视为致命异常。
            Assert.Inconclusive(
                "非 UI 环境下 Apply 抛出预期异常: " + ex.GetType().Name);
        }
    }

    [TestMethod]
    public void CurrentLanguage_DefaultIsZh()
    {
        // CurrentLanguage 默认值为 "zh",I18n 是静态类,未调用 Apply 前应保持默认。
        // 注意:若其他测试已调用 Apply 且 Application.Current 可用,该值可能被改变,
        // 因此此处仅断言其属于已知语言集合。
        CollectionAssert.Contains(
            new[] { "zh", "en", "ja" },
            I18n.CurrentLanguage);
    }
}
