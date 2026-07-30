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
    public string Language { get; set; } = "zh";
    public string ThemePreset { get; set; } = "default";
    public bool SnapToEdge { get; set; } = false;
    public bool AutoStart { get; set; } = false;
    public bool LockPosition { get; set; } = false;
    public bool ClickThrough { get; set; } = false;

    // === New structured config ===
    public Models.GlobalConfig Global { get; set; } = new();
    public Models.LayoutConfig Layout { get; set; } = new();
    public Dictionary<string, Models.ComponentConfig> Components { get; set; } = new();
    public Dictionary<string, bool> Plugins { get; set; } = new();

    // 旧路径:程序目录(仅用于一次性迁移到新路径)
    private static readonly string LegacyFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    // 新路径:用户 LocalAppData,避免装在 Program Files 时无写权限
    private static string GetFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopClock");
        return Path.Combine(dir, "settings.json");
    }

    public void Save()
    {
        PopulateStructuredFromFlat();
        var path = GetFilePath();
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

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
                    var v2 = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    v2.MigrateFlatFromStructured();
                    if (migratedFromLegacy) v2.Save();
                    return v2;
                }

                var v1 = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
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
        }
    }

    private void SetComponentSetting(string component, string key, object value)
    {
        if (!Components.ContainsKey(component))
            Components[component] = new Models.ComponentConfig();
        Components[component].Settings[key] = value;
    }

    private T GetComponentSetting<T>(string component, string key, T fallback)
    {
        if (Components.TryGetValue(component, out var config) && config.Settings.TryGetValue(key, out var val))
        {
            if (val is JsonElement je)
            {
                try { return JsonSerializer.Deserialize<T>(je.GetRawText()) ?? fallback; }
                catch { return fallback; }
            }
            if (val is T t) return t;
        }
        return fallback;
    }
}
