using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopClock.Models;
using DesktopClock.Services;

namespace DesktopClock.Components;

public class WeatherComponent : IClockComponent
{
    public string Id => "weather";
    public string DisplayName => "天气";
    public FrameworkElement View => _panel;
    public ComponentConfig Config { get; set; } = new();

    private readonly StackPanel _panel;
    private readonly TextBlock _mainText;
    private readonly TextBlock _detailText;
    private string? _cached;
    private string? _cachedDetail;
    private DateTime _lastFetch = DateTime.MinValue;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>
    /// 离线缓存文件路径,位于 %LOCALAPPDATA%\DesktopClock\cache\weather.json。
    /// 缓存结构:{ "main":"...", "detail":"...", "ts":"ISO8601" }
    /// </summary>
    private static string CachePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopClock", "cache");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "weather.json");
        }
    }

    public WeatherComponent()
    {
        _panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _mainText = new TextBlock
        {
            Text = "天气加载中...",
            FontSize = 13,
            Foreground = Brushes.LightGray,
            FontFamily = new FontFamily("Microsoft YaHei"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _detailText = new TextBlock
        {
            Text = "",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
            FontFamily = new FontFamily("Microsoft YaHei"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        _panel.Children.Add(_mainText);
        _panel.Children.Add(_detailText);
    }

    public void Update(DateTime now)
    {
        // 首次更新:优先从离线缓存恢复,避免启动时显示"加载中"
        if (_cached == null)
        {
            LoadCache();
            if (_cached != null)
                DispatcherUpdate(_cached + " (缓存)", _cachedDetail ?? "");
        }

        if ((now - _lastFetch).TotalMinutes >= 30)
        {
            _lastFetch = now;
            _ = FetchAsync();
        }
    }

    public void ApplyConfig()
    {
        if (Config.Settings.TryGetValue("fontColor", out var fc))
        {
            try { _mainText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fc.ToString()!)); }
            catch { }
        }
    }

    private async Task FetchAsync()
    {
        double lat = 39.9042, lon = 116.4074;
        if (Config.Settings.TryGetValue("latitude", out var la))
        {
            if (la is double d1) lat = d1;
            else if (la is JsonElement je1 && je1.TryGetDouble(out var d2)) lat = d2;
            else if (double.TryParse(la?.ToString(), out var d3)) lat = d3;
        }
        if (Config.Settings.TryGetValue("longitude", out var lo))
        {
            if (lo is double e1) lon = e1;
            else if (lo is JsonElement je2 && je2.TryGetDouble(out var e2)) lon = e2;
            else if (double.TryParse(lo?.ToString(), out var e3)) lon = e3;
        }

        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current_weather=true&daily=sunrise,sunset&timezone=auto";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var current = doc.RootElement.GetProperty("current_weather");
            double temp = current.GetProperty("temperature").GetDouble();
            int code = current.GetProperty("weathercode").GetInt32();
            string desc = WeatherCodeToDesc(code);

            string detail = "";
            if (doc.RootElement.TryGetProperty("daily", out var daily))
            {
                string sunrise = "", sunset = "";
                if (daily.TryGetProperty("sunrise", out var sr) && sr.GetArrayLength() > 0)
                    sunrise = sr[0].GetString() ?? "";
                if (daily.TryGetProperty("sunset", out var ss) && ss.GetArrayLength() > 0)
                    sunset = ss[0].GetString() ?? "";

                if (!string.IsNullOrEmpty(sunrise) && !string.IsNullOrEmpty(sunset))
                {
                    // 只取时间部分
                    if (sunrise.Length > 11) sunrise = sunrise.Substring(11, 5);
                    if (sunset.Length > 11) sunset = sunset.Substring(11, 5);
                    detail = $"日出 {sunrise}  日落 {sunset}";
                }
            }

            _cached = $"{desc} {temp:F0}°C";
            _cachedDetail = detail;
            SaveCache(_cached, _cachedDetail);
            DispatcherUpdate(_cached, detail);
        }
        catch (Exception ex)
        {
            Logger.Warning($"[Weather] fetch failed: {ex.Message}");
            if (_cached != null)
                DispatcherUpdate(_cached + " (缓存)", _cachedDetail ?? "");
            else
                DispatcherUpdate("天气获取失败", "");
        }
    }

    /// <summary>
    /// 将天气主文本与详情写入离线缓存文件,带时间戳。
    /// </summary>
    private void SaveCache(string main, string? detail)
    {
        try
        {
            var payload = new
            {
                main,
                detail = detail ?? "",
                ts = DateTime.Now.ToString("O")
            };
            File.WriteAllText(CachePath, JsonSerializer.Serialize(payload));
        }
        catch (Exception ex)
        {
            Logger.Warning($"[Weather] save cache failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 从离线缓存文件恢复天气文本。缓存超过 24 小时视为过期不加载。
    /// </summary>
    private void LoadCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return;
            var json = File.ReadAllText(CachePath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("main", out var m)) return;
            _cached = m.GetString();
            if (doc.RootElement.TryGetProperty("detail", out var d))
                _cachedDetail = d.GetString();
            // 缓存超过 24 小时视为过期,不阻塞但记录日志
            if (doc.RootElement.TryGetProperty("ts", out var t) &&
                DateTime.TryParse(t.GetString(), out var ts) &&
                (DateTime.Now - ts).TotalHours > 24)
            {
                Logger.Information("[Weather] cache expired (>24h), will refresh");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"[Weather] load cache failed: {ex.Message}");
        }
    }

    private void DispatcherUpdate(string main, string detail)
    {
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            _mainText.Text = main;
            _detailText.Text = detail;
            _detailText.Visibility = string.IsNullOrEmpty(detail) ? Visibility.Collapsed : Visibility.Visible;
        }));
    }

    private static string WeatherCodeToDesc(int code)
    {
        return code switch
        {
            0 => "☀️ 晴",
            1 or 2 or 3 => "⛅ 多云",
            45 or 48 => "🌫️ 雾",
            51 or 53 or 55 => "🌧️ 毛毛雨",
            61 or 63 or 65 => "🌧️ 雨",
            71 or 73 or 75 => "🌨️ 雪",
            80 or 81 or 82 => "🌧️ 阵雨",
            95 or 96 or 99 => "⛈️ 雷暴",
            _ => "☁️ 阴"
        };
    }
}
