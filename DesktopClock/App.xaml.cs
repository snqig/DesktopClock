using System.Windows;

namespace DesktopClock;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        SetAutoStart();
        base.OnStartup(e);
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