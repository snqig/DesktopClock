using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DesktopClock;

public partial class App : Application
{
    public static string? StartupDisplayMode { get; private set; }
    public static int? StartupInstanceId { get; private set; }

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
        RegisterGlobalExceptionHandlers();
        WriteLog($"[App] Startup at {DateTime.Now:O}, args: {string.Join(" ", e.Args)}");

        ParseArgs(e.Args);
        if (StartupInstanceId.HasValue)
            AppSettings.CurrentInstanceId = StartupInstanceId.Value;

        try
        {
            SetAutoStart();
        }
        catch (Exception ex)
        {
            WriteLog($"[App] SetAutoStart failed: {ex}");
        }

        base.OnStartup(e);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (s, args) =>
        {
            WriteLog($"[DispatcherUnhandledException] {args.Exception}");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            WriteLog($"[AppDomainUnhandledException] isTerminating={args.IsTerminating} ex={args.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            WriteLog($"[UnobservedTaskException] {args.Exception}");
            args.SetObserved();
        };
    }

    internal static void WriteLog(string msg)
    {
        try
        {
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }
        catch { }
    }

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

    private void SetAutoStart()
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (key != null)
            {
                var fileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (fileName != null)
                    key.SetValue("DesktopClock", fileName);
            }
        }
        catch
        {
        }
    }
}
