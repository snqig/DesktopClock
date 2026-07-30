using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopClock.Components;
using DesktopClock.Services;

namespace DesktopClock;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private AppSettings _settings = new();
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _hotkeyRegistered;
    private IntPtr _windowHandle;
    private bool _isShuttingDown;
    private readonly ComponentRegistry _registry = new();
    private readonly LayoutEngine _layoutEngine = new();
    private readonly PluginManager _pluginManager;
    private readonly HashSet<string> _firedReminders = new();

    private const int HOTKEY_ID = 9000;
    private const int WM_HOTKEY = 0x0312;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        RegisterComponents();
        _pluginManager = new PluginManager(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins"), _registry);
        _pluginManager.LoadAll(_settings.Plugins);
        ApplySettings();
        LoadPosition();

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
        _timer.Start();

        this.MouseLeftButtonDown += (s, e) =>
        {
            if (!_settings.LockPosition) this.DragMove();
        };

        this.MouseMove += (s, e) =>
        {
            _layoutEngine.HandleMouseMove(e.GetPosition(this));
        };

        this.MouseLeftButtonUp += (s, e) =>
        {
            _layoutEngine.HandleMouseUp(e.GetPosition(this), _settings.Layout);
        };

        this.KeyDown += (s, e) =>
        {
            _layoutEngine.HandleKeyDown(e.Key, _settings.Layout);
        };

        _layoutEngine.LayoutChanged += () =>
        {
            if (_layoutEngine.IsFreeMode)
            {
                // 先把当前画布上的坐标同步到 Layout.Positions,再持久化
                _layoutEngine.SaveFreePositions(MainContainer, _settings.Layout);
                _settings.Save();
            }
        };

        CreateTrayIcon();
    }

    private void RegisterComponents()
    {
        _registry.Register(new DateComponent());
        _registry.Register(new LunarComponent());
        _registry.Register(new DigitalClockComponent());
        _registry.Register(new FlipClockComponent());
        _registry.Register(new WordClockComponent());
        _registry.Register(new BinaryClockComponent());
        _registry.Register(new MinimalClockComponent());
        _registry.Register(new AnalogClockComponent());
        _registry.Register(new AnalogPremiumClockComponent());
        _registry.Register(new MechanicalClockComponent());
        _registry.Register(new WorldClockComponent());
    }

    private void RebuildLayout()
    {
        // Sync LayoutConfig from settings
        var clockId = _settings.DisplayMode switch
        {
            "flip" => "flip_clock",
            "word" => "word_clock",
            "binary" => "binary_clock",
            "minimal" => "minimal_clock",
            "progress" => "analog_clock",
            "analog_premium" => "analog_premium_clock",
            "mechanical" => "mechanical_clock",
            _ => "digital_clock"
        };

        var active = new List<string>();
        if (_settings.ShowDate) active.Add("date");
        if (_settings.LunarEnabled) active.Add("lunar");
        active.Add(clockId);
        if (_settings.WorldClockEnabled) active.Add("world_clock");

        // Add external plugin components
        foreach (var kvp in _registry.GetAllExternal())
        {
            if (!active.Contains(kvp.Key))
                active.Add(kvp.Key);
        }

        _settings.Layout.ActiveComponents = active;
        // 所有模式（包括 progress）都使用 Stack 布局,不再强制使用 Free 布局
        // 如需拖拽定位,用户可在设置中手动切换到 Free 模式

        _layoutEngine.BuildLayout(MainContainer, _registry, _settings.Layout);
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon();
        try
        {
            _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                System.Windows.Application.ResourceAssembly.Location);
        }
        catch
        {
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        }
        _trayIcon.Text = "桌面时钟";
        _trayIcon.Visible = true;

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示/隐藏", null, (_, _) => ToggleVisibility());
        menu.Items.Add("设置", null, (_, _) => OpenSettings());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) =>
        {
            _isShuttingDown = true;
            System.Windows.Application.Current.Shutdown();
        });

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ToggleVisibility();
    }

    private void ToggleVisibility()
    {
        if (this.Visibility == Visibility.Visible)
        {
            this.Visibility = Visibility.Hidden;
        }
        else
        {
            this.Visibility = Visibility.Visible;
            this.Show();
            this.Activate();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowHandle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(_windowHandle);
        source?.AddHook(WndProc);
        RegisterGlobalHotkey();
        if (_settings.ClickThrough)
            SetClickThrough(true);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ToggleVisibility();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void RegisterGlobalHotkey()
    {
        if (_windowHandle == IntPtr.Zero) return;
        if (_hotkeyRegistered)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _hotkeyRegistered = false;
        }
        try
        {
            var parts = _settings.HotkeyHide.Split('+');
            uint mod = 0, vk = 0;
            foreach (var part in parts)
            {
                switch (part.Trim().ToLower())
                {
                    case "ctrl": mod |= 0x0002; break;
                    case "alt": mod |= 0x0001; break;
                    case "shift": mod |= 0x0004; break;
                    case "win": mod |= 0x0008; break;
                    default:
                        var key = (Key)Enum.Parse(typeof(Key), part.Trim(), true);
                        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                        break;
                }
            }
            if (mod != 0 && vk != 0)
            {
                RegisterHotKey(_windowHandle, HOTKEY_ID, mod, vk);
                _hotkeyRegistered = true;
            }
        }
        catch { }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _registry.UpdateAll(DateTime.Now);
        UpdateChime();
        CheckReminders();
    }

    private void UpdateChime()
    {
        var now = DateTime.Now;
        if (_settings.ChimeEnabled && now.Minute == 0 && now.Second == 0)
            SystemSounds.Beep.Play();
    }

    private void CheckReminders()
    {
        if (!_settings.ReminderEnabled) return;
        var now = DateTime.Now;
        // 清理非当前分钟的已触发记录,防止集合无限增长;同分钟内仍能去重
        var currentMinute = now.ToString("yyyyMMddHHmm");
        _firedReminders.RemoveWhere(k => !k.EndsWith(currentMinute));
        List<ReminderItem> reminders;
        try { reminders = System.Text.Json.JsonSerializer.Deserialize<List<ReminderItem>>(_settings.RemindersJson) ?? new(); }
        catch { return; }

        foreach (var r in reminders)
        {
            if (!r.IsEnabled) continue;
            bool shouldFire = false;

            if (r.DateTime.HasValue && !r.IsRecurring)
            {
                var dt = r.DateTime.Value;
                shouldFire = now.Year == dt.Year && now.Month == dt.Month && now.Day == dt.Day &&
                             now.Hour == dt.Hour && now.Minute == dt.Minute && now.Second == 0;
            }
            else if (r.IsRecurring && r.DayOfWeek.HasValue)
            {
                shouldFire = now.DayOfWeek == r.DayOfWeek.Value &&
                             now.Hour == r.TimeOfDay.Hours &&
                             now.Minute == r.TimeOfDay.Minutes &&
                             now.Second == 0;
            }

            if (shouldFire)
            {
                string key = r.Id + "_" + now.ToString("yyyyMMddHHmm");
                if (_firedReminders.Add(key))
                {
                    string msg = r.Title;
                    if (!string.IsNullOrEmpty(r.Description)) msg += "\n" + r.Description;
                    MessageBox.Show(msg, "提醒", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (!_settings.SnapToEdge) return;
        var sw = SystemParameters.PrimaryScreenWidth;
        var sh = SystemParameters.PrimaryScreenHeight;
        if (this.Left < 20) this.Left = 0;
        if (this.Top < 20) this.Top = 0;
        if (this.Left + this.Width > sw - 20) this.Left = sw - this.Width;
        if (this.Top + this.Height > sh - 20) this.Top = sh - this.Height;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();
        var colorItem = new MenuItem { Header = "选择颜色" };
        colorItem.Click += (_, _) => PickColor();
        menu.Items.Add(colorItem);

        var settingsItem = new MenuItem { Header = "设置" };
        settingsItem.Click += (_, _) =>
        {
            this.ContextMenu = null;
            Dispatcher.BeginInvoke(new Action(OpenSettings));
        };
        menu.Items.Add(settingsItem);
        menu.Items.Add(new Separator());
        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) =>
        {
            _isShuttingDown = true;
            System.Windows.Application.Current.Shutdown();
        };
        menu.Items.Add(exitItem);
        this.ContextMenu = menu;
        base.OnMouseRightButtonDown(e);
    }

    private void OpenSettings()
    {
        var win = new SettingsWindow(_settings, _pluginManager) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _settings = win.Settings;
            ApplySettings();
        }
    }

    private void ApplySettings()
    {
        // Update central settings provider so all components see the new settings
        SettingsProvider.Instance.UpdateSettings(_settings);
        
        RebuildLayout();
        _registry.ApplyAllConfig();

        ApplyBackground();
        try { MainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.BorderColor)); } catch { }
        MainBorder.BorderThickness = new Thickness(_settings.BorderThickness);
        ApplyThemePreset();

        // Window sizing
        switch (_settings.DisplayMode)
        {
            case "minimal":
                this.Width = Math.Max(200, _settings.FontSize * 5 + 40);
                this.Height = Math.Max(60, _settings.FontSize * 1.2 + 40);
                break;
            case "word":
                this.Width = 420;
                this.Height = 160 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0);
                break;
            case "binary":
                this.Width = 340;
                this.Height = 140 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0);
                break;
            case "progress":
                this.Width = 340;
                this.Height = 340 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0);
                break;
            case "analog_premium":
                this.Width = 380;
                this.Height = 380 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0);
                break;
            case "mechanical":
                this.Width = 420;
                this.Height = 420 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0);
                break;
            case "flip":
                this.Width = 380;
                this.Height = 140 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0);
                break;
            default:
                var h = _settings.FontSize * 1.3 + 40;
                if (_settings.ShowDate) h += _settings.DateFontSize + 10;
                if (_settings.LunarEnabled) h += _settings.LunarFontSize + 6;
                if (_settings.WorldClockEnabled) h += 30;
                this.Width = _settings.FontSize * 7 + 40;
                this.Height = h;
                break;
        }

        if (_windowHandle != IntPtr.Zero)
            SetClickThrough(_settings.ClickThrough);
        SetAutoStart(_settings.AutoStart);

        try
        {
            var cultureName = _settings.Language == "en" ? "en-US" : "zh-CN";
            var culture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        }
        catch { }

        _settings.Save();
    }

    private void ApplyBackground()
    {
        // 模拟时钟模式:玻璃圆盘之外完全透明,只显示组件本身的圆盘
        if (_settings.DisplayMode == "progress" || _settings.DisplayMode == "analog_premium" || _settings.DisplayMode == "mechanical")
        {
            MainBorder.Background = Brushes.Transparent;
            return;
        }

        if (_settings.BackgroundType == "gradient")
        {
            try
            {
                var start = (Color)ColorConverter.ConvertFromString(_settings.GradientStartColor);
                var end = (Color)ColorConverter.ConvertFromString(_settings.GradientEndColor);
                var gradient = new LinearGradientBrush(start, end, _settings.GradientAngle);
                gradient.Opacity = _settings.BackgroundOpacity;
                MainBorder.Background = gradient;
            }
            catch
            {
                MainBorder.Background = new SolidColorBrush(Color.FromArgb(
                    (byte)(_settings.BackgroundOpacity * 255), 0, 0, 0));
            }
        }
        else
        {
            MainBorder.Background = new SolidColorBrush(Color.FromArgb(
                (byte)(_settings.BackgroundOpacity * 255), 0, 0, 0));
        }
    }

    private void ApplyThemePreset()
    {
        var currentTheme = _settings.ThemePreset;
        if (currentTheme == "default") return;

        string fontColor, borderColor;
        switch (currentTheme)
        {
            case "dark": fontColor = "#00d4ff"; borderColor = "#00d4ff"; break;
            case "light": fontColor = "#333333"; borderColor = "#007aff"; break;
            case "green": fontColor = "#00ff00"; borderColor = "#00ff00"; break;
            case "blue": fontColor = "#4488ff"; borderColor = "#4488ff"; break;
            default: return;
        }

        _settings.FontColor = fontColor;
        _settings.BorderColor = borderColor;
        try { MainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderColor)); } catch { }
        _registry.ApplyAllConfig();

        // 预设色已应用,重置为 default,避免后续 ApplySettings 再次覆盖用户手动修改的颜色。
        // 这样用户在设置里选完主题预设后,再改颜色就能生效(与右键"选择颜色"行为一致)。
        _settings.ThemePreset = "default";
    }

    private void SetClickThrough(bool enable)
    {
        if (_windowHandle == IntPtr.Zero) return;
        var exStyle = GetWindowLong(_windowHandle, GWL_EXSTYLE);
        if (enable) exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
        else exStyle &= ~(WS_EX_TRANSPARENT | WS_EX_LAYERED);
        SetWindowLong(_windowHandle, GWL_EXSTYLE, exStyle);
    }

    private void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (key == null) return;
            if (enable)
            {
                var fileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (fileName != null) key.SetValue("DesktopClock", fileName);
            }
            else key.DeleteValue("DesktopClock", false);
        }
        catch { }
    }

    private void PickColor()
    {
        System.Drawing.Color current;
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(_settings.FontColor);
            current = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
        }
        catch
        {
            current = System.Drawing.Color.Cyan;
        }

        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = current };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
            _settings.FontColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            _registry.ApplyAllConfig();
            _settings.Save();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 无论是隐藏到托盘还是真正退出,都先保存自由布局位置
        _layoutEngine.SaveFreePositions(MainContainer, _settings.Layout);
        if (!_isShuttingDown) { e.Cancel = true; this.Visibility = Visibility.Hidden; return; }
        if (_hotkeyRegistered) { UnregisterHotKey(_windowHandle, HOTKEY_ID); _hotkeyRegistered = false; }
        _trayIcon?.Dispose();
        _trayIcon = null;
        SavePosition();
        _settings.Save();
        base.OnClosing(e);
    }

    private void SavePosition()
    {
        var path = GetConfigPath();
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, $"{this.Left},{this.Top}");
    }

    private void LoadPosition()
    {
        var path = GetConfigPath();
        if (File.Exists(path))
        {
            var parts = File.ReadAllText(path).Split(',');
            if (parts.Length == 2 && double.TryParse(parts[0], out var left) && double.TryParse(parts[1], out var top))
            {
                this.Left = left; this.Top = top; return;
            }
        }
        this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 20;
        this.Top = 20;
    }

    private static string GetConfigPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopClock", "pos.txt");
    }
}
