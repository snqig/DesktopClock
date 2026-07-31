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
    private Services.PointerStyleManager? _pointerStyleManager;

    // 挂件管理器 + 渲染调度器(倒计时等其他挂件共享)
    private Services.WidgetManager? _widgetManager;
    private Render.FrameRenderScheduler? _frameScheduler;

    private const int HOTKEY_ID = 9000;
    private const int HOTKEY_ID_SKIN_NEXT = 9001;
    private const int HOTKEY_ID_COUNTDOWN = 9002;
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
        Logger.Information("[MainWindow] constructor start");
        InitializeComponent();

        _settings = AppSettings.Load();
        if (!string.IsNullOrEmpty(App.StartupDisplayMode))
            _settings.DisplayMode = App.StartupDisplayMode;
        Logger.Information($"[MainWindow] settings loaded, DisplayMode={_settings.DisplayMode}");

        // 初始化指针样式管理器
        InitializePointerStyleManager();

        RegisterComponents();
        Logger.Information("[MainWindow] components registered");
        // 天气组件 IP 自动定位成功后,将新坐标持久化到 AppSettings
        WeatherComponent.LocationAutoDetected += () =>
        {
            try
            {
                var weather = _registry.Get("weather");
                if (weather != null)
                {
                    if (weather.Config.Settings.TryGetValue("latitude", out var la) && la is double lat)
                        _settings.WeatherLatitude = lat;
                    if (weather.Config.Settings.TryGetValue("longitude", out var lo) && lo is double lon)
                        _settings.WeatherLongitude = lon;
                    if (weather.Config.Settings.TryGetValue("city", out var city))
                        _settings.WeatherCity = city?.ToString() ?? _settings.WeatherCity;
                    _settings.Save();
                    Logger.Information("[MainWindow] weather location auto-detected and saved");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[MainWindow] saving auto-located weather coords failed", ex);
            }
        };
        _pluginManager = new PluginManager(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins"), _registry);
        _pluginManager.LoadAll(_settings.Plugins);
        Logger.Information("[MainWindow] plugins loaded");
        ApplySettings();
        Logger.Information("[MainWindow] ApplySettings done");
        // 先设置尺寸再加载位置,否则默认位置用到 NaN 的 Width/Height 会导致窗口不可见
        LoadPosition();
        Logger.Information($"[MainWindow] LoadPosition done, Left={this.Left}, Top={this.Top}, W={this.Width}, H={this.Height}");
        // 强制确保窗口可见,避免 HotkeyHide / Hidden 残留
        if (this.Visibility != Visibility.Visible) this.Visibility = Visibility.Visible;
        if (!this.IsVisible) this.Show();
        Logger.Information($"[MainWindow] visibility ensured, IsVisible={this.IsVisible}");

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
        _timer.Start();

        // 初始化 FrameRenderScheduler + WidgetManager(共享服务)
        InitializeWidgetRuntime();

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

    /// <summary>初始化指针样式管理器并注入到 AnalogClockSkin</summary>
    private void InitializePointerStyleManager()
    {
        _pointerStyleManager = new Services.PointerStyleManager();
        // 数据目录与 settings.json 同目录
        _pointerStyleManager.DataDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopClock");
        _pointerStyleManager.Load();
        // 注入到 AnalogClockSkin 静态属性
        Skins.AnalogClockSkin.StyleManager = _pointerStyleManager;
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
            "cyberpunk" => "cyberpunk_neon_clock_skin",
            // 指针样式编辑器模式:复用 analog_clock_skin 容器,但强制使用编辑器方案
            "pointer_editor" => "analog_clock_skin",
            _ => "digital_clock"
        };
        // 记录是否为"指针样式编辑器"模式,后续用于强制注入 pointerSetId
        bool isPointerEditorMode = _settings.DisplayMode == "pointer_editor";

        // 指针表盘/缎带皮肤/双时区/赛博朋克通过 SkinHost 包装,动态注册到组件中心
        if (clockId == "analog_clock_skin" || clockId == "ribbon_clock_skin" || clockId == "dual_analog_clock_skin" || clockId == "cyberpunk_neon_clock_skin")
        {
            _registry.Unregister("analog_clock_skin");
            _registry.Unregister("ribbon_clock_skin");
            _registry.Unregister("dual_analog_clock_skin");
            _registry.Unregister("cyberpunk_neon_clock_skin");
            var skin = clockId switch
            {
                "analog_clock_skin" => (IClockSkin)new AnalogClockSkin(),
                "dual_analog_clock_skin" => new DualAnalogClockSkin(),
                "cyberpunk_neon_clock_skin" => new CyberpunkNeonSkin(),
                _ => new RibbonClockSkin()
            };
            var host = new SkinHost(skin);
            if (!_settings.Components.TryGetValue(clockId, out var cfg))
                cfg = new Models.ComponentConfig();
            // 注入当前激活的指针方案 ID(如果有)
            if (!string.IsNullOrEmpty(_settings.ActivePointerSetId))
                cfg.Settings["pointerSetId"] = _settings.ActivePointerSetId;
            // 指针样式编辑器模式:强制要求使用编辑器方案,若未设置则给出提示日志
            if (isPointerEditorMode && string.IsNullOrEmpty(_settings.ActivePointerSetId))
            {
                Logger.Warning("[MainWindow] pointer_editor mode enabled but no ActivePointerSetId set, falling back to default hands");
            }
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
            _registry.Unregister("cyberpunk_neon_clock_skin");

            // 相册背景仅适用于指针表盘(analog_skin/ribbon/dual_analog),
            // 切换到其他表盘时自动关闭,避免背景层残留影响显示
            if (_settings.SkinBackgroundEnabled)
            {
                _settings.SkinBackgroundEnabled = false;
                _settings.Save();
            }
        }

        _currentClockId = clockId;

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

        // 同步时钟宽度到滚动组件,使跑马灯区域与时间右边缘对齐
        SyncScrollComponentWidths();
    }

    /// <summary>
    /// 当前被 BackgroundWrapper 包裹的原始表盘组件(为 null 表示未包裹)。
    /// </summary>
    private IClockComponent? _wrappedClock;

    /// <summary>
    /// 当前布局使用的时钟组件 ID,用于宽度同步。
    /// </summary>
    private string _currentClockId = "digital_clock";

    /// <summary>
    /// 同步时钟宽度到滚动组件(待办滚动、系统监控),
    /// 使滚动区域与时间显示右边缘对齐,文字从右到左滚动。
    /// </summary>
    private void SyncScrollComponentWidths()
    {
        var clockComp = _registry.Get(_currentClockId) ?? _wrappedClock;
        if (clockComp?.View is not FrameworkElement clockView) return;

        void UpdateWidths()
        {
            double w = clockView.ActualWidth;
            if (w <= 0) return;

            if (_registry.Get("scrolling_todo") is ScrollingTodoComponent todo)
                todo.SetScrollWidth(w);
            if (_registry.Get("sys_mon") is SysMonComponent sysMon)
                sysMon.SetScrollWidth(w);
        }

        // 立即同步一次(ActualWidth 可能此时为 0,SizeChanged 时再触发)
        UpdateWidths();
        clockView.SizeChanged -= (_, _) => UpdateWidths();
        clockView.SizeChanged += (_, _) => UpdateWidths();
    }

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

        // Weather: 注入经纬度 + 显示样式配置
        var weather = _registry.Get("weather");
        if (weather != null)
        {
            weather.Config.Settings["latitude"] = _settings.WeatherLatitude;
            weather.Config.Settings["longitude"] = _settings.WeatherLongitude;
            weather.Config.Settings["city"] = _settings.WeatherCity;
            weather.Config.Settings["fontSize"] = _settings.WeatherFontSize;
            weather.Config.Settings["detailFontSize"] = _settings.WeatherDetailFontSize;
            weather.Config.Settings["fontColor"] = _settings.WeatherFontColor;
            weather.Config.Settings["detailColor"] = _settings.WeatherDetailColor;
            weather.Config.Settings["alignment"] = _settings.WeatherAlignment;
            weather.Config.Position = _settings.WeatherPosition;
            weather.ApplyConfig();
        }

        // Countdown: 注入目标时间、样式和显示配置
        // 注意:CountdownTarget 是 UTC,CountdownComponent 的计时器每帧传入本地 now,
        // 因此存入配置前必须先转为本地时间,避免时区偏移导致倒计时提前或延后结束。
        var countdown = _registry.Get("countdown");
        if (countdown != null)
        {
            if (_settings.CountdownTarget.HasValue)
                countdown.Config.Settings["target"] = _settings.CountdownTarget.Value.ToLocalTime();
            countdown.Config.Settings["label"] = _settings.CountdownLabel;
            countdown.Config.Settings["fontColor"] = _settings.CountdownFontColor;
            countdown.Config.Settings["fontSize"] = _settings.CountdownFontSize;
            countdown.Config.Settings["fontFamily"] = _settings.CountdownFontFamily;
            countdown.Config.Settings["displayMode"] = _settings.CountdownDisplayMode; // days/time
            countdown.Config.Settings["stopAtZero"] = _settings.CountdownStopAtZero;
            countdown.Config.Settings["showTitle"] = _settings.CountdownShowTitle;
        }

        // SysMon: 注入显示开关、字体样式
        var sysMon = _registry.Get("sys_mon");
        if (sysMon != null)
        {
            sysMon.Config.Settings["showCpu"] = _settings.SysMonShowCpu;
            sysMon.Config.Settings["showMemory"] = _settings.SysMonShowMemory;
            sysMon.Config.Settings["showNetwork"] = _settings.SysMonShowNetwork;
            sysMon.Config.Settings["showBattery"] = _settings.SysMonShowBattery;
            sysMon.Config.Settings["fontColor"] = _settings.SysMonFontColor;
            sysMon.Config.Settings["fontSize"] = _settings.SysMonFontSize;
            sysMon.Config.Settings["fontFamily"] = _settings.SysMonFontFamily;
        }

        // ScrollingTodo: 注入文字、速度和字体样式
        var todo = _registry.Get("scrolling_todo");
        if (todo != null)
        {
            todo.Config.Settings["text"] = _settings.TodoScrollText;
            todo.Config.Settings["speed"] = _settings.TodoScrollSpeed;
            todo.Config.Settings["fontColor"] = _settings.TodoScrollFontColor;
            todo.Config.Settings["fontSize"] = _settings.TodoScrollFontSize;
            todo.Config.Settings["fontFamily"] = _settings.TodoScrollFontFamily;
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

        // 切换鼠标穿透
        var clickThroughItem = menu.Items.Add(_settings.ClickThrough ? "取消鼠标穿透" : "鼠标穿透") as System.Windows.Forms.ToolStripMenuItem;
        clickThroughItem!.Click += (_, _) =>
        {
            _settings.ClickThrough = !_settings.ClickThrough;
            SetClickThrough(_settings.ClickThrough);
            clickThroughItem.Text = _settings.ClickThrough ? "取消鼠标穿透" : "鼠标穿透";
            _settings.Save();
        };

        // 切换置顶
        var topmostItem = menu.Items.Add(this.Topmost ? "取消置顶" : "窗口置顶") as System.Windows.Forms.ToolStripMenuItem;
        topmostItem!.Click += (_, _) =>
        {
            this.Topmost = !this.Topmost;
            topmostItem.Text = this.Topmost ? "取消置顶" : "窗口置顶";
        };

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // 开机自启快捷开关
        var autoStartItem = menu.Items.Add(_settings.AutoStart ? "取消开机自启" : "开机自启") as System.Windows.Forms.ToolStripMenuItem;
        autoStartItem!.Click += (_, _) =>
        {
            _settings.AutoStart = !_settings.AutoStart;
            try { App.SetAutoStart(_settings.AutoStart); } catch { }
            autoStartItem.Text = _settings.AutoStart ? "取消开机自启" : "开机自启";
            _settings.Save();
        };

        menu.Items.Add("倒计时显示/隐藏", null, (_, _) => ToggleCountdownVisibility());
        menu.Items.Add("设置", null, (_, _) => OpenSettings());
        menu.Items.Add("重启程序", null, (_, _) => RestartApp());
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
            else if (id == HOTKEY_ID_COUNTDOWN)
            {
                ToggleCountdownVisibility();
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
            UnregisterHotKey(_windowHandle, HOTKEY_ID_COUNTDOWN);
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
            // 全局倒计时显示/隐藏快捷键: Ctrl+Shift+D (或从 _settings.HotkeyCountdown 读取)
            try
            {
                var hotkeyCd = string.IsNullOrEmpty(_settings.HotkeyCountdown) ? "Ctrl+Shift+D" : _settings.HotkeyCountdown;
                uint modCd = 0, vkCd = 0;
                foreach (var part in hotkeyCd.Split('+'))
                {
                    switch (part.Trim().ToLower())
                    {
                        case "ctrl": modCd |= 0x0002; break;
                        case "alt": modCd |= 0x0001; break;
                        case "shift": modCd |= 0x0004; break;
                        case "win": modCd |= 0x0008; break;
                        default:
                            var key = (Key)Enum.Parse(typeof(Key), part.Trim(), true);
                            vkCd = (uint)KeyInterop.VirtualKeyFromKey(key);
                            break;
                    }
                }
                if (modCd != 0 && vkCd != 0)
                {
                    RegisterHotKey(_windowHandle, HOTKEY_ID_COUNTDOWN, modCd, vkCd);
                }
            }
            catch { Logger.Warning("[Hotkey] countdown hotkey register failed"); }
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
        CheckNightDim();
    }

    private bool _nightDimActive;

    /// <summary>
    /// 夜间自动降低透明度:
    /// 在 NightDimStartHour ~ NightDimEndHour 时段内,
    /// 将 MainBorder.Opacity 降低到 NightDimOpacity(范围 0~1)。
    /// 跨午夜场景(22~6)已正确处理。
    /// </summary>
    private void CheckNightDim()
    {
        if (!_settings.NightDimEnabled)
        {
            if (_nightDimActive)
            {
                _nightDimActive = false;
                ApplyOpacity(false);
            }
            return;
        }

        var hour = DateTime.Now.Hour;
        var start = _settings.NightDimStartHour;
        var end = _settings.NightDimEndHour;
        // 跨午夜:start > end,如 22~6
        bool inNight = start <= end
            ? (hour >= start && hour < end)
            : (hour >= start || hour < end);

        if (inNight != _nightDimActive)
        {
            _nightDimActive = inNight;
            ApplyOpacity(_nightDimActive);
            Logger.Information($"[NightDim] active={_nightDimActive}, opacity={(_nightDimActive ? _settings.NightDimOpacity : _settings.BackgroundOpacity)}");
        }
    }

    /// <summary>应用透明度(nightDim=true 时使用夜间透明度,否则用常规透明度)。</summary>
    private void ApplyOpacity(bool nightDim)
    {
        try
        {
            // 避免悬停透明度逻辑干扰:只在非悬停状态应用
            if (_hoverActive) return;
            var target = nightDim
                ? Math.Max(0.05, _settings.NightDimOpacity)
                : Math.Max(0.1, _settings.BackgroundOpacity);
            if (MainBorder != null) MainBorder.Opacity = target;
        }
        catch { }
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
        // 吸附距离从设置读取,默认 20px,范围 5~100
        var snapDist = (double)Math.Clamp(_settings.SnapDistance, 5, 100);
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
            ("双时区指针", "dual_analog"), ("缎带流光", "ribbon"),
            ("赛博朋克霓虹发光指针", "cyberpunk"), ("极简", "minimal")
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

        // 指针样式编辑器入口
        var pointerEditorItem = new MenuItem { Header = "指针样式编辑器" };
        pointerEditorItem.Click += (_, _) => OpenPointerStyleEditor();
        menu.Items.Add(pointerEditorItem);

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

    /// <summary>打开指针样式编辑器</summary>
    private void OpenPointerStyleEditor()
    {
        if (_pointerStyleManager == null) return;
        var editor = new PointerStyleEditor(_pointerStyleManager) { Owner = this };
        editor.OnApply = (set) =>
        {
            // 保存方案 ID 到 AppSettings
            _settings.ActivePointerSetId = set.Id;

            // 同步到所有可能的皮肤组件配置
            var skinIds = new[] { "analog_clock_skin", "ribbon_clock_skin", "dual_analog_clock_skin", "cyberpunk_neon_clock_skin" };
            foreach (var id in skinIds)
            {
                if (_settings.Components.TryGetValue(id, out var cfg))
                    cfg.Settings["pointerSetId"] = set.Id;
            }

            // 也同步到 analog_clock 组件（老式 key）
            if (_settings.Components.TryGetValue("analog_clock", out var oldCfg))
                oldCfg.Settings["pointerSetId"] = set.Id;

            _settings.Save();

            // 遍历注册表刷新所有皮肤宿主(包括 BackgroundWrapper 包裹的)
            RefreshAllSkinHosts(set.Id);
        };
        editor.Show();
    }

    /// <summary>
    /// 遍历注册表,找到所有 SkinHost(包括 BackgroundWrapper 包裹的),
    /// 设置 pointerSetId 并调用 ApplyConfig 刷新指针样式。
    /// </summary>
    private void RefreshAllSkinHosts(string setId)
    {
        foreach (var comp in _registry.GetAll())
        {
            SkinHost? host = null;

            if (comp is SkinHost directHost)
                host = directHost;
            else if (comp is BackgroundWrapper wrapper && wrapper.Inner is SkinHost wrappedHost)
                host = wrappedHost;

            if (host == null) continue;

            host.Config.Settings["pointerSetId"] = setId;
            host.ApplyConfig();
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
        double extraHeight = 0;
        if (_settings.CountdownEnabled) extraHeight += Math.Max(20, _settings.CountdownFontSize + 8);
        if (_settings.TodoScrollEnabled) extraHeight += Math.Max(20, _settings.TodoScrollFontSize + 8);
        if (_settings.SysMonEnabled) extraHeight += Math.Max(20, _settings.SysMonFontSize + 8);
        if (_settings.WeatherEnabled) extraHeight += Math.Max(20, _settings.WeatherFontSize + 8);
        if (_settings.MediaInfoEnabled) extraHeight += 24;

        switch (_settings.DisplayMode)
        {
            case "minimal":
                this.Width = Math.Max(200, _settings.FontSize * 5 + 40);
                this.Height = Math.Max(60, _settings.FontSize * 1.2 + 40) + extraHeight;
                break;
            case "word":
                this.Width = 420;
                this.Height = 160 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0) + extraHeight;
                break;
            case "binary":
                this.Width = 340;
                this.Height = 140 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0) + extraHeight;
                break;
            case "progress":
                this.Width = 340;
                this.Height = 340 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0) + extraHeight;
                break;
            case "analog_premium":
                this.Width = 380;
                this.Height = 380 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0) + extraHeight;
                break;
            case "mechanical":
                this.Width = 420;
                this.Height = 420 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0) + extraHeight;
                break;
            case "analog_skin":
                this.Width = 420;
                this.Height = 420 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0) + extraHeight;
                break;
            case "ribbon":
                this.Width = 420;
                this.Height = 420 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0) + extraHeight;
                break;
            case "cyberpunk":
                this.Width = 420;
                this.Height = 420 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0) + extraHeight;
                break;
            case "dual_analog":
                this.Width = 460;
                this.Height = 280 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0) + extraHeight;
                break;
            case "flip":
                this.Width = 380;
                this.Height = 140 + (_settings.LunarEnabled ? _settings.LunarFontSize + 6 : 0) + extraHeight;
                break;
            default:
                var h = _settings.FontSize * 1.3 + 40;
                if (_settings.ShowDate) h += _settings.DateFontSize + 10;
                if (_settings.LunarEnabled) h += _settings.LunarFontSize + 6;
                if (_settings.WorldClockEnabled) h += 30;
                h += extraHeight;
                this.Width = _settings.FontSize * 7 + 40;
                this.Height = h;
                break;
        }

        if (_windowHandle != IntPtr.Zero)
            SetClickThrough(_settings.ClickThrough);
        SetAutoStart(_settings.AutoStart);

        // 倒计时挂件:启用/关闭 + 刷新样式/目标/颜色/字体配置
        try
        {
            if (_settings.CountdownEnabled)
            {
                TryStartCountdown();
                RefreshCountdownConfig();
            }
            else
            {
                TryStopCountdown();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("[ApplySettings] countdown sync failed", ex);
        }

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
        // 1) Mica/Acrylic 背景效果优先(Windows 11)
        var backdrop = _settings.BackdropType?.ToLower() switch
        {
            "mica" => BackdropType.Mica,
            "acrylic" => BackdropType.Acrylic,
            "tabbed" => BackdropType.Tabbed,
            _ => BackdropType.None
        };
        if (backdrop != BackdropType.None && WindowBackdrop.IsWindows11())
        {
            // Mica/Acrylic 模式下窗口背景由 DWM 绘制
            MainBorder.Background = Brushes.Transparent;
            MainBorder.Opacity = 1.0;
            bool dark = IsSystemDarkMode();
            WindowBackdrop.Apply(this, backdrop, dark);
            ApplyGlobalFilter();
            return;
        }

        WindowBackdrop.Clear(this);

        // 2) 渐变背景:用户明确选择时才绘制
        if (_settings.BackgroundType == "gradient")
        {
            try
            {
                var start = (Color)ColorConverter.ConvertFromString(_settings.GradientStartColor);
                var end = (Color)ColorConverter.ConvertFromString(_settings.GradientEndColor);
                var gradient = new LinearGradientBrush(start, end, _settings.GradientAngle);
                gradient.Opacity = _settings.BackgroundOpacity;
                MainBorder.Background = gradient;
                MainBorder.Opacity = 1.0;
                ApplyHoverOpacity(_hoverActive);
                ApplyGlobalFilter();
                return;
            }
            catch { }
        }

        // 3) 默认:所有模式(数字/翻转/二进制/模拟/赛博朋克/极简等)窗口背景完全透明,
        //    只显示组件本身内容,避免黑色底板影响外观
        MainBorder.Background = Brushes.Transparent;
        MainBorder.Opacity = 1.0;
        ApplyGlobalFilter();
    }

    /// <summary>
    /// 应用悬停透明度效果。鼠标靠近时提高不透明度(更清晰),离开时恢复。
    /// 仅在渐变背景模式下生效;透明背景下改 Opacity 会让组件内容一起消失,故跳过。
    /// </summary>
    private void ApplyHoverOpacity(bool hover)
    {
        _hoverActive = hover;
        // 透明背景(默认)或 Mica/Acrylic 模式下 MainBorder 是 Transparent,
        // 改 Opacity 会让时钟数字/表盘内容一起变透明,因此只在渐变背景时生效
        if (_settings.BackgroundType != "gradient") return;

        if (_settings.HoverOpacityEnabled)
        {
            double target = hover ? _settings.HoverOpacity : Math.Max(0.1, _settings.BackgroundOpacity);
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

    // ==================== 挂件运行时: WidgetManager + FrameRenderScheduler ====================
    /// <summary>
    /// 初始化多挂件共享的运行时:
    /// 1. FrameRenderScheduler 统一调度所有需要高频刷新的挂件(倒计时、秒针等)
    /// 2. WidgetManager 统一管理各挂件的创建、显示、隐藏、位置持久化
    /// 3. 注册 CountdownWidget 工厂,根据 CountdownEnabled 自动启动
    /// </summary>
    private void InitializeWidgetRuntime()
    {
        try
        {
            // 渲染调度器:Interactive 模式 ~30 FPS 用于倒计时秒级刷新
            _frameScheduler = new Render.FrameRenderScheduler(Render.FrameMode.Interactive);
            _frameScheduler.Start();

            // 窗口管理器(基于 Win32 NativeMethods)
            var windowManager = new Core.WindowManager();

            _widgetManager = new Services.WidgetManager(windowManager, _frameScheduler);

            // 注册倒计时挂件工厂
            _widgetManager.RegisterFactory("countdown", () =>
            {
                var widget = new Views.Widgets.CountdownWindow();
                // 应用持久化配置(位置、尺寸、置顶等)
                if (!double.IsNaN(_settings.CountdownWindowLeft) && !double.IsNaN(_settings.CountdownWindowTop))
                {
                    widget.WindowStartupLocation = WindowStartupLocation.Manual;
                    widget.Left = _settings.CountdownWindowLeft;
                    widget.Top = _settings.CountdownWindowTop;
                }
                widget.Width = _settings.CountdownWindowWidth;
                widget.Height = _settings.CountdownWindowHeight;
                widget.Topmost = _settings.CountdownTopmost;

                // 注入配置(支持多任务轮播)
                widget.ApplyConfigMulti(_settings);

                // FrameRenderScheduler 订阅倒计时 OnFrame 刷新
                _frameScheduler?.Subscribe(widget.OnFrame);

                // 窗口关闭前保存位置
                widget.Closing += (_, e) =>
                {
                    if (!_isShuttingDown)
                    {
                        e.Cancel = true;
                        widget.Visibility = Visibility.Hidden;
                        SaveCountdownWindowPos(widget);
                    }
                    else
                    {
                        SaveCountdownWindowPos(widget);
                    }
                };

                // 关闭时解除订阅
                widget.Unloaded += (_, _) => _frameScheduler?.Unsubscribe(widget.OnFrame);

                return widget;
            }, enabledByDefault: _settings.CountdownEnabled);

            Logger.Information("[WidgetRuntime] initialized, countdownEnabled=" + _settings.CountdownEnabled);

            // 若配置中已启用,立即启动倒计时挂件
            if (_settings.CountdownEnabled)
            {
                TryStartCountdown();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("[WidgetRuntime] init failed", ex);
        }
    }

    private void SaveCountdownWindowPos(Window widget)
    {
        try
        {
            _settings.CountdownWindowLeft = widget.Left;
            _settings.CountdownWindowTop = widget.Top;
            _settings.CountdownWindowWidth = widget.Width;
            _settings.CountdownWindowHeight = widget.Height;
            _settings.CountdownTopmost = widget.Topmost;
            _settings.CountdownWindowOpacity = widget.Opacity;
            _settings.Save();
        }
        catch { }
    }

    /// <summary>启动倒计时挂件(已运行则无操作)。</summary>
    public void TryStartCountdown()
    {
        try
        {
            if (_widgetManager == null) return;
            _widgetManager.Start("countdown");
            Logger.Information($"[Countdown] started, target={_settings.CountdownTarget?.ToLocalTime():O}, label={_settings.CountdownLabel}");
        }
        catch (Exception ex)
        {
            Logger.Error("[Countdown] start failed", ex);
        }
    }

    /// <summary>停止倒计时挂件并释放。</summary>
    public void TryStopCountdown()
    {
        try
        {
            if (_widgetManager == null) return;
            _widgetManager.Stop("countdown");
            Logger.Information("[Countdown] stopped");
        }
        catch (Exception ex)
        {
            Logger.Error("[Countdown] stop failed", ex);
        }
    }

    /// <summary>倒计时显示/隐藏切换(托盘菜单 + 热键共用)。</summary>
    public void ToggleCountdownVisibility()
    {
        if (_widgetManager == null) return;
        var layered = _widgetManager.WindowManager.Get("countdown");
        if (layered == null)
        {
            TryStartCountdown();
            return;
        }
        // LayeredWindow.Window 为实际 WPF Window 句柄
        var wpfWin = layered.Window;
        if (wpfWin.Visibility == Visibility.Visible)
        {
            SaveCountdownWindowPos(wpfWin);
            _widgetManager.Hide("countdown");
        }
        else
        {
            _widgetManager.Show("countdown");
        }
    }

    /// <summary>应用新的倒计时配置到已运行的挂件(支持多任务)。</summary>
    public void RefreshCountdownConfig()
    {
        if (_widgetManager == null) return;
        var layered = _widgetManager.WindowManager.Get("countdown");
        if (layered?.Window is Views.Widgets.CountdownWindow cw)
        {
            cw.ApplyConfigMulti(_settings);
        }
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
            _skinCycle = new[] { "digital", "flip", "binary", "progress", "analog_premium", "mechanical", "analog_skin", "dual_analog", "ribbon", "cyberpunk", "minimal" };
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
            UnregisterHotKey(_windowHandle, HOTKEY_ID_COUNTDOWN);
            _hotkeyRegistered = false;
        }
        // 释放 WidgetManager + FrameRenderScheduler
        try
        {
            _widgetManager?.StopAll();
            _widgetManager = null;
            _frameScheduler?.Stop();
            _frameScheduler?.Dispose();
            _frameScheduler = null;
        }
        catch { }
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
        var sw = SystemParameters.PrimaryScreenWidth;
        var sh = SystemParameters.PrimaryScreenHeight;
        // 先保证自己的尺寸有效,避免 NaN
        if (double.IsNaN(this.Width) || this.Width < 100) this.Width = 400;
        if (double.IsNaN(this.Height) || this.Height < 100) this.Height = 200;

        var path = AppSettings.GetPositionFilePath();
        if (File.Exists(path))
        {
            var parts = File.ReadAllText(path).Split(',');
            if (parts.Length == 2 && double.TryParse(parts[0], out var left) && double.TryParse(parts[1], out var top))
            {
                // 夹紧到屏幕范围内,避免窗口在屏幕外看不见
                left = Math.Clamp(left, 0, Math.Max(0, sw - this.Width));
                top = Math.Clamp(top, 0, Math.Max(0, sh - this.Height));
                this.Left = left; this.Top = top;
                return;
            }
        }
        // 多实例时错开默认位置,避免完全重叠
        int offset = AppSettings.CurrentInstanceId * 30;
        this.Left = Math.Max(0, sw - this.Width - 20 - offset);
        this.Top = 20 + offset;
    }
}
