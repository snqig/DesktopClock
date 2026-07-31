using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DesktopClock.Models;

namespace DesktopClock.Services;

/// <summary>
/// 指针渲染器:根据 SinglePointerStyle 创建或更新 Image 指针元素。
/// 支持 PNG 素材、旋转锚点、缩放、染色、阴影、发光、透明度。
/// PNG 加载失败时返回 null,调用方可回退到矢量 Line 指针。
/// </summary>
public static class PointerRenderer
{
    /// <summary>
    /// 在 Canvas 上创建或更新一根 PNG 指针。
    /// 锚点(RotationCenterX/Y)对准表盘中心(cx,cy),通过 RenderTransformOrigin 实现。
    /// </summary>
    /// <param name="parent">Canvas 父容器</param>
    /// <param name="existing">已存在的 Image(为 null 则新建)</param>
    /// <param name="style">指针样式</param>
    /// <param name="cx">表盘中心 X</param>
    /// <param name="cy">表盘中心 Y</param>
    /// <param name="angle">当前角度</param>
    /// <param name="baseSize">基准尺寸(图片最长边,默认 200)</param>
    /// <returns>更新后的 Image;PNG 加载失败返回 null</returns>
    public static Image? CreateOrUpdate(
        Canvas parent,
        Image? existing,
        SinglePointerStyle style,
        double cx, double cy,
        double angle,
        double baseSize = 200)
    {
        // 无图片路径 → 返回 null(调用方回退 Line)
        if (string.IsNullOrEmpty(style.ImagePath))
            return null;

        var source = LoadImage(style.ImagePath);
        if (source == null)
            return null; // 降级

        Image img;
        if (existing != null)
        {
            img = existing;
        }
        else
        {
            img = new Image
            {
                Stretch = Stretch.Uniform
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            parent.Children.Add(img);
        }

        img.Source = source;
        img.Opacity = Math.Clamp(style.Opacity, 0, 1);
        img.RenderTransformOrigin = new Point(
            Math.Clamp(style.RotationCenterX, 0, 1),
            Math.Clamp(style.RotationCenterY, 0, 1));

        // 计算显示尺寸:按基准尺寸缩放
        double imgW = source.PixelWidth > 0 ? source.PixelWidth : baseSize;
        double imgH = source.PixelHeight > 0 ? source.PixelHeight : baseSize;
        double maxDim = Math.Max(imgW, imgH);
        double scale = (baseSize / maxDim) * style.Scale;
        double dispW = imgW * scale;
        double dispH = imgH * scale;

        img.Width = dispW;
        img.Height = dispH;

        // 定位:使锚点对准表盘中心
        // Canvas.Left = cx - 锚点相对图片的偏移
        double anchorX = style.RotationCenterX * dispW;
        double anchorY = style.RotationCenterY * dispH;
        Canvas.SetLeft(img, cx - anchorX);
        Canvas.SetTop(img, cy - anchorY);

        // RenderTransform:旋转(RenderTransformOrigin 已设锚点)
        var rotate = new RotateTransform { Angle = angle };
        img.RenderTransform = rotate;

        // 染色 + 阴影/发光(合并为一个 Effect)
        ApplyEffects(img, style);

        return img;
    }

    /// <summary>
    /// 更新已有 Image 指针的角度(高频调用,每帧走针)。
    /// </summary>
    public static void UpdateAngle(Image img, double angle)
    {
        if (img.RenderTransform is RotateTransform rt)
            rt.Angle = angle;
        else
        {
            var newRt = new RotateTransform { Angle = angle };
            img.RenderTransform = newRt;
        }
    }

    // === 加载 PNG(支持 pack URI 和文件路径) ===
    private static BitmapSource? LoadImage(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        try
        {
            var uri = PointerStyleManager.ToPackUri(path);
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(uri, UriKind.Absolute);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            // 尝试文件绝对路径
            try
            {
                if (File.Exists(path))
                {
                    var bytes = File.ReadAllBytes(path);
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = new System.IO.MemoryStream(bytes);
                    img.EndInit();
                    img.Freeze();
                    return img;
                }
            }
            catch { }
            return null;
        }
    }

    // === 染色 + 阴影/发光(合并为一个 DropShadowEffect) ===
    private static void ApplyEffects(Image img, SinglePointerStyle style)
    {
        if (!style.ShadowEnabled && style.GlowIntensity <= 0 && string.IsNullOrEmpty(style.ColorTint))
        {
            img.Effect = null;
            return;
        }

        try
        {
            // 颜色优先级:ColorTint > 默认 Cyan
            var color = !string.IsNullOrEmpty(style.ColorTint)
                ? (Color)ColorConverter.ConvertFromString(style.ColorTint)
                : Colors.Cyan;

            double blur = style.GlowIntensity > 0
                ? 8 + style.GlowIntensity * 4  // 发光:8~48
                : style.ShadowEnabled ? 6 : 0;

            double opacity = style.GlowIntensity > 0
                ? Math.Min(1.0, style.GlowIntensity / 10.0)
                : style.ShadowEnabled ? 0.4 : 0;

            img.Effect = new DropShadowEffect
            {
                Color = color,
                ShadowDepth = style.ShadowEnabled ? 2 : 0,
                BlurRadius = blur,
                Opacity = opacity
            };
        }
        catch { }
    }
}
