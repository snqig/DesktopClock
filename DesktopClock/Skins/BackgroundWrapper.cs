using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DesktopClock.Components;
using DesktopClock.Models;

namespace DesktopClock.Skins;

/// <summary>
/// 通用相册背景包装器。可包裹任意 <see cref="IClockComponent"/>,在其视图下方
/// 叠加一层图片背景(支持透明度、模糊、拉伸模式),使所有表盘共用同一套背景能力。
/// 配置通过 <see cref="SkinBackgroundConfig"/> 存储,键名与 SkinHost 保持一致。
/// </summary>
public class BackgroundWrapper : IClockComponent
{
    private readonly IClockComponent _inner;
    private readonly Grid _rootGrid = new();
    private readonly Image _bgImage = new();

    public string Id => _inner.Id;
    public string DisplayName => _inner.DisplayName;
    public FrameworkElement View => _rootGrid;
    public ComponentConfig Config { get; set; } = new();
    /// <summary>获取被包裹的内部组件(用于穿透访问 SkinHost 等)。</summary>
    public IClockComponent Inner => _inner;

    public BackgroundWrapper(IClockComponent inner)
    {
        _inner = inner;
        // 背景层置于最底层
        _bgImage.Stretch = Stretch.UniformToFill;
        _rootGrid.Children.Add(_bgImage);
        // 内部组件视图置于上方;先断开旧父级,避免"已是另一个元素的逻辑子元素"异常
        DetachFromParent(_inner.View as FrameworkElement);
        _rootGrid.Children.Add(_inner.View);
    }

    private static void DetachFromParent(FrameworkElement? element)
    {
        if (element == null || element.Parent == null) return;
        switch (element.Parent)
        {
            case Panel p: p.Children.Remove(element); break;
            case ContentControl cc: cc.Content = null; break;
            case Decorator dec: dec.Child = null; break;
        }
    }

    public void Update(DateTime now) => _inner.Update(now);

    public void ApplyConfig()
    {
        // 先把 Config 同步给内部组件(保留原有行为)
        _inner.Config = Config;
        _inner.ApplyConfig();
        ApplyBackground();
    }

    private void ApplyBackground()
    {
        var cfg = SkinBackgroundConfig.FromDictionary(Config.Settings);
        if (string.IsNullOrWhiteSpace(cfg.ImagePath))
        {
            _bgImage.Source = null;
            _bgImage.Effect = null;
            _bgImage.Opacity = 1.0;
            return;
        }

        var fullPath = Path.IsPathRooted(cfg.ImagePath)
            ? cfg.ImagePath
            : Path.Combine(AppContext.BaseDirectory, cfg.ImagePath);
        if (!File.Exists(fullPath))
        {
            _bgImage.Source = null;
            return;
        }

        try
        {
            _bgImage.Source = new BitmapImage(new Uri(fullPath, UriKind.Absolute));
        }
        catch
        {
            _bgImage.Source = null;
            return;
        }
        _bgImage.Stretch = cfg.Mode;
        _bgImage.Opacity = cfg.Opacity;
        _bgImage.Effect = cfg.Blur > 0 ? new BlurEffect { Radius = cfg.Blur } : null;
    }
}
