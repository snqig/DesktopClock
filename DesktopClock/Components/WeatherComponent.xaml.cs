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
    private bool _autoLocating;
    private bool _autoLocated;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private const double DefaultMainFontSize = 13;
    private const double DefaultDetailFontSize = 11;
    private static readonly Color DefaultMainColor = Colors.LightGray;
    private static readonly Color DefaultDetailColor = Color.FromRgb(0xAA, 0xAA, 0xAA);

    public static event Action? LocationAutoDetected;

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
            FontSize = DefaultMainFontSize,
            Foreground = new SolidColorBrush(DefaultMainColor),
            FontFamily = new FontFamily("Microsoft YaHei"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _detailText = new TextBlock
        {
            Text = "",
            FontSize = DefaultDetailFontSize,
            Foreground = new SolidColorBrush(DefaultDetailColor),
            FontFamily = new FontFamily("Microsoft YaHei"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        _panel.Children.Add(_mainText);
        _panel.Children.Add(_detailText);
    }

    public void Update(DateTime now)
    {
        if (_cached == null)
        {
            LoadCache();
            if (_cached != null)
                DispatcherUpdate(_cached + " (缓存)", _cachedDetail ?? "");
        }

        if (!_autoLocated && !_autoLocating && !HasValidCoords())
        {
            _autoLocating = true;
            _ = TryLocateAsync();
        }

        if ((now - _lastFetch).TotalMinutes >= 30)
        {
            _lastFetch = now;
            _ = FetchAsync();
        }
    }

    private bool HasValidCoords()
    {
        double lat = 0, lon = 0;
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
        return lat != 0 && lon != 0;
    }

    public void ApplyConfig()
    {
        // 主文字大小
        if (Config.Settings.TryGetValue("fontSize", out var fs))
        {
            double v = DefaultMainFontSize;
            if (fs is double d1) v = d1;
            else if (fs is JsonElement je1 && je1.TryGetDouble(out var d2)) v = d2;
            else if (double.TryParse(fs?.ToString(), out var d3)) v = d3;
            _mainText.FontSize = Math.Clamp(v, 8, 120);
            // 详情默认按主字号 0.85 缩放
            if (!Config.Settings.ContainsKey("detailFontSize"))
                _detailText.FontSize = Math.Clamp(v * 0.85, 6, 100);
        }

        // 详情文字大小
        if (Config.Settings.TryGetValue("detailFontSize", out var dfs))
        {
            double v = DefaultDetailFontSize;
            if (dfs is double d1) v = d1;
            else if (dfs is JsonElement je1 && je1.TryGetDouble(out var d2)) v = d2;
            else if (double.TryParse(dfs?.ToString(), out var d3)) v = d3;
            _detailText.FontSize = Math.Clamp(v, 6, 100);
        }

        // 主文字颜色(fontColor / mainColor 双 key 兼容)
        Color mainColor = DefaultMainColor;
        bool mainColorSet = false;
        if (Config.Settings.TryGetValue("fontColor", out var fc))
        {
            try { mainColor = (Color)ColorConverter.ConvertFromString(fc.ToString()!); mainColorSet = true; } catch { }
        }
        else if (Config.Settings.TryGetValue("mainColor", out var mc))
        {
            try { mainColor = (Color)ColorConverter.ConvertFromString(mc.ToString()!); mainColorSet = true; } catch { }
        }
        if (mainColorSet) _mainText.Foreground = new SolidColorBrush(mainColor);

        // 详情文字颜色
        if (Config.Settings.TryGetValue("detailColor", out var dc))
        {
            try { _detailText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dc.ToString()!)); }
            catch
            {
                // 失败则按主色 0.7 亮度推导
                if (_mainText.Foreground is SolidColorBrush mcb)
                {
                    var c = mcb.Color;
                    _detailText.Foreground = new SolidColorBrush(Color.FromRgb((byte)(c.R * 0.7), (byte)(c.G * 0.7), (byte)(c.B * 0.7)));
                }
            }
        }
        else
        {
            if (_mainText.Foreground is SolidColorBrush mcb2)
            {
                var c = mcb2.Color;
                _detailText.Foreground = new SolidColorBrush(Color.FromRgb((byte)(c.R * 0.7), (byte)(c.G * 0.7), (byte)(c.B * 0.7)));
            }
        }

        // 水平对齐(left / center / right)
        if (Config.Settings.TryGetValue("alignment", out var align))
        {
            var a = (align?.ToString() ?? "center").ToLowerInvariant();
            var ha = a switch
            {
                "left" => HorizontalAlignment.Left,
                "right" => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Center
            };
            _panel.HorizontalAlignment = ha;
            _mainText.HorizontalAlignment = ha;
            _detailText.HorizontalAlignment = ha;
        }
    }

    private async Task FetchAsync()
    {
        double lat = 31.2989, lon = 120.5853; // 默认苏州
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

    private void SaveCache(string main, string? detail)
    {
        try
        {
            var payload = new { main, detail = detail ?? "", ts = DateTime.Now.ToString("O") };
            File.WriteAllText(CachePath, JsonSerializer.Serialize(payload));
        }
        catch (Exception ex)
        {
            Logger.Warning($"[Weather] save cache failed: {ex.Message}");
        }
    }

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

    private async Task TryLocateAsync()
    {
        try
        {
            Logger.Information("[Weather] auto-locating via ipapi.co...");
            var json = await _http.GetStringAsync("https://ipapi.co/json/");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("latitude", out var latProp) &&
                root.TryGetProperty("longitude", out var lonProp) &&
                latProp.TryGetDouble(out var lat) &&
                lonProp.TryGetDouble(out var lon))
            {
                Config.Settings["latitude"] = lat;
                Config.Settings["longitude"] = lon;

                if (root.TryGetProperty("city", out var cityProp))
                    Config.Settings["city"] = cityProp.GetString() ?? "";
                if (root.TryGetProperty("region", out var regionProp))
                    Config.Settings["region"] = regionProp.GetString() ?? "";
                if (root.TryGetProperty("country_name", out var countryProp))
                    Config.Settings["country"] = countryProp.GetString() ?? "";

                _autoLocated = true;
                Logger.Information($"[Weather] auto-located: {lat},{lon} city={Config.Settings.GetValueOrDefault("city")}");
                LocationAutoDetected?.Invoke();
            }
            else
            {
                Logger.Warning("[Weather] auto-locate response missing lat/lon");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"[Weather] auto-locate failed: {ex.Message}");
        }
        finally
        {
            _autoLocating = false;
        }
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
