using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopClock.Core;

namespace DesktopClock.FloatWindows;

/// <summary>
/// 天气悬浮窗口(P0)：HTTP 获取天气，支持经纬度配置与 IP 自动定位。
/// </summary>
public partial class WeatherWindow : BaseFloatWindow
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly DispatcherTimer _timer;
    private DateTime _lastFetch = DateTime.MinValue;
    private static string CachePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopClock", "cache", "weather.json");

    public WeatherWindow()
    {
        ComponentId = "weather";
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
        _timer.Tick += (_, _) => _ = FetchWeatherAsync();
        _timer.Start();
        _ = FetchWeatherAsync();
    }

    private async Task FetchWeatherAsync()
    {
        var cfg = ComponentManager.Instance.GetConfig(ComponentId);
        double lat = 39.9, lon = 116.4;

        lat = cfg?.GetDouble("latitude", 39.9) ?? 39.9;
        lon = cfg?.GetDouble("longitude", 116.4) ?? 116.4;

        try
        {
            // 尝试读取缓存(Open-Meteo 格式)
            if (File.Exists(CachePath))
            {
                var cacheText = await File.ReadAllTextAsync(CachePath);
                var cacheDoc = JsonDocument.Parse(cacheText);
                if (cacheDoc.RootElement.TryGetProperty("current", out var curEl))
                {
                    if (curEl.TryGetProperty("temperature_2m", out var tempEl))
                    {
                        var temp = tempEl.GetDouble();
                        MainText.Text = $"{temp:F0}°C";
                    }
                    if (curEl.TryGetProperty("weather_code", out var wcEl) && wcEl.ValueKind == JsonValueKind.Number)
                    {
                        DetailText.Text = WeatherCodeToString(wcEl.GetInt32());
                    }
                }
            }

            if (DateTime.Now - _lastFetch < TimeSpan.FromMinutes(10)) return;
            _lastFetch = DateTime.Now;

            // Open-Meteo API
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code";
            var resp = await _http.GetStringAsync(url);
            var doc = JsonDocument.Parse(resp);

            if (doc.RootElement.TryGetProperty("current", out var cur))
            {
                if (cur.TryGetProperty("temperature_2m", out var tEl))
                {
                    var temp = tEl.GetDouble();
                    MainText.Text = $"{temp:F0}°C";
                }
                if (cur.TryGetProperty("weather_code", out var wcEl))
                {
                    var code = wcEl.GetInt32();
                    DetailText.Text = WeatherCodeToString(code);
                }
            }

            // 写入缓存
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            await File.WriteAllTextAsync(CachePath, resp);
        }
        catch (Exception ex)
        {
            Services.Logger.Error("[WeatherWindow] Fetch failed", ex);
            MainText.Text = "天气获取失败";
        }
    }

    private static string WeatherCodeToString(int code) => code switch
    {
        0 => "晴",
        1 or 2 or 3 => "多云",
        45 or 48 => "雾",
        51 or 53 or 55 => "毛毛雨",
        61 or 63 or 65 => "雨",
        71 or 73 or 75 => "雪",
        80 or 81 or 82 => "阵雨",
        95 or 96 or 99 => "雷暴",
        _ => "未知",
    };

    public override void LoadFromConfig()
    {
        var cfg = ComponentManager.Instance.EnsureConfig(ComponentId);

        if (!double.IsNaN(cfg.Left)) Left = cfg.Left;
        if (!double.IsNaN(cfg.Top)) Top = cfg.Top;
        Width = cfg.Width > 0 ? cfg.Width : 240;
        Height = cfg.Height > 0 ? cfg.Height : 100;
        ClampToScreen();

        IsTopmost = cfg.Topmost;
        DesktopWidgetMode = cfg.DesktopWidgetMode;
        IsLocked = cfg.LockPosition;
        WindowOpacity = cfg.Opacity;

        try { MainText.FontFamily = new FontFamily(cfg.FontFamily); } catch { }
        MainText.FontSize = cfg.FontSize;
        try { MainText.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(cfg.FontColor)); } catch { }
        try { DetailText.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(cfg.FontColor)); } catch { }
    }

    public override void SavePosition()
    {
        var cfg = ComponentManager.Instance.EnsureConfig(ComponentId);
        cfg.Left = Left;
        cfg.Top = Top;
        cfg.Width = ActualWidth > 0 ? ActualWidth : Width;
        cfg.Height = ActualHeight > 0 ? ActualHeight : Height;
        cfg.Topmost = IsTopmost;
        cfg.DesktopWidgetMode = DesktopWidgetMode;
        cfg.LockPosition = IsLocked;
        cfg.Opacity = WindowOpacity;
        ComponentManager.Instance.SaveConfig();
    }

    public override void ApplyConfigChange()
    {
        LoadFromConfig();
        _ = FetchWeatherAsync();
    }
}
