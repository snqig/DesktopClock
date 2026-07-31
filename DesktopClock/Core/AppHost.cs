using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DesktopClock.Core;

/// <summary>
/// 应用程序 DI 容器入口。
/// 一次性构建,运行期间只读访问。
/// 现有 MainWindow 仍可继续直接 new,新模块逐步迁移到 DI。
/// </summary>
public static class AppHost
{
    private static IServiceProvider? _sp;
    private static IHost? _host;

    /// <summary>构建后的根 ServiceProvider。第一次访问时自动构建。</summary>
    public static IServiceProvider Services
    {
        get
        {
            if (_sp == null) Build();
            return _sp!;
        }
    }

    /// <summary>获取某服务(返回 null 如果未注册)。</summary>
    public static T? GetService<T>() where T : class => Services.GetService<T>();

    /// <summary>获取必需服务(未注册抛异常)。</summary>
    public static T GetRequiredService<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>启动后台 Host(目前为空壳,后续 P1 日志/服务可挂在上面)。</summary>
    public static System.Threading.Tasks.Task StartAsync()
    {
        Build();
        return _host?.StartAsync() ?? System.Threading.Tasks.Task.CompletedTask;
    }

    /// <summary>停止后台 Host。</summary>
    public static System.Threading.Tasks.Task StopAsync()
        => _host?.StopAsync() ?? System.Threading.Tasks.Task.CompletedTask;

    private static void Build()
    {
        if (_sp != null) return;

        _host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // 暂留空壳,新模块注册在 Core/ServiceCollectionExtensions.cs
                // 现有 Logger 静态类继续工作
            })
            .Build();

        _sp = _host.Services;
    }
}
