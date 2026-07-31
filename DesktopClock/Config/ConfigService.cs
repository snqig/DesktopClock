using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DesktopClock.Config;

namespace DesktopClock.Config;

/// <summary>
/// 新版 AppConfig 持久化服务。
/// - 异步读写(后台线程)
/// - 防抖保存(延迟 500ms)
/// - 自动备份(崩溃恢复)
/// - 从旧 AppSettings 自动迁移
/// </summary>
public sealed class ConfigService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private readonly string _dir;
    private readonly string _configPath;
    private readonly string _backupPath;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private Timer? _debounceTimer;
    private AppConfig? _pendingSave;
    private volatile bool _disposed;

    public ConfigService(string? dir = null)
    {
        _dir = dir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopClock");
        Directory.CreateDirectory(_dir);
        _configPath = Path.Combine(_dir, "appconfig.json");
        _backupPath = Path.Combine(_dir, "appconfig.backup.json");
    }

    /// <summary>加载配置(不存在时从旧 settings.json 迁移)。</summary>
    public AppConfig Load()
    {
        _ioLock.Wait();
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg != null) return cfg;
            }

            // 主文件损坏 → 尝试备份
            if (File.Exists(_backupPath))
            {
                var json = File.ReadAllText(_backupPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg != null) return cfg;
            }

            // 首次运行 → 从旧 AppSettings 迁移
            return MigrateFromLegacy();
        }
        finally { _ioLock.Release(); }
    }

    /// <summary>防抖保存(500ms 内多次调用合并为一次磁盘写入)。</summary>
    public void SaveDebounced(AppConfig config)
    {
        if (_disposed) return;
        _pendingSave = config;
        _debounceTimer ??= new Timer(_ => DoSave(), null, 500, Timeout.Infinite);
        _debounceTimer.Change(500, Timeout.Infinite);
    }

    /// <summary>立即保存(阻塞,用于程序退出)。</summary>
    public void SaveNow(AppConfig config)
    {
        if (_disposed) return;
        _pendingSave = config;
        DoSave();
    }

    private void DoSave()
    {
        var cfg = _pendingSave;
        if (cfg == null) return;
        _ioLock.Wait();
        try
        {
            // 先写入临时文件再替换,避免写入中断损坏配置
            var tmp = _configPath + ".tmp";
            var json = JsonSerializer.Serialize(cfg, JsonOpts);
            File.WriteAllText(tmp, json);

            // 当前文件备份
            if (File.Exists(_configPath))
                File.Copy(_configPath, _backupPath, true);

            File.Move(tmp, _configPath, overwrite: true);
        }
        catch { /* 保存失败不崩溃 */ }
        finally { _ioLock.Release(); }
    }

    /// <summary>从旧 settings.json 迁移到新 AppConfig。</summary>
    private AppConfig MigrateFromLegacy()
    {
        var cfg = new AppConfig();
        try
        {
            var legacyPath = Path.Combine(_dir, "settings.json");
            if (!File.Exists(legacyPath)) return cfg;

            var json = File.ReadAllText(legacyPath);
            using var doc = JsonDocument.Parse(json);

            // 时钟挂件
            if (doc.RootElement.TryGetProperty("DisplayMode", out var dm))
                cfg.Clock.DisplayMode = dm.GetString() ?? "digital";
            if (doc.RootElement.TryGetProperty("Use24Hour", out var u24))
                cfg.Clock.Use24Hour = u24.GetBoolean();
            if (doc.RootElement.TryGetProperty("ShowSeconds", out var ss))
                cfg.Clock.ShowSeconds = ss.GetBoolean();
            if (doc.RootElement.TryGetProperty("FontFamily", out var ff))
                cfg.Clock.FontFamily = ff.GetString() ?? "DS-Digital";
            if (doc.RootElement.TryGetProperty("FontSize", out var fs))
                cfg.Clock.FontSize = fs.GetDouble();
            if (doc.RootElement.TryGetProperty("FontColor", out var fc))
                cfg.Clock.FontColor = fc.GetString() ?? "#00d4ff";
            if (doc.RootElement.TryGetProperty("BackgroundOpacity", out var bo))
                cfg.Clock.BackgroundOpacity = bo.GetDouble();
            if (doc.RootElement.TryGetProperty("BackgroundType", out var bt))
                cfg.Clock.BackgroundType = bt.GetString() ?? "none";
            if (doc.RootElement.TryGetProperty("ActivePointerSetId", out var aps))
                cfg.Clock.ActivePointerSetId = aps.GetString() ?? string.Empty;
            if (doc.RootElement.TryGetProperty("ShowDate", out var sd))
                cfg.Clock.ShowDate = sd.GetBoolean();
            if (doc.RootElement.TryGetProperty("LunarEnabled", out var le))
                cfg.Clock.LunarEnabled = le.GetBoolean();

            // 全局
            if (doc.RootElement.TryGetProperty("Language", out var lng))
                cfg.Language = lng.GetString() ?? "zh";
            if (doc.RootElement.TryGetProperty("ThemePreset", out var tp))
                cfg.ThemePreset = tp.GetString() ?? "default";
            if (doc.RootElement.TryGetProperty("AutoStart", out var au))
                cfg.AutoStart = au.GetBoolean();

            // 热键
            if (doc.RootElement.TryGetProperty("HotkeyHide", out var hk))
                cfg.Hotkey.ToggleAll = hk.GetString() ?? "Ctrl+H";
        }
        catch { /* 迁移失败用默认值 */ }

        return cfg;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debounceTimer?.Dispose();
        _ioLock.Dispose();
    }
}
