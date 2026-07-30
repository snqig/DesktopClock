using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DesktopClock.Services;

namespace DesktopClock.Skins;

/// <summary>
/// 皮肤宿主。负责装载当前激活的表盘皮肤,叠加通用相册背景,
/// 并统一接收时间更新,避免每个皮肤各自维护 timer。
/// </summary>
public class SkinHost : Components.IClockComponent
{
    public string Id => _skin.Id;
    public string DisplayName => _skin.DisplayName;
    public FrameworkElement View => _rootGrid;
    public Models.ComponentConfig Config { get; set; } = new();

    private readonly IClockSkin _skin;
    private readonly Grid _rootGrid = new();

    public SkinHost(IClockSkin skin)
    {
        _skin = skin;
        _rootGrid.Children.Add(_skin.View);
        ApplyBackground();
    }

    public void Update(DateTime now) => _skin.UpdateTime(now);

    public void ApplyConfig()
    {
        _skin.LoadConfig(Config.Settings);
        ApplyBackground();
    }

    private void ApplyBackground()
    {
        // 移除旧背景
        for (int i = _rootGrid.Children.Count - 1; i >= 0; i--)
        {
            if (_rootGrid.Children[i] is Border bg && bg.Tag?.ToString() == "skin-background")
                _rootGrid.Children.RemoveAt(i);
        }

        var cfg = SkinBackgroundConfig.FromDictionary(Config.Settings);
        if (string.IsNullOrWhiteSpace(cfg.ImagePath)) return;

        var fullPath = Path.IsPathRooted(cfg.ImagePath)
            ? cfg.ImagePath
            : Path.Combine(AppContext.BaseDirectory, cfg.ImagePath);
        if (!File.Exists(fullPath)) return;

        var img = new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(fullPath, UriKind.Absolute)),
            Stretch = cfg.Mode,
            Opacity = cfg.Opacity
        };
        if (cfg.Blur > 0)
            img.Effect = new BlurEffect { Radius = cfg.Blur };

        var border = new Border
        {
            Tag = "skin-background",
            Child = img,
            Background = Brushes.Transparent
        };
        // 背景置于最底层
        _rootGrid.Children.Insert(0, border);
    }

    public Dictionary<string, object> SaveConfig() => _skin.SaveConfig();
}
