using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DesktopClock;

public class AppSettings
{
    public int Version { get; set; } = 2;

    // === Old flat properties (backward compatible) ===
    public double FontSize { get; set; } = 64;
    public double BackgroundOpacity { get; set; } = 0.85;
    public string FontColor { get; set; } = "#00d4ff";
    public string FontFamily { get; set; } = "DS-Digital";

    public bool ShowDate { get; set; } = true;
    public string DateFontFamily { get; set; } = "DS-Digital";
    public double DateFontSize { get; set; } = 16;
    public string DateColor { get; set; } = "#00FF00";
    public string DatePosition { get; set; } = "top";

    public bool Use24Hour { get; set; } = true;
    public bool ShowSeconds { get; set; } = true;
    public string DisplayMode { get; set; } = "digital";
    public string BackgroundType { get; set; } = "solid";
    public string GradientStartColor { get; set; } = "#1a1a2e";
    public string GradientEndColor { get; set; } = "#16213e";
    public double GradientAngle { get; set; } = 45;
    public string BorderColor { get; set; } = "#00d4ff";
    public double BorderThickness { get; set; } = 1;
    public bool LunarEnabled { get; set; } = false;
    public bool ShowSolarTerm { get; set; } = true;
    public bool ShowZodiac { get; set; } = true;
    public double LunarFontSize { get; set; } = 14;
    public string LunarColor { get; set; } = "#aaaaaa";
    public bool ChimeEnabled { get; set; } = false;
    public bool ReminderEnabled { get; set; } = false;
    public string RemindersJson { get; set; } = "[]";
    public bool WorldClockEnabled { get; set; } = false;
    public string WorldClockTimeZone { get; set; } = "China Standard Time";
    public string HotkeyHide { get; set; } = "Ctrl+H";
    public string HotkeyCountdown { get; set; } = "Ctrl+Shift+D";
    public string Language { get; set; } = "zh";
    public string ThemePreset { get; set; } = "default";
    public bool SnapToEdge { get; set; } = false;
    public int SnapDistance { get; set; } = 20;
    public bool AutoStart { get; set; } = false;
    public bool LockPosition { get; set; } = false;
    public bool ClickThrough { get; set; } = false;

    // === 悬停透明度增强 ===
    public bool HoverOpacityEnabled { get; set; } = false;
    public double HoverOpacity { get; set; } = 1.0;

    // === 夜间自动降低透明度 ===
    public bool NightDimEnabled { get; set; } = false;
    public int NightDimStartHour { get; set; } = 22;
    public int NightDimEndHour { get; set; } = 6;
    public double NightDimOpacity { get; set; } = 0.4;

    // === AOD 省电模式 ===
    public bool AodEnabled { get; set; } = false;
    public int AodIdleMinutes { get; set; } = 5;

    // === 跟随系统主题 ===
    public bool FollowSystemTheme { get; set; } = false;

    // === 相册背景(所有表盘通用) ===
    public bool SkinBackgroundEnabled { get; set; } = false;
    public string SkinBackgroundPath { get; set; } = string.Empty;
    public double SkinBackgroundOpacity { get; set; } = 1.0;
    public double SkinBackgroundBlur { get; set; } = 0;
    public string SkinBackgroundStretch { get; set; } = "UniformToFill";

    // === 窗口背景效果 ===
    public string BackdropType { get; set; } = "none"; // none / mica / acrylic / tabbed

    // === 系统监控 ===
    public bool SysMonEnabled { get; set; } = false;
    public bool SysMonShowCpu { get; set; } = true;
    public bool SysMonShowMemory { get; set; } = true;
    public bool SysMonShowNetwork { get; set; } = false;
    public bool SysMonShowBattery { get; set; } = true;
    public string SysMonFontColor { get; set; } = "#FFD1D1D6";
    public double SysMonFontSize { get; set; } = 12;
    public string SysMonFontFamily { get; set; } = "Consolas, Microsoft YaHei";

    // === 天气 ===
    public bool WeatherEnabled { get; set; } = false;
    public string WeatherCity { get; set; } = "Suzhou";
    public double WeatherLatitude { get; set; } = 31.2989;
    public double WeatherLongitude { get; set; } = 120.5853;
    public double WeatherFontSize { get; set; } = 13;
    public double WeatherDetailFontSize { get; set; } = 11;
    public string WeatherFontColor { get; set; } = "#FFD3D3D3"; // LightGray
    public string WeatherDetailColor { get; set; } = "#FFAAAAAA";
    public string WeatherAlignment { get; set; } = "center"; // left / center / right
    public string WeatherPosition { get; set; } = "bottom"; // top / bottom (ComponentConfig.Position)

    // === 倒计时 ===
    public bool CountdownEnabled { get; set; } = false;
    public DateTime? CountdownTarget { get; set; }
    public string CountdownLabel { get; set; } = "倒计时";

    /// <summary>多任务倒计时列表(P3 增值功能)。空列表时回退到单任务模式。</summary>
    public System.Collections.Generic.List<Models.CountdownTask> CountdownTasks { get; set; } = new();

    /// <summary>多任务轮播间隔(秒),默认 10 秒</summary>
    public int CountdownTaskRotationSeconds { get; set; } = 10;
    public bool CountdownShowTitle { get; set; } = true;
    public string CountdownDisplayMode { get; set; } = "days"; // days / time
    public string CountdownEndAction { get; set; } = "blink"; // none / blink / alert / sound
    public bool CountdownStopAtZero { get; set; } = true;

    // 倒计时样式
    public string CountdownFontFamily { get; set; } = "Microsoft YaHei UI";
    public double CountdownFontSize { get; set; } = 48;
    public string CountdownFontColor { get; set; } = "#FFFFFFFF";
    public double CountdownOpacity { get; set; } = 1.0;

    // 倒计时描边
    public bool CountdownStrokeEnabled { get; set; } = false;
    public double CountdownStrokeThickness { get; set; } = 1.0;
    public string CountdownStrokeColor { get; set; } = "#FF000000";

    // 倒计时阴影
    public bool CountdownShadowEnabled { get; set; } = true;
    public double CountdownShadowSize { get; set; } = 4.0;
    public string CountdownShadowColor { get; set; } = "#FF000000";

    // 倒计时窗口
    public double CountdownWindowLeft { get; set; } = double.NaN;
    public double CountdownWindowTop { get; set; } = double.NaN;
    public double CountdownWindowWidth { get; set; } = 240;
    public double CountdownWindowHeight { get; set; } = 120;
    public double CountdownWindowOpacity { get; set; } = 1.0;
    public bool CountdownTopmost { get; set; } = true;

    // === 待办文字 ===
    public bool TodoScrollEnabled { get; set; } = false;
    public string TodoScrollText { get; set; } = "";
    public double TodoScrollSpeed { get; set; } = 40.0; // pixels per second
    public string TodoScrollFontColor { get; set; } = "#FFFFF8DC"; // LightYellow
    public double TodoScrollFontSize { get; set; } = 12;
    public string TodoScrollFontFamily { get; set; } = "Microsoft YaHei";

    // === 音乐播放信息 ===
    public bool MediaInfoEnabled { get; set; } = false;
    public bool MediaInfoShowArtist { get; set; } = true;

    // === 定时自动切换表盘 ===
    public bool AutoSwitchEnabled { get; set; } = false;
    public string AutoSwitchDayMode { get; set; } = "digital";
    public string AutoSwitchNightMode { get; set; } = "minimal";
    public int AutoSwitchDayStartHour { get; set; } = 7;
    public int AutoSwitchNightStartHour { get; set; } = 19;

    // === 双时区表盘 ===
    public string DualAnalogTimeZone { get; set; } = "Eastern Standard Time";
    public string DualAnalogLabel { get; set; } = "纽约";

    // === 全局滤镜 ===
    public bool GlobalFilterEnabled { get; set; } = false;
    public double GlobalFilterVignette { get; set; } = 0.0; // 0-1
    public double GlobalFilterGrayscale { get; set; } = 0.0; // 0-1
    public double GlobalFilterColorTemp { get; set; } = 0.0; // -1(cool) to 1(warm)

    // === New structured config ===
    public Models.GlobalConfig Global { get; set; } = new();
    public Models.LayoutConfig Layout { get; set; } = new();
    public Dictionary<string, Models.ComponentConfig> Components { get; set; } = new();
    public Dictionary<string, bool> Plugins { get; set; } = new();

    // === 指针方案持久化 ===
    /// <summary>所有自定义指针方案(启动时由 PointerStyleManager 加载)</summary>
    public List<Models.PointerSet> PointerSets { get; set; } = new();
    /// <summary>当前激活的指针方案 ID(空=使用默认矢量指针)</summary>
    public string ActivePointerSetId { get; set; } = string.Empty;

    // 旧路径:程序目录(仅用于一次性迁移到新路径)
    private static readonly string LegacyFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    // 当前实例ID(0表示主实例)
    public static int CurrentInstanceId { get; set; } = 0;

    // 新路径:用户 LocalAppData,避免装在 Program Files 时无写权限
    private static string GetFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopClock");
        var fileName = CurrentInstanceId > 0 ? $"settings_instance_{CurrentInstanceId}.json" : "settings.json";
        return Path.Combine(dir, fileName);
    }

    public static string GetPositionFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopClock");
        var fileName = CurrentInstanceId > 0 ? $"pos_instance_{CurrentInstanceId}.txt" : "pos.txt";
        return Path.Combine(dir, fileName);
    }

    public void Save()
    {
        PopulateStructuredFromFlat();
        var path = GetFilePath();
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        });
        File.WriteAllText(path, json);
    }

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public static AppSettings Load()
    {
        var path = GetFilePath();
        string? json = null;
        bool migratedFromLegacy = false;
        try
        {
            if (File.Exists(path))
            {
                json = File.ReadAllText(path);
            }
            else if (File.Exists(LegacyFilePath))
            {
                // 首次运行:从旧路径迁移
                json = File.ReadAllText(LegacyFilePath);
                migratedFromLegacy = true;
            }

            if (json != null)
            {
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("Version", out var ver) && ver.GetInt32() >= 2)
                {
                    var v2 = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
                    v2.MigrateFlatFromStructured();
                    if (migratedFromLegacy) v2.Save();
                    return v2;
                }

                var v1 = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
                v1.Version = 2;
                v1.MigrateToStructured();
                if (migratedFromLegacy) v1.Save();
                return v1;
            }
        }
        catch
        {
        }
        return new AppSettings();
    }

    private void MigrateToStructured()
    {
        Global = new Models.GlobalConfig
        {
            Language = Language,
            ThemePreset = ThemePreset,
            BackgroundOpacity = BackgroundOpacity,
            ClickThrough = ClickThrough,
            SnapToEdge = SnapToEdge,
            LockPosition = LockPosition,
            AutoStart = AutoStart,
            HotkeyHide = HotkeyHide,
            WindowWidth = 500,
            WindowHeight = 120
        };

        // DisplayMode 是显示模式名,需映射为真实组件 id
        var clockId = DisplayMode switch
        {
            "flip" => "flip_clock",
            "word" => "word_clock",
            "binary" => "binary_clock",
            "minimal" => "minimal_clock",
            "progress" => "analog_clock",
            _ => "digital_clock"
        };

        var activeComponents = new List<string>();
        if (ShowDate) activeComponents.Add("date");
        if (LunarEnabled) activeComponents.Add("lunar");
        activeComponents.Add(clockId);
        if (WorldClockEnabled) activeComponents.Add("world_clock");

        Layout = new Models.LayoutConfig
        {
            ActiveComponents = activeComponents,
            ZOrder = new List<string> { "date", "lunar", clockId, "world_clock" }
        };

        Components = new Dictionary<string, Models.ComponentConfig>
        {
            ["digital_clock"] = new()
            {
                Enabled = DisplayMode == "digital",
                Position = "center",
                Settings = new()
                {
                    ["fontSize"] = FontSize,
                    ["fontFamily"] = FontFamily,
                    ["fontColor"] = FontColor,
                    ["use24Hour"] = Use24Hour,
                    ["showSeconds"] = ShowSeconds
                }
            },
            ["date"] = new()
            {
                Enabled = ShowDate,
                Position = DatePosition,
                Settings = new()
                {
                    ["fontSize"] = DateFontSize,
                    ["fontFamily"] = DateFontFamily,
                    ["fontColor"] = DateColor
                }
            },
            ["lunar"] = new()
            {
                Enabled = LunarEnabled,
                Position = "top",
                Settings = new()
                {
                    ["fontSize"] = LunarFontSize,
                    ["fontColor"] = LunarColor,
                    ["showSolarTerm"] = ShowSolarTerm,
                    ["showZodiac"] = ShowZodiac
                }
            },
            ["world_clock"] = new()
            {
                Enabled = WorldClockEnabled,
                Position = "bottom",
                Settings = new()
                {
                    ["timeZone"] = WorldClockTimeZone
                }
            },
            ["chime"] = new()
            {
                Enabled = ChimeEnabled
            },
            ["reminder"] = new()
            {
                Enabled = ReminderEnabled,
                Settings = new()
                {
                    ["items"] = RemindersJson
                }
            }
        };
    }

    private void PopulateStructuredFromFlat()
    {
        if (Global == null) Global = new();
        Global.Language = Language;
        Global.ThemePreset = ThemePreset;
        Global.BackgroundOpacity = BackgroundOpacity;
        Global.ClickThrough = ClickThrough;
        Global.SnapToEdge = SnapToEdge;
        Global.LockPosition = LockPosition;
        Global.AutoStart = AutoStart;
        Global.HotkeyHide = HotkeyHide;

        if (Components == null) Components = new();
        SetComponentSetting("digital_clock", "fontSize", FontSize);
        SetComponentSetting("digital_clock", "fontFamily", FontFamily);
        SetComponentSetting("digital_clock", "fontColor", FontColor);
        SetComponentSetting("digital_clock", "use24Hour", Use24Hour);
        SetComponentSetting("digital_clock", "showSeconds", ShowSeconds);

        SetComponentSetting("date", "fontSize", DateFontSize);
        SetComponentSetting("date", "fontFamily", DateFontFamily);
        SetComponentSetting("date", "fontColor", DateColor);

        SetComponentSetting("lunar", "fontSize", LunarFontSize);
        SetComponentSetting("lunar", "fontColor", LunarColor);
        SetComponentSetting("lunar", "showSolarTerm", ShowSolarTerm);
        SetComponentSetting("lunar", "showZodiac", ShowZodiac);

        SetComponentSetting("world_clock", "timeZone", WorldClockTimeZone);
        SetComponentSetting("reminder", "items", RemindersJson);

        SetComponentSetting("weather", "city", WeatherCity);
        SetComponentSetting("weather", "latitude", WeatherLatitude);
        SetComponentSetting("weather", "longitude", WeatherLongitude);
        SetComponentSetting("weather", "fontSize", WeatherFontSize);
        SetComponentSetting("weather", "detailFontSize", WeatherDetailFontSize);
        SetComponentSetting("weather", "fontColor", WeatherFontColor);
        SetComponentSetting("weather", "detailColor", WeatherDetailColor);
        SetComponentSetting("weather", "alignment", WeatherAlignment);
        if (Components.TryGetValue("weather", out var wcfg))
            wcfg.Position = WeatherPosition;
    }

    private void MigrateFlatFromStructured()
    {
        if (Global != null)
        {
            Language = Global.Language;
            ThemePreset = Global.ThemePreset;
            BackgroundOpacity = Global.BackgroundOpacity;
            ClickThrough = Global.ClickThrough;
            SnapToEdge = Global.SnapToEdge;
            LockPosition = Global.LockPosition;
            AutoStart = Global.AutoStart;
            HotkeyHide = Global.HotkeyHide;
        }

        if (Components != null)
        {
            FontSize = GetComponentSetting<double>("digital_clock", "fontSize", FontSize);
            FontFamily = GetComponentSetting<string>("digital_clock", "fontFamily", FontFamily);
            FontColor = GetComponentSetting<string>("digital_clock", "fontColor", FontColor);
            Use24Hour = GetComponentSetting<bool>("digital_clock", "use24Hour", Use24Hour);
            ShowSeconds = GetComponentSetting<bool>("digital_clock", "showSeconds", ShowSeconds);

            DateFontSize = GetComponentSetting<double>("date", "fontSize", DateFontSize);
            DateFontFamily = GetComponentSetting<string>("date", "fontFamily", DateFontFamily);
            DateColor = GetComponentSetting<string>("date", "fontColor", DateColor);

            LunarFontSize = GetComponentSetting<double>("lunar", "fontSize", LunarFontSize);
            LunarColor = GetComponentSetting<string>("lunar", "fontColor", LunarColor);
            ShowSolarTerm = GetComponentSetting<bool>("lunar", "showSolarTerm", ShowSolarTerm);
            ShowZodiac = GetComponentSetting<bool>("lunar", "showZodiac", ShowZodiac);

            WorldClockTimeZone = GetComponentSetting<string>("world_clock", "timeZone", WorldClockTimeZone);
            RemindersJson = GetComponentSetting<string>("reminder", "items", RemindersJson);

            WeatherCity = GetComponentSetting<string>("weather", "city", WeatherCity);
            WeatherLatitude = GetComponentSetting<double>("weather", "latitude", WeatherLatitude);
            WeatherLongitude = GetComponentSetting<double>("weather", "longitude", WeatherLongitude);
            WeatherFontSize = GetComponentSetting<double>("weather", "fontSize", WeatherFontSize);
            WeatherDetailFontSize = GetComponentSetting<double>("weather", "detailFontSize", WeatherDetailFontSize);
            WeatherFontColor = GetComponentSetting<string>("weather", "fontColor", WeatherFontColor);
            WeatherDetailColor = GetComponentSetting<string>("weather", "detailColor", WeatherDetailColor);
            WeatherAlignment = GetComponentSetting<string>("weather", "alignment", WeatherAlignment);
            if (Components.TryGetValue("weather", out var wcfg) && !string.IsNullOrEmpty(wcfg.Position))
                WeatherPosition = wcfg.Position;
        }
    }

    public void SetComponentSetting(string component, string key, object value)
    {
        if (!Components.ContainsKey(component))
            Components[component] = new Models.ComponentConfig();
        Components[component].Settings[key] = value;
    }

    public T GetComponentSetting<T>(string component, string key, T fallback)
    {
        if (Components.TryGetValue(component, out var config) && config.Settings.TryGetValue(key, out var val))
        {
            if (val is JsonElement je)
            {
                try { return JsonSerializer.Deserialize<T>(je.GetRawText(), JsonOpts) ?? fallback; }
                catch { return fallback; }
            }
            if (val is T t) return t;
        }
        return fallback;
    }
}
