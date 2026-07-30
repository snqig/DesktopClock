using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopClock.Models;
using Windows.Media.Control;

namespace DesktopClock.Components;

public class MediaInfoComponent : IClockComponent
{
    public string Id => "media_info";
    public string DisplayName => "音乐播放";
    public FrameworkElement View => _panel;
    public ComponentConfig Config { get; set; } = new();

    private readonly StackPanel _panel;
    private readonly TextBlock _titleText;
    private readonly TextBlock _artistText;
    private readonly DispatcherTimer _pollTimer;

    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;

    public MediaInfoComponent()
    {
        _panel = new StackPanel { Orientation = Orientation.Horizontal };
        _titleText = CreateItem("🎵 --");
        _artistText = CreateItem("--", 10, Brushes.Gray);
        _panel.Children.Add(_titleText);
        _panel.Children.Add(_artistText);

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _pollTimer.Tick += async (_, _) => await RefreshAsync();
        _pollTimer.Start();

        _ = InitializeAsync();
    }

    private TextBlock CreateItem(string text, double fontSize = 11, Brush? foreground = null)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            Foreground = foreground ?? Brushes.LightGray,
            Margin = new Thickness(0, 0, 8, 0),
            FontFamily = new FontFamily("Microsoft YaHei")
        };
    }

    private async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged += (s, e) => _ = RefreshAsync();
            }
        }
        catch { }
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        string title = "--";
        string artist = "";
        try
        {
            if (_sessionManager == null)
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            }

            var session = _sessionManager?.GetCurrentSession();
            if (session != null)
            {
                var props = await session.TryGetMediaPropertiesAsync();
                if (props != null)
                {
                    title = string.IsNullOrWhiteSpace(props.Title) ? "--" : props.Title;
                    artist = string.IsNullOrWhiteSpace(props.Artist) ? "" : props.Artist;
                }
            }
        }
        catch { }

        DispatcherUpdate(title, artist);
    }

    private void DispatcherUpdate(string title, string artist)
    {
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            _titleText.Text = $"🎵 {title}";
            _artistText.Text = artist;
            _artistText.Visibility = string.IsNullOrEmpty(artist) ? Visibility.Collapsed : Visibility.Visible;
        }));
    }

    public void Update(DateTime now) { }

    public void ApplyConfig()
    {
        if (Config.Settings.TryGetValue("fontColor", out var fc))
        {
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fc.ToString()!));
                _titleText.Foreground = brush;
                _artistText.Foreground = brush;
            }
            catch { }
        }
        if (Config.Settings.TryGetValue("showArtist", out var sa) && sa is bool b)
        {
            _artistText.Visibility = b && !string.IsNullOrEmpty(_artistText.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
