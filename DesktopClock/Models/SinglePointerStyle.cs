using System;

namespace DesktopClock.Models;

/// <summary>
/// 单根指针(时/分/秒)的样式配置,三根指针各自独立解耦。
/// 支持 PNG 素材、旋转锚点、缩放、染色、阴影、发光、透明度。
/// </summary>
public class SinglePointerStyle
{
    /// <summary>PNG 素材相对路径(如 "Assets/PointerSets/Cyberpunk/hour.png"),空则回退矢量线条</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>旋转锚点 X(0~1 相对图片宽,0.5=水平中点)</summary>
    public double RotationCenterX { get; set; } = 0.5;

    /// <summary>旋转锚点 Y(0~1 相对图片高,1.0=底部,即指针根部贴合圆心)</summary>
    public double RotationCenterY { get; set; } = 1.0;

    /// <summary>独立缩放比例(1.0=原始尺寸)</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>染色滤镜(HEX 如 "#00FFFF",空=不着色,保留原图色彩)</summary>
    public string ColorTint { get; set; } = string.Empty;

    /// <summary>阴影开关</summary>
    public bool ShadowEnabled { get; set; } = false;

    /// <summary>外发光强度(0=关,1~10 递增)</summary>
    public double GlowIntensity { get; set; } = 0;

    /// <summary>透明度(0~1)</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>深拷贝</summary>
    public SinglePointerStyle Clone() => new()
    {
        ImagePath = ImagePath,
        RotationCenterX = RotationCenterX,
        RotationCenterY = RotationCenterY,
        Scale = Scale,
        ColorTint = ColorTint,
        ShadowEnabled = ShadowEnabled,
        GlowIntensity = GlowIntensity,
        Opacity = Opacity
    };

    /// <summary>默认时针样式</summary>
    public static SinglePointerStyle DefaultHour() => new();

    /// <summary>默认分针样式</summary>
    public static SinglePointerStyle DefaultMinute() => new();

    /// <summary>默认秒针样式</summary>
    public static SinglePointerStyle DefaultSecond() => new();
}
