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
using DesktopClock.Skins;

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
    private bool _hoverActive;

    private const int HOTKEY_ID = 9000;
    private const int HOTKEY_ID_SKIN_NEXT = 9001;
    private const int WM_HOTKEY = 0x0312;
    private const int WM_NCHITTEST = 0x0084;
    private const int HTTRANSPARENT = -1;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;

    private string[] _skinCycle = Array.Empty<string>();
    private int _skinCycleIndex;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        if (!string.IsNullOrEmpty(App.StartupDisplayMode))
            _settings.DisplayMode = App.StartupDisplayMode;
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

        // 悬停透明度:靠近时不透明,离开时恢复
        this.MouseEnter += (_, _) =>
        {
            if (_settings.HoverOpacityEnabled)
                ApplyHoverOpacity(true);
        };
        this.MouseLeave += (_, _) =>
        {
            if (_settings.HoverOpacityEnabled)
                ApplyHoverOpacity(false);
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

        // 监听系统主题变化
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

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
        _registry.Register(new SysMonComponent());
        _registry.Register(new WeatherComponent());
        _registry.Register(new CountdownComponent());
        _registry.Register(new ScrollingTodoComponent());
        _registry.Register(new MediaInfoComponent());
    }

    private void RebuildLayout()
    {
        // 还原此前被 BackgroundWrapper 包裹的原始表盘组件,确保每次基于干净状态重建
        RestoreWrappedClock();

        // 将 _settings.Components 中保存的配置注入到所有已注册组件
        InjectComponentConfigs();

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
            "analog_skin" => "analog_clock_skin",
            "ribbon" => "ribbon_clock_skin",
            "dual_analog" => "dual_analog_clock_skin",
            _ => "digital_clock"
        };

        // 指针表盘/缎带皮肤/双时区通过 SkinHost 包装,动态注册到组件中心
        if (clockId == "analog_clock_skin" || clockId == "ribbon_clock_skin" || clockId == "dual_analog_clock_skin")
        {
            _registry.Unregister("analog_clock_skin");
            _registry.Unregister("ribbon_clock_skin");
            _registry.Unregister("dual_analog_clock_skin");
            var skin = clockId switch
            {
                "analog_clock_skin" => (IClockSkin)new AnalogClockSkin(),
                "dual_analog_clock_skin" => new DualAnalogClockSkin(),
                _ => new RibbonClockSkin()
            };
            var host = new SkinHost(skin);
            if (!_settings.Components.TryGetValue(clockId, out var cfg))
                cfg = new Models.ComponentConfig();
            // 相册背景开启时,把全局背景参数注入 SkinHost 配置,使其同样生效
            if (_settings.SkinBackgroundEnabled && !string.IsNullOrWhiteSpace(_settings.SkinBackgroundPath))
                InjectBackgroundSettings(cfg);
            // 双时区表盘:注入时区配置
            if (clockId == "dual_analog_clock_skin")
            {
                cfg.Settings["secondTimeZone"] = _settings.DualAnalogTimeZone;
                cfg.Settings["secondLabel"] = _settings.DualAnalogLabel;
            }
            host.Config = cfg;
            host.ApplyConfig();
            _registry.Register(host);
        }
        else
        {
            // 切换回其他表盘时清理 skin 宿主,避免残留
            _registry.Unregister("analog_clock_skin");
            _registry.Unregister("ribbon_clock_skin");
            _registry.Unregister("dual_analog_clock_skin");

            // 相册背景仅适用于指针表盘(analog_skin/ribbon/dual_analog),
            // 切换到其他表盘时自动关闭,避免背景层残留影响显示
            if (_settings.SkinBackgroundEnabled)
            {
                _settings.SkinBackgroundEnabled = false;
                _settings.Save();
            }
        }

        var active = new List<string>();
        if (_settings.ShowDate) active.Add("date");
        if (_settings.LunarEnabled) active.Add("lunar");
        active.Add(clockId);
        if (_settings.WorldClockEnabled) active.Add("world_clock");
        if (_settings.SysMonEnabled) active.Add("sys_mon");
        if (_settings.WeatherEnabled) active.Add("weather");
        if (_settings.CountdownEnabled) active.Add("countdown");
        if (_settings.TodoScrollEnabled) active.Add("scrolling_todo");
        if (_settings.MediaInfoEnabled) active.Add("media_info");

        // Add external plugin components
        foreach (var kvp in _registry.GetAllExternal())
        {
            if (!active.Contains(kvp.Key))
                active.Add(kvp.Key);
        }

        _settings.Layout.ActiveComponents = active;
        _settings.Layout.DatePosition = _settings.DatePosition;
        // 所有模式（包括 progress）都使用 Stack 布局,不再强制使用 Free 布局
        // 如需拖拽定位,用户可在设置中手动切换到 Free 模式

        _layoutEngine.BuildLayout(MainContainer, _registry, _settings.Layout);
    }

    /// <summary>
    /// 当前被 BackgroundWrapper 包裹的原始表盘组件(为 null 表示未包裹)。
    /// </summary>
    private IClockComponent? _wrappedClock;

    /// <summary>
    /// 用 BackgroundWrapper 包裹指定表盘组件并替换注册表中的实例,
    /// 使其在 Stack 布局中显示时叠加相册背景层。
    /// </summary>
    private void WrapClockWithBackground(string clockId)
    {
        var original = _registry.Get(clockId);
        if (original == null) return;

        // 深拷贝原始组件配置,避免把背景参数持久化到 _settings.Components
        if (!_settings.Components.TryGetValue(clockId, out var baseCfg))
            baseCfg = new Models.ComponentConfig();
        var cfg = System.Text.Json.JsonSerializer.Deserialize<Models.ComponentConfig>(
            System.Text.Json.JsonSerializer.Serialize(baseCfg, AppSettings.JsonOpts),
            AppSettings.JsonOpts) ?? baseCfg;
        // 注入相册背景参数(键名与 SkinBackgroundConfig 一致)
        InjectBackgroundSettings(cfg);

        var wrapper = new BackgroundWrapper(original);
        wrapper.Config = cfg;
        wrapper.ApplyConfig();

        _registry.Unregister(clockId);
        _registry.Register(wrapper);
        _wrappedClock = original;
    }

    /// <summary>
    /// 还原被 BackgroundWrapper 包裹的原始表盘组件,把注册表恢复到干净状态。
    /// 在每次 RebuildLayout 开始时调用,确保基于原始组件重新决定是否包裹。
    /// </summary>
    private void RestoreWrappedClock()
    {
        if (_wrappedClock == null) return;
        var id = _wrappedClock.Id;
        _registry.Unregister(id);
        _registry.Register(_wrappedClock);
        _wrappedClock = null;
    }

    /// <summary>
    /// 将 _settings.Components 中保存的配置注入到所有已注册组件,
    /// 并根据 AppSettings 的顶层字段补充各组件所需的运行时参数。
    /// </summary>
    private void InjectComponentConfigs()
    {
        foreach (var comp in _registry.GetAll())
        {
            // 跳过 SkinHost,它的配置在 RebuildLayout 中单独处理
            if (comp is SkinHost) continue;

            if (_settings.Components.TryGetValue(comp.Id, out var savedCfg))
            {
                comp.Config = System.Text.Json.JsonSerializer.Deserialize<Models.ComponentConfig>(
                    System.Text.Json.JsonSerializer.Serialize(savedCfg, AppSettings.JsonOpts),
                    AppSettings.JsonOpts) ?? comp.Config;
            }
        }

        // Weather: 注入经纬度
        var weather = _registry.Get("weather");
        if (weather != null)
        {
            weather.Config.Settings["latitude"] = _settings.WeatherLatitude;
            weather.Config.Settings["longitude"] = _settings.WeatherLongitude;
            weather.Config.Settings["city"] = _settings.WeatherCity;
        }

        // Countdown: 注入目标时间和标签
        var countdown = _registry.Get("countdown");
        if (countdown != null)
        {
            if (_settings.CountdownTarget.HasValue)
                countdown.Config.Settings["target"] = _settings.CountdownTarget.Value.ToString("yyyy-MM-dd HH:mm:ss");
            countdown.Config.Settings["label"] = _settings.CountdownLabel;
        }

        // SysMon: 注入显示开关
        var sysMon = _registry.Get("sys_mon");
        if (sysMon != null)
        {
            sysMon.Config.Settings["showCpu"] = _settings.SysMonShowCpu;
            sysMon.Config.Settings["showMemory"] = _settings.SysMonShowMemory;
            sysMon.Config.Settings["showNetwork"] = _settings.SysMonShowNetwork;
            sysMon.Config.Settings["showBattery"] = _settings.SysMonShowBattery;
        }

        // ScrollingTodo: 注入文字和速度
        var todo = _registry.Get("scrolling_todo");
        if (todo != null)
        {
            todo.Config.Settings["text"] = _settings.TodoScrollText;
            todo.Config.Settings["speed"] = _settings.TodoScrollSpeed;
        }

        // MediaInfo: 注入显示开关
        var media = _registry.Get("media_info");
        if (media != null)
        {
            media.Config.Settings["showArtist"] = _settings.MediaInfoShowArtist;
        }
    }

    /// <summary>
    /// 把全局相册背景参数(imagePath/opacity/blur/mode)注入到指定组件配置字典。
    /// SkinBackgroundConfig.FromDictionary 会读取这些键。
    /// </summary>
    private void InjectBackgroundSettings(Models.ComponentConfig cfg)
    {
        cfg.Settings["imagePath"] = _settings.SkinBackgroundPath;
        cfg.Settings["opacity"] = _settings.SkinBackgroundOpacity;
        cfg.Settings["blur"] = _settings.SkinBackgroundBlur;
        cfg.Settings["mode"] = _settings.SkinBackgroundStretch;
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon();
        try
        {
            _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                Environment.ProcessPath ?? System.Windows.Application.ResourceAssembly.Location);
        }
        catch
        {
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        }
        var instanceLabel = AppSettings.CurrentInstanceId > 0 ? $" [实例{AppSettings.CurrentInstanceId}]" : "";
        _trayIcon.Text = $"桌面时钟{instanceLabel}";
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
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_ID)
            {
                ToggleVisibility();
                handled = true;
            }
            else if (id == HOTKEY_ID_SKIN_NEXT)
            {
                CycleSkin();
                handled = true;
            }
        }
        // 穿透模式下,鼠标事件默认穿过窗口;按住 Ctrl 时允许交互
        if (_settings.ClickThrough && msg == WM_NCHITTEST)
        {
            bool ctrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            if (!ctrlDown)
            {
                handled = true;
                return (IntPtr)HTTRANSPARENT;
            }
        }
        return IntPtr.Zero;
    }

    private void RegisterGlobalHotkey()
    {
        if (_windowHandle == IntPtr.Zero) return;
        if (_hotkeyRegistered)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_SKIN_NEXT);
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
            // 全局切换表盘快捷键: Ctrl+Shift+S
            RegisterHotKey(_windowHandle, HOTKEY_ID_SKIN_NEXT, 0x0002 | 0x0004, (uint)KeyInterop.VirtualKeyFromKey(Key.S));
        }
        catch { }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _registry.UpdateAll(DateTime.Now);
        UpdateChime();
        CheckReminders();
        CheckAodState();
        CheckAutoSwitch();
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
        try { reminders = System.Text.Json.JsonSerializer.Deserialize<List<ReminderItem>>(_settings.RemindersJson, AppSettings.JsonOpts) ?? new(); }
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

    #region AOD 省电模式

    private bool _aodActive;
    private double _preAodOpacity;
    private bool _preAodShowSeconds;

    private void CheckAodState()
    {
        if (!_settings.AodEnabled) { if (_aodActive) RestoreFromAod(); return; }

        uint idleMs = GetIdleTime();
        bool shouldAod = idleMs > (uint)(_settings.AodIdleMinutes * 60 * 1000);

        if (shouldAod && !_aodActive)
            EnterAod();
        else if (!shouldAod && _aodActive)
            RestoreFromAod();
    }

    private uint GetIdleTime()
    {
        var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
        if (!GetLastInputInfo(ref lii)) return 0;
        return (uint)Environment.TickCount - lii.dwTime;
    }

    private void EnterAod()
    {
        _aodActive = true;
        _preAodOpacity = this.Opacity;
        _preAodShowSeconds = _settings.ShowSeconds;

        // 降低整体窗口亮度
        this.Opacity = 0.35;
        // 隐藏秒针
        _settings.ShowSeconds = false;
        SettingsProvider.Instance.UpdateSettings(_settings);
        _registry.ApplyAllConfig();
    }

    private void RestoreFromAod()
    {
        _aodActive = false;
        this.Opacity = _preAodOpacity > 0 ? _preAodOpacity : 1.0;
        _settings.ShowSeconds = _preAodShowSeconds;
        SettingsProvider.Instance.UpdateSettings(_settings);
        _registry.ApplyAllConfig();
    }

    #endregion

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (!_settings.SnapToEdge) return;
        SnapWindowToEdges();
    }

    private void SnapWindowToEdges()
    {
        const double snapDist = 15;
        var sw = SystemParameters.PrimaryScreenWidth;
        var sh = SystemParameters.PrimaryScreenHeight;

        // 四边吸附
        if (Math.Abs(this.Left) < snapDist) this.Left = 0;
        if (Math.Abs(this.Top) < snapDist) this.Top = 0;
        if (Math.Abs(this.Left + this.Width - sw) < snapDist) this.Left = sw - this.Width;
        if (Math.Abs(this.Top + this.Height - sh) < snapDist) this.Top = sh - this.Height;

        // 四角吸附(对角线方向)
        if (Math.Abs(this.Left) < snapDist && Math.Abs(this.Top) < snapDist)
        { this.Left = 0; this.Top = 0; }
        if (Math.Abs(this.Left + this.Width - sw) < snapDist && Math.Abs(this.Top) < snapDist)
        { this.Left = sw - this.Width; this.Top = 0; }
        if (Math.Abs(this.Left) < snapDist && Math.Abs(this.Top + this.Height - sh) < snapDist)
        { this.Left = 0; this.Top = sh - this.Height; }
        if (Math.Abs(this.Left + this.Width - sw) < snapDist && Math.Abs(this.Top + this.Height - sh) < snapDist)
        { this.Left = sw - this.Width; this.Top = sh - this.Height; }
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();

        // 表盘预设快速切换
        var presetMenu = new MenuItem { Header = "切换表盘" };
        var presets = new (string name, string mode)[]
        {
            ("数字", "digital"), ("翻转", "flip"), ("二进制", "binary"),
            ("模拟时钟", "progress"), ("超精美模拟", "analog_premium"),
            ("机械时钟", "mechanical"), ("指针表盘", "analog_skin"),
            ("双时区指针", "dual_analog"), ("缎带流光", "ribbon"), ("极简", "minimal")
        };
        foreach (var (name, mode) in presets)
        {
            var item = new MenuItem { Header = name };
            item.Click += (_, _) => SwitchDisplayMode(mode);
            presetMenu.Items.Add(item);
        }
        menu.Items.Add(presetMenu);
        menu.Items.Add(new Separator());

        // 置顶切换
        var topmostItem = new MenuItem { Header = this.Topmost ? "取消置顶" : "窗口置顶" };
        topmostItem.Click += (_, _) =>
        {
            this.Topmost = !this.Topmost;
        };
        menu.Items.Add(topmostItem);
        menu.Items.Add(new Separator());

        // 简易可视化编辑器入口:切换自由布局编辑模式
        var editModeItem = new MenuItem { Header = _settings.Layout.Mode == "free" ? "退出编辑模式" : "编辑布局模式" };
        editModeItem.Click += (_, _) => ToggleEditMode();
        menu.Items.Add(editModeItem);

        menu.Items.Add(new Separator());

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

        var restartItem = new MenuItem { Header = "重启程序" };
        restartItem.Click += (_, _) => RestartApp();
        menu.Items.Add(restartItem);
        menu.Items.Add(new Separator());

        var exportItem = new MenuItem { Header = "导出配置" };
        exportItem.Click += (_, _) => ExportSkinProfile();
        menu.Items.Add(exportItem);

        var importItem = new MenuItem { Header = "导入配置" };
        importItem.Click += (_, _) => ImportSkinProfile();
        menu.Items.Add(importItem);
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

    private void SwitchDisplayMode(string mode)
    {
        _settings.DisplayMode = mode;
        ApplySettings();
        _settings.Save();
    }

    private void ToggleEditMode()
    {
        _settings.Layout.Mode = _settings.Layout.Mode == "free" ? "stack" : "free";
        ApplySettings();
        _settings.Save();
        // 显示提示
        var msg = _settings.Layout.Mode == "free" ? "已进入编辑模式，可拖拽组件调整位置" : "已退出编辑模式，布局已保存";
        _trayIcon?.ShowBalloonTip(2000, "桌面时钟", msg, System.Windows.Forms.ToolTipIcon.Info);
    }

    private void RestartApp()
    {
        _isShuttingDown = true;
        var exe = Environment.ProcessPath ?? System.Windows.Application.ResourceAssembly.Location;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
        {
            UseShellExecute = true
        });
        System.Windows.Application.Current.Shutdown();
    }

    private void ExportSkinProfile()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "表盘配置|*.dskin",
            DefaultExt = "dskin",
            FileName = $"{_settings.DisplayMode}_profile.dskin"
        };
        if (dialog.ShowDialog() != true) return;

        var profile = new
        {
            Name = _settings.DisplayMode,
            DisplayMode = _settings.DisplayMode,
            FontColor = _settings.FontColor,
            BorderColor = _settings.BorderColor,
            FontSize = _settings.FontSize,
            FontFamily = _settings.FontFamily,
            BackgroundType = _settings.BackgroundType,
            BackgroundOpacity = _settings.BackgroundOpacity,
            GradientStartColor = _settings.GradientStartColor,
            GradientEndColor = _settings.GradientEndColor,
            GradientAngle = _settings.GradientAngle,
            BorderThickness = _settings.BorderThickness,
            ShowSeconds = _settings.ShowSeconds,
            Use24Hour = _settings.Use24Hour,
            ShowDate = _settings.ShowDate,
            DateColor = _settings.DateColor,
            DateFontSize = _settings.DateFontSize,
            DateFontFamily = _settings.DateFontFamily,
            DatePosition = _settings.DatePosition,
            SkinBackgroundEnabled = _settings.SkinBackgroundEnabled,
            SkinBackgroundPath = _settings.SkinBackgroundPath,
            SkinBackgroundOpacity = _settings.SkinBackgroundOpacity,
            SkinBackgroundBlur = _settings.SkinBackgroundBlur,
            SkinBackgroundStretch = _settings.SkinBackgroundStretch,
            Components = _settings.Components
        };

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(profile, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
            File.WriteAllText(dialog.FileName, json);
            MessageBox.Show("配置已导出", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportSkinProfile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "表盘配置|*.dskin",
            DefaultExt = "dskin"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            void TryString(string prop, Action<string> setter)
            {
                if (root.TryGetProperty(prop, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String)
                    setter(el.GetString()!);
            }
            void TryDouble(string prop, Action<double> setter)
            {
                if (root.TryGetProperty(prop, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.Number)
                    setter(el.GetDouble());
            }
            void TryBool(string prop, Action<bool> setter)
            {
                if (root.TryGetProperty(prop, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.True || el.ValueKind == System.Text.Json.JsonValueKind.False)
                    setter(el.GetBoolean());
            }

            TryString("DisplayMode", v => _settings.DisplayMode = v);
            TryString("FontColor", v => _settings.FontColor = v);
            TryString("BorderColor", v => _settings.BorderColor = v);
            TryDouble("FontSize", v => _settings.FontSize = v);
            TryString("FontFamily", v => _settings.FontFamily = v);
            TryString("BackgroundType", v => _settings.BackgroundType = v);
            TryDouble("BackgroundOpacity", v => _settings.BackgroundOpacity = v);
            TryString("GradientStartColor", v => _settings.GradientStartColor = v);
            TryString("GradientEndColor", v => _settings.GradientEndColor = v);
            TryDouble("GradientAngle", v => _settings.GradientAngle = v);
            TryDouble("BorderThickness", v => _settings.BorderThickness = v);
            TryBool("ShowSeconds", v => _settings.ShowSeconds = v);
            TryBool("Use24Hour", v => _settings.Use24Hour = v);
            TryBool("ShowDate", v => _settings.ShowDate = v);
            TryString("DateColor", v => _settings.DateColor = v);
            TryDouble("DateFontSize", v => _settings.DateFontSize = v);
            TryString("DateFontFamily", v => _settings.DateFontFamily = v);
            TryString("DatePosition", v => _settings.DatePosition = v);
            TryBool("SkinBackgroundEnabled", v => _settings.SkinBackgroundEnabled = v);
            TryString("SkinBackgroundPath", v => _settings.SkinBackgroundPath = v);
            TryDouble("SkinBackgroundOpacity", v => _settings.SkinBackgroundOpacity = v);
            TryDouble("SkinBackgroundBlur", v => _settings.SkinBackgroundBlur = v);
            TryString("SkinBackgroundStretch", v => _settings.SkinBackgroundStretch = v);

            if (root.TryGetProperty("Components", out var compEl))
            {
                var comps = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Models.ComponentConfig>>(compEl.GetRawText(), AppSettings.JsonOpts);
                if (comps != null)
                    foreach (var kvp in comps)
                        _settings.Components[kvp.Key] = kvp.Value;
            }

            ApplySettings();
            _settings.Save();
            MessageBox.Show("配置已导入并应用", "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

        // 注意:不再把旧 SkinHost.Config 回写到 _settings.Components,
        // 因为 SettingsWindow 已通过 SetComponentSetting 保存了最新配置,
        // 回写会用旧的运行时配置覆盖用户新设置的 tickColor 等字段。

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
            case "analog_skin":
                this.Width = 420;
                this.Height = 420 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0);
                break;
            case "ribbon":
                this.Width = 420;
                this.Height = 420 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0);
                break;
            case "dual_analog":
                this.Width = 460;
                this.Height = 280 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0);
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
        // 模拟时钟/缎带/双时区模式:玻璃圆盘之外完全透明,只显示组件本身的圆盘
        if (_settings.DisplayMode == "progress" || _settings.DisplayMode == "analog_premium" || _settings.DisplayMode == "mechanical" || _settings.DisplayMode == "analog_skin" || _settings.DisplayMode == "ribbon" || _settings.DisplayMode == "dual_analog")
        {
            MainBorder.Background = Brushes.Transparent;
            MainBorder.Opacity = 1.0;
            WindowBackdrop.Clear(this);
            return;
        }

        // 应用 Mica/Acrylic 背景效果(Windows 11)
        var backdrop = _settings.BackdropType?.ToLower() switch
        {
            "mica" => BackdropType.Mica,
            "acrylic" => BackdropType.Acrylic,
            "tabbed" => BackdropType.Tabbed,
            _ => BackdropType.None
        };
        if (backdrop != BackdropType.None && WindowBackdrop.IsWindows11())
        {
            // Mica/Acrylic 模式下窗口背景需不透明,由 DWM 绘制
            MainBorder.Background = Brushes.Transparent;
            bool dark = IsSystemDarkMode();
            WindowBackdrop.Apply(this, backdrop, dark);
        }
        else
        {
            WindowBackdrop.Clear(this);
            // 应用悬停透明度状态(如果启用)
            ApplyHoverOpacity(_hoverActive);

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

        ApplyGlobalFilter();
    }

    /// <summary>
    /// 应用悬停透明度效果。鼠标靠近时提高不透明度(更清晰),离开时恢复。
    /// 对透明表盘模式(progress/analog_premium/mechanical/analog_skin)无效。
    /// </summary>
    private void ApplyHoverOpacity(bool hover)
    {
        _hoverActive = hover;
        // 透明表盘模式下 MainBorder 是 Transparent,Opacity 无意义,跳过
        if (_settings.DisplayMode == "progress" || _settings.DisplayMode == "analog_premium"
            || _settings.DisplayMode == "mechanical" || _settings.DisplayMode == "analog_skin"
            || _settings.DisplayMode == "ribbon" || _settings.DisplayMode == "dual_analog")
            return;

        if (_settings.HoverOpacityEnabled)
        {
            double target = hover ? _settings.HoverOpacity : _settings.BackgroundOpacity;
            MainBorder.Opacity = target;
        }
        else
        {
            MainBorder.Opacity = 1.0;
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

    private void OnUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category == Microsoft.Win32.UserPreferenceCategory.General && _settings.FollowSystemTheme)
        {
            Dispatcher.BeginInvoke(new Action(ApplySystemTheme));
        }
    }

    private void ApplySystemTheme()
    {
        bool isDark = IsSystemDarkMode();
        _settings.FontColor = isDark ? "#00d4ff" : "#333333";
        _settings.BorderColor = isDark ? "#00d4ff" : "#007aff";
        try { MainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.BorderColor)); } catch { }
        _registry.ApplyAllConfig();
        _settings.Save();
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
        catch { return false; }
    }

    private void SetClickThrough(bool enable)
    {
        if (_windowHandle == IntPtr.Zero) return;
        // 穿透行为改为由 WndProc WM_NCHITTEST 动态控制,
        // 不再设置 WS_EX_TRANSPARENT(该标志会让窗口完全无法接收任何鼠标消息)。
        var exStyle = GetWindowLong(_windowHandle, GWL_EXSTYLE);
        if (enable) exStyle |= WS_EX_LAYERED;
        else exStyle &= ~WS_EX_LAYERED;
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

    #region 全局滤镜 / 自动切换 / 快捷键切表盘

    private void ApplyGlobalFilter()
    {
        if (!_settings.GlobalFilterEnabled)
        {
            MainBorder.Effect = null;
            MainBorder.OpacityMask = null;
            GrayscaleOverlay.Visibility = Visibility.Collapsed;
            ColorTempOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        // 暗角: 使用径向渐变 OpacityMask 让边缘变透明(透出桌面)
        if (_settings.GlobalFilterVignette > 0)
        {
            double intensity = Math.Clamp(_settings.GlobalFilterVignette, 0, 1);
            var brush = new RadialGradientBrush(
                Color.FromArgb((byte)(255 * (1 - intensity)), 0, 0, 0),
                Color.FromArgb(0, 0, 0, 0))
            {
                RadiusX = 0.7,
                RadiusY = 0.7
            };
            MainBorder.OpacityMask = brush;
        }
        else
        {
            MainBorder.OpacityMask = null;
        }

        // 灰度: 叠加灰色矩形,通过不透明度模拟去色效果
        double gray = Math.Clamp(_settings.GlobalFilterGrayscale, 0, 1);
        if (gray > 0)
        {
            GrayscaleOverlay.Opacity = gray * 0.55;
            GrayscaleOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            GrayscaleOverlay.Visibility = Visibility.Collapsed;
        }

        // 色温: 正数=暖色(橙),负数=冷色(蓝)
        double temp = Math.Clamp(_settings.GlobalFilterColorTemp, -1, 1);
        if (Math.Abs(temp) > 0.01)
        {
            ColorTempOverlay.Visibility = Visibility.Visible;
            if (temp > 0)
            {
                // 暖色
                ColorTempOverlay.Fill = new SolidColorBrush(Color.FromRgb(255, 140, 0));
                ColorTempOverlay.Opacity = temp * 0.28;
            }
            else
            {
                // 冷色
                ColorTempOverlay.Fill = new SolidColorBrush(Color.FromRgb(0, 160, 255));
                ColorTempOverlay.Opacity = Math.Abs(temp) * 0.22;
            }
        }
        else
        {
            ColorTempOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void CheckAutoSwitch()
    {
        if (!_settings.AutoSwitchEnabled) return;
        var now = DateTime.Now;
        string targetMode = now.Hour >= _settings.AutoSwitchDayStartHour && now.Hour < _settings.AutoSwitchNightStartHour
            ? _settings.AutoSwitchDayMode
            : _settings.AutoSwitchNightMode;
        if (_settings.DisplayMode != targetMode)
        {
            _settings.DisplayMode = targetMode;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplySettings();
                _settings.Save();
            }));
        }
    }

    private void CycleSkin()
    {
        if (_skinCycle.Length == 0)
        {
            _skinCycle = new[] { "digital", "flip", "binary", "progress", "analog_premium", "mechanical", "analog_skin", "dual_analog", "ribbon", "minimal" };
        }
        _skinCycleIndex = (_skinCycleIndex + 1) % _skinCycle.Length;
        SwitchDisplayMode(_skinCycle[_skinCycleIndex]);
    }

    #endregion

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 无论是隐藏到托盘还是真正退出,都先保存自由布局位置
        _layoutEngine.SaveFreePositions(MainContainer, _settings.Layout);
        if (!_isShuttingDown) { e.Cancel = true; this.Visibility = Visibility.Hidden; return; }
        if (_hotkeyRegistered)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_SKIN_NEXT);
            _hotkeyRegistered = false;
        }
        _trayIcon?.Dispose();
        _trayIcon = null;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SavePosition();
        _settings.Save();
        base.OnClosing(e);
    }

    private void SavePosition()
    {
        var path = AppSettings.GetPositionFilePath();
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, $"{this.Left},{this.Top}");
    }

    private void LoadPosition()
    {
        var path = AppSettings.GetPositionFilePath();
        if (File.Exists(path))
        {
            var parts = File.ReadAllText(path).Split(',');
            if (parts.Length == 2 && double.TryParse(parts[0], out var left) && double.TryParse(parts[1], out var top))
            {
                this.Left = left; this.Top = top; return;
            }
        }
        // 多实例时错开默认位置,避免完全重叠
        int offset = AppSettings.CurrentInstanceId * 30;
        this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 20 - offset;
        this.Top = 20 + offset;
    }
}
