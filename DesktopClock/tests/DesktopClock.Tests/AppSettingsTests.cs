using System;
using System.Reflection;
using DesktopClock;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesktopClock.Tests;

[TestClass]
public class AppSettingsTests
{
    [TestMethod]
    public void Load_ReturnsNonNullInstance()
    {
        var settings = AppSettings.Load();
        Assert.IsNotNull(settings);
    }

    [TestMethod]
    public void JsonOpts_IsNotNull()
    {
        Assert.IsNotNull(AppSettings.JsonOpts);
    }

    [TestMethod]
    public void CurrentInstanceId_DefaultIsZero()
    {
        // 重置为默认值后验证(静态属性可能被其他测试或宿主修改)。
        AppSettings.CurrentInstanceId = 0;
        Assert.AreEqual(0, AppSettings.CurrentInstanceId);
    }

    [TestMethod]
    public void GetFilePath_EndsWithJson_ForPrimaryInstance()
    {
        AppSettings.CurrentInstanceId = 0;
        var path = InvokeGetFilePath();
        Assert.IsNotNull(path);
        Assert.IsTrue(
            path!.EndsWith(".json", StringComparison.OrdinalIgnoreCase),
            $"期望以 .json 结尾,实际: {path}");
    }

    [TestMethod]
    public void GetFilePath_EndsWithJson_ForSecondaryInstance()
    {
        AppSettings.CurrentInstanceId = 3;
        try
        {
            var path = InvokeGetFilePath();
            Assert.IsNotNull(path);
            Assert.IsTrue(
                path!.EndsWith(".json", StringComparison.OrdinalIgnoreCase),
                $"期望以 .json 结尾,实际: {path}");
            Assert.IsTrue(
                path.Contains("settings_instance_3", StringComparison.OrdinalIgnoreCase),
                $"期望包含实例标识,实际: {path}");
        }
        finally
        {
            AppSettings.CurrentInstanceId = 0;
        }
    }

    [TestMethod]
    public void GetPositionFilePath_EndsWithTxt()
    {
        AppSettings.CurrentInstanceId = 0;
        var path = AppSettings.GetPositionFilePath();
        Assert.IsNotNull(path);
        Assert.IsTrue(
            path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase),
            $"期望以 .txt 结尾,实际: {path}");
    }

    /// <summary>
    /// GetFilePath 为私有静态方法,通过反射调用以避免修改主项目可见性。
    /// </summary>
    private static string? InvokeGetFilePath()
    {
        var method = typeof(AppSettings).GetMethod(
            "GetFilePath",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "未找到 AppSettings.GetFilePath 方法");
        return (string?)method.Invoke(null, null);
    }
}
