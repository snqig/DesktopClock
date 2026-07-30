using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace DesktopClock.Services;

/// <summary>
/// 基于 Serilog 的统一日志服务,日志写入 %LOCALAPPDATA%\DesktopClock\logs\desktopclock.log,
/// 按天滚动保留 7 天。日志级别可通过 LOG_LEVEL 环境变量(Debug/Information/Warning/Error)调整。
/// </summary>
public static class Logger
{
    private static int _initialized;

    private static LoggerConfiguration BuildConfiguration()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopClock", "logs");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "desktopclock.log");

        var level = LogEventLevel.Information;
        var envLevel = Environment.GetEnvironmentVariable("LOG_LEVEL");
        if (!string.IsNullOrEmpty(envLevel) && Enum.TryParse(envLevel, true, out LogEventLevel parsed))
            level = parsed;

        return new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .Enrich.WithProperty("pid", Environment.ProcessId)
            .WriteTo.File(path,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
    }

    /// <summary>
    /// 初始化全局 Serilog Logger。仅可调用一次,重复调用将被忽略。
    /// </summary>
    public static void Init()
    {
        if (System.Threading.Interlocked.Exchange(ref _initialized, 1) != 0) return;
        Log.Logger = BuildConfiguration().CreateLogger();
    }

    public static void Information(string msg) => Log.Information(msg);
    public static void Warning(string msg) => Log.Warning(msg);
    public static void Error(string msg, Exception? ex = null)
    {
        if (ex != null) Log.Error(ex, msg);
        else Log.Error(msg);
    }
    public static void Debug(string msg) => Log.Debug(msg);
}

