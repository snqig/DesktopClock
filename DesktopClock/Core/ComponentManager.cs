using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using DesktopClock.Services;

namespace DesktopClock.Core;

/// <summary>
/// 全局组件管理器单例：统一管理所有悬浮窗口的生命周期。
/// 负责：创建/显示/隐藏/销毁组件窗口、启动自动加载、退出统一保存。
/// </summary>
public sealed class ComponentManager
{
    private static ComponentManager? _instance;
    public static ComponentManager Instance => _instance ??= new();

    private readonly Dictionary<string, BaseFloatWindow> _windows = new();
    private readonly Dictionary<string, Func<BaseFloatWindow>> _factories = new();
    private FloatWindowConfig _config = new();
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _isShuttingDown;

    // 配置文件路径
    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopClock");
    private static string ConfigPath => Path.Combine(ConfigDir, "float_windows.json");

    /// <summary>当前配置(只读访问)</summary>
    public FloatWindowConfig Config => _config;

    private ComponentManager() { }

    /// <summary>注册组件工厂</summary>
    public void RegisterFactory(string componentId, Func<BaseFloatWindow> factory)
    {
        _factories[componentId] = factory;
    }

    /// <summary>初始化：加载配置、创建启用的组件、初始化托盘</summary>
    public void Initialize()
    {
        LoadConfig();
        EnsureTrayIcon();

        foreach (var (id, cfg) in _config.Components)
        {
            if (cfg.Enabled && _factories.TryGetValue(id, out var factory))
            {
                try
                {
                    var win = factory();
                    _windows[id] = win;
                    win.Show();
                }
                catch (Exception ex)
                {
                    Services.Logger.Error($"[ComponentManager] Failed to create {id}", ex);
                }
            }
        }
    }

    /// <summary>显示指定组件</summary>
    public void Show(string componentId)
    {
        if (_windows.TryGetValue(componentId, out var win))
        {
            win.Show();
            return;
        }

        if (_factories.TryGetValue(componentId, out var factory))
        {
            try
            {
                var w = factory();
                _windows[componentId] = w;
                w.Show();
                if (_config.Components.TryGetValue(componentId, out var cfg))
                {
                    cfg.Enabled = true;
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                Services.Logger.Error($"[ComponentManager] Failed to show {componentId}", ex);
            }
        }
    }

    /// <summary>隐藏指定组件</summary>
    public void Hide(string componentId)
    {
        if (_windows.TryGetValue(componentId, out var win))
        {
            win.Hide();
            if (_config.Components.TryGetValue(componentId, out var cfg))
            {
                cfg.Enabled = false;
                SaveConfig();
            }
        }
    }

    /// <summary>切换组件显示/隐藏</summary>
    public void Toggle(string componentId)
    {
        if (_windows.TryGetValue(componentId, out var win) && win.IsVisible)
            Hide(componentId);
        else
            Show(componentId);
    }

    /// <summary>显示全部组件</summary>
    public void ShowAll()
    {
        foreach (var id in _factories.Keys)
            Show(id);
    }

    /// <summary>隐藏全部组件</summary>
    public void HideAll()
    {
        foreach (var win in _windows.Values)
            win.Hide();
        foreach (var cfg in _config.Components.Values)
            cfg.Enabled = false;
        SaveConfig();
    }

    /// <summary>获取组件窗口</summary>
    public BaseFloatWindow? Get(string componentId)
        => _windows.TryGetValue(componentId, out var w) ? w : null;

    /// <summary>获取组件配置</summary>
    public ComponentWindowConfig? GetConfig(string componentId)
        => _config.Components.TryGetValue(componentId, out var c) ? c : null;

    /// <summary>确保组件配置存在</summary>
    public ComponentWindowConfig EnsureConfig(string componentId)
    {
        if (!_config.Components.TryGetValue(componentId, out var cfg))
        {
            cfg = new ComponentWindowConfig();
            _config.Components[componentId] = cfg;
        }
        return cfg;
    }

    /// <summary>通知所有组件配置变更</summary>
    public void NotifyConfigChange()
    {
        SaveConfig();
        foreach (var win in _windows.Values)
            win.ApplyConfigChange();
    }

    /// <summary>打开全局设置窗口</summary>
    public void OpenGlobalSettings()
    {
        var settings = DesktopClock.AppSettings.Load();
        var win = new SettingsWindow(settings);
        if (win.ShowDialog() == true)
        {
            settings.Save();
            Services.SettingsProvider.Instance.UpdateSettings(settings);
            NotifyConfigChange();
        }
    }

    /// <summary>加载配置文件</summary>
    private void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                _config = JsonSerializer.Deserialize<FloatWindowConfig>(json, DesktopClock.AppSettings.JsonOpts) ?? new();
            }
        }
        catch (Exception ex)
        {
            Services.Logger.Error("[ComponentManager] LoadConfig failed", ex);
        }
    }

    /// <summary>保存配置文件</summary>
    public void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(_config, DesktopClock.AppSettings.JsonOpts);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Services.Logger.Error("[ComponentManager] SaveConfig failed", ex);
        }
    }

    /// <summary>程序退出：保存所有状态并关闭窗口</summary>
    public void Shutdown()
    {
        _isShuttingDown = true;
        NotificationService.NotificationRequested -= OnNotificationRequested;
        foreach (var win in _windows.Values)
        {
            try
            {
                win.SavePosition();
                win.Close();
            }
            catch { }
        }
        _windows.Clear();
        SaveConfig();
        _trayIcon?.Dispose();
    }

    // ==================== 托盘图标 ====================

    private void OnNotificationRequested(string title, string body)
    {
        _trayIcon?.ShowBalloonTip(3000, title, body, System.Windows.Forms.ToolTipIcon.Info);
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon != null) return;

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "DesktopClock.exe")
                   ?? System.Drawing.SystemIcons.Application,
            Text = "桌面时钟",
            Visible = true
        };

        // 订阅通知事件，转发到托盘气泡
        NotificationService.NotificationRequested += OnNotificationRequested;

        var menu = new System.Windows.Forms.ContextMenuStrip();

        menu.Items.Add("显示全部组件", null, (_, _) => ShowAll());
        menu.Items.Add("隐藏全部组件", null, (_, _) => HideAll());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("设置", null, (_, _) => OpenGlobalSettings());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) =>
        {
            Shutdown();
            Application.Current.Shutdown();
        });

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => OpenGlobalSettings();
    }
}

// ==================== 配置模型 ====================

/// <summary>全部悬浮窗口的根配置</summary>
public class FloatWindowConfig
{
    /// <summary>配置版本(用于迁移)</summary>
    public int Version { get; set; } = 1;

    /// <summary>各组件配置字典</summary>
    public Dictionary<string, ComponentWindowConfig> Components { get; set; } = new();
}

/// <summary>单个悬浮窗口的通用配置</summary>
public class ComponentWindowConfig
{
    /// <summary>是否启用(显示)</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>窗口左边距</summary>
    public double Left { get; set; } = double.NaN;

    /// <summary>窗口上边距</summary>
    public double Top { get; set; } = double.NaN;

    /// <summary>窗口宽度</summary>
    public double Width { get; set; } = 300;

    /// <summary>窗口高度</summary>
    public double Height { get; set; } = 150;

    /// <summary>是否置顶</summary>
    public bool Topmost { get; set; } = true;

    /// <summary>是否锁定位置</summary>
    public bool LockPosition { get; set; } = false;

    /// <summary>窗口透明度 0~1</summary>
    public double Opacity { get; set; } = 1.0;

    // ==================== 通用样式 ====================

    /// <summary>字体</summary>
    public string FontFamily { get; set; } = "Microsoft YaHei UI";

    /// <summary>字号</summary>
    public double FontSize { get; set; } = 14;

    /// <summary>文字颜色</summary>
    public string FontColor { get; set; } = "#FFFFFFFF";

    /// <summary>是否启用描边</summary>
    public bool StrokeEnabled { get; set; } = false;

    /// <summary>描边颜色</summary>
    public string StrokeColor { get; set; } = "#FF000000";

    /// <summary>描边粗细</summary>
    public double StrokeThickness { get; set; } = 1.0;

    /// <summary>是否启用阴影</summary>
    public bool ShadowEnabled { get; set; } = false;

    /// <summary>阴影颜色</summary>
    public string ShadowColor { get; set; } = "#FF000000";

    /// <summary>阴影大小</summary>
    public double ShadowSize { get; set; } = 4;

    // ==================== 组件专属参数(自由扩展) ====================

    /// <summary>组件专属参数字典</summary>
    public Dictionary<string, object> Settings { get; set; } = new();

    // === 类型安全取值方法(兼容 JsonElement 反序列化) ===

    /// <summary>从 Settings 中读取 bool 值</summary>
    public bool GetBool(string key, bool defaultValue = false)
    {
        if (!Settings.TryGetValue(key, out var v)) return defaultValue;
        return v switch
        {
            bool b => b,
            System.Text.Json.JsonElement el => el.ValueKind == System.Text.Json.JsonValueKind.True,
            _ => defaultValue
        };
    }

    /// <summary>从 Settings 中读取 int 值</summary>
    public int GetInt(string key, int defaultValue = 0)
    {
        if (!Settings.TryGetValue(key, out var v)) return defaultValue;
        return v switch
        {
            int i => i,
            double d => (int)d,
            System.Text.Json.JsonElement el when el.ValueKind == System.Text.Json.JsonValueKind.Number => el.GetInt32(),
            _ => defaultValue
        };
    }

    /// <summary>从 Settings 中读取 double 值</summary>
    public double GetDouble(string key, double defaultValue = 0)
    {
        if (!Settings.TryGetValue(key, out var v)) return defaultValue;
        return v switch
        {
            double d => d,
            int i => i,
            System.Text.Json.JsonElement el when el.ValueKind == System.Text.Json.JsonValueKind.Number => el.GetDouble(),
            _ => defaultValue
        };
    }

    /// <summary>从 Settings 中读取 string 值</summary>
    public string GetString(string key, string defaultValue = "")
    {
        if (!Settings.TryGetValue(key, out var v)) return defaultValue;
        return v switch
        {
            string s => s,
            System.Text.Json.JsonElement el => el.ValueKind == System.Text.Json.JsonValueKind.String ? el.GetString() ?? defaultValue : el.ToString(),
            _ => v?.ToString() ?? defaultValue
        };
    }
}
