using System.Windows.Media;

namespace DesktopClock.Skins;

/// <summary>
/// 通用相册背景配置,所有表盘可叠加使用。
/// </summary>
public class SkinBackgroundConfig
{
    public string ImagePath { get; set; } = string.Empty;
    public double Opacity { get; set; } = 1.0;
    public double Blur { get; set; } = 0;
    public Stretch Mode { get; set; } = Stretch.UniformToFill;

    public static SkinBackgroundConfig FromDictionary(Dictionary<string, object> dict)
    {
        var cfg = new SkinBackgroundConfig();
        if (dict.TryGetValue("imagePath", out var v)) cfg.ImagePath = v?.ToString() ?? string.Empty;
        if (dict.TryGetValue("opacity", out v) && double.TryParse(v?.ToString(), out var op)) cfg.Opacity = op;
        if (dict.TryGetValue("blur", out v) && double.TryParse(v?.ToString(), out var bl)) cfg.Blur = bl;
        if (dict.TryGetValue("mode", out v) && Enum.TryParse<Stretch>(v?.ToString(), out var m)) cfg.Mode = m;
        return cfg;
    }

    public Dictionary<string, object> ToDictionary() => new()
    {
        ["imagePath"] = ImagePath,
        ["opacity"] = Opacity,
        ["blur"] = Blur,
        ["mode"] = Mode.ToString()
    };
}
