using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DesktopClock.Services;

namespace DesktopClock;

public partial class App : Application
{
    public static string? StartupDisplayMode { get; private set; }
    public static int? StartupInstanceId { get; private set; }

    /// <summary>
    /// 旧版崩溃日志路径(保留兼容,新日志走 Serilog)。
    /// </summary>
    private static string CrashLogPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopClock");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "crash.log");
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Serilog 初始化必须在最前,确保后续异常都能被记录
        Logger.Init();
        Logger.Information($"[App] Startup at {DateTime.Now:O}, args: {string.Join(" ", e.Args)}");

        RegisterGlobalExceptionHandlers();

        ParseArgs(e.Args);
        if (StartupInstanceId.HasValue)
            AppSettings.CurrentInstanceId = StartupInstanceId.Value;

        try
        {
            SyncAutoStartFromSettings();
        }
        catch (Exception ex)
        {
            Logger.Error("[App] SyncAutoStartFromSettings failed", ex);
        }

        // 应用启动时按设置初始化语言(zh / en / ja)
        try
        {
            var s = AppSettings.Load();
            I18n.Apply(s.Language);
        }
        catch (Exception ex)
        {
            Logger.Error("[App] I18n apply failed", ex);
        }

        Logger.Information("[App] before base.OnStartup");
        base.OnStartup(e);
        Logger.Information("[App] after base.OnStartup, MainWindow created");
    }

    private void RegisterGlobalExceptionHandlers()
    {
        // UI 线程未处理异常
        DispatcherUnhandledException += (s, args) =>
        {
            Logger.Error("[DispatcherUnhandledException]", args.Exception);
            WriteCrashLog($"[DispatcherUnhandledException] {args.Exception}");
            Serilog.Log.CloseAndFlush();
            args.Handled = true;
        };

        // 非托管/后台线程未处理异常
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Logger.Error($"[AppDomainUnhandledException] isTerminating={args.IsTerminating}", ex);
            WriteCrashLog($"[AppDomainUnhandledException] isTerminating={args.IsTerminating} ex={args.ExceptionObject}");
            Serilog.Log.CloseAndFlush();
        };

        // Task 未观察异常
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            Logger.Error("[UnobservedTaskException]", args.Exception);
            args.SetObserved();
        };
    }

    /// <summary>
    /// 旧版崩溃日志(Serilog 之外的冗余备份,便于无 Serilog 依赖时排查)。
    /// </summary>
    internal static void WriteCrashLog(string msg)
    {
        try
        {
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }
        catch { }
    }

    /// <summary>
    /// 兼容旧调用入口,转发到 Serilog。
    /// </summary>
    internal static void WriteLog(string msg) => Logger.Information(msg);

    private static void ParseArgs(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
                StartupDisplayMode = arg.Substring(7);
            else if (arg.StartsWith("--instance=", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(arg.Substring(11), out var id))
                    StartupInstanceId = id;
            }
        }
    }

    /// <summary>
    /// 根据设置同步开机自启注册表项。设置关闭时移除注册表项,避免残留。
    /// </summary>
    private void SyncAutoStartFromSettings()
    {
        var settings = AppSettings.Load();
        SetAutoStart(settings.AutoStart);
    }

    /// <summary>
    /// 设置或移除开机自启注册表项。
    /// </summary>
    public static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (key == null) return;

            const string valueName = "DesktopClock";
            if (enable)
            {
                var fileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (fileName != null)
                {
                    key.SetValue(valueName, $"\"{fileName}\"");
                    Logger.Information($"[AutoStart] enabled -> {fileName}");
                }
            }
            else
            {
                if (key.GetValue(valueName) != null)
                {
                    key.DeleteValue(valueName, false);
                    Logger.Information("[AutoStart] disabled");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("[SetAutoStart] failed", ex);
        }
    }
}
