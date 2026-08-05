using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopClock.Components;
using DesktopClock.Core;
using DesktopClock.Models;
using DesktopClock.Services;
using DesktopClock.Skins;

namespace DesktopClock.FloatWindows;

/// <summary>
/// 数字时钟悬浮窗口(P0)。
/// 支持多种显示模式(digital/flip/binary/analog/premium/mechanical/minimal/word 及皮肤表盘),
/// 通过 ComponentRegistry + LayoutEngine 复用现有 IClockComponent 实现。
/// 外观样式读取 AppSettings,窗口级属性读取 ComponentWindowConfig。
/// </summary>
public partial class ClockWindow : BaseFloatWindow
{
    private readonly DispatcherTimer _timer;
    private readonly ComponentRegistry _registry = new();
    private readonly LayoutEngine _layoutEngine = new();
    private string _currentClockId = "digital_clock";
    private Services.PointerStyleManager? _pointerStyleManager;

    public ClockWindow()
    {
        ComponentId = "clock";
        InitializeComponent();
        InitializePointerStyleManager();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateClock();
        _timer.Start();
    }

    /// <summary>初始化指针样式管理器并注入到 AnalogClockSkin(移植自 MainWindow)</summary>
    private void InitializePointerStyleManager()
    {
        _pointerStyleManager = new Services.PointerStyleManager();
        _pointerStyleManager.DataDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopClock");
        _pointerStyleManager.Load();
        // 注入到 AnalogClockSkin 静态属性,使皮肤表盘可读取 PNG 指针方案
        Skins.AnalogClockSkin.StyleManager = _pointerStyleManager;
    }

    /// <summary>打开指针样式编辑器(移植自 MainWindow.OpenPointerStyleEditor)</summary>
    private void OpenPointerStyleEditor()
    {
        if (_pointerStyleManager == null) return;
        var editor = new PointerStyleEditor(_pointerStyleManager) { Owner = this };
        editor.OnApply = (set) =>
        {
            var s = DesktopClock.AppSettings.Load();
            s.ActivePointerSetId = set.Id;

            // 同步到所有皮肤组件配置
            var skinIds = new[] { "analog_clock_skin", "ribbon_clock_skin", "dual_analog_clock_skin", "cyberpunk_neon_clock_skin" };
            foreach (var id in skinIds)
            {
                if (s.Components.TryGetValue(id, out var cfg))
                    cfg.Settings["pointerSetId"] = set.Id;
            }
            s.Save();

            // 刷新所有皮肤宿主
            foreach (var comp in _registry.GetAll())
            {
                if (comp is Skins.SkinHost host)
                {
                    host.Config.Settings["pointerSetId"] = set.Id;
                    host.ApplyConfig();
                }
            }
        };
        editor.Show();
    }

    /// <summary>右键菜单增加"指针样式编辑器"入口(仅皮肤模式显示)</summary>
    protected override void OpenComponentSettings()
    {
        var s = DesktopClock.AppSettings.Load();
        var isSkinMode = s.DisplayMode is "analog_skin" or "pointer_editor" or "ribbon" or "dual_analog" or "cyberpunk";
        if (isSkinMode && _pointerStyleManager != null)
        {
            var menu = new System.Windows.Controls.ContextMenu();
            var miEditor = new System.Windows.Controls.MenuItem { Header = "指针样式编辑器" };
            miEditor.Click += (_, _) => OpenPointerStyleEditor();
            menu.Items.Add(miEditor);

            var miGlobal = new System.Windows.Controls.MenuItem { Header = "全局设置" };
            miGlobal.Click += (_, _) => ComponentManager.Instance.OpenGlobalSettings();
            menu.Items.Add(miGlobal);

            menu.IsOpen = true;
            return;
        }
        base.OpenComponentSettings();
    }

    private void UpdateClock()
    {
        try
        {
            _registry.UpdateAll(DateTime.Now);
        }
        catch (Exception ex)
        {
            Services.Logger.Error("[ClockWindow] UpdateAll failed", ex);
        }
    }

    public override void LoadFromConfig()
    {
        var cfg = ComponentManager.Instance.EnsureConfig(ComponentId);
        var s = DesktopClock.AppSettings.Load();

        // 主题预设:应用预设颜色后重置为 default
        ApplyThemePreset(s);

        if (!double.IsNaN(cfg.Left)) Left = cfg.Left;
        if (!double.IsNaN(cfg.Top)) Top = cfg.Top;

        // 窗口尺寸:模拟类组件用正方形(基于组件固有尺寸),数字类基于字号
        var (w, h) = GetWindowSizeForMode(s.DisplayMode, s.FontSize);
        Width = w;
        Height = h;
        ClampToScreen();

        IsTopmost = cfg.Topmost;
        DesktopWidgetMode = cfg.DesktopWidgetMode;
        IsLocked = cfg.LockPosition;
        WindowOpacity = cfg.Opacity;

        // 背景
        ApplyBackground(s);

        // 边框
        try
        {
            MainBorder.BorderBrush = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(s.BorderColor));
        }
        catch { MainBorder.BorderBrush = Brushes.Transparent; }
        MainBorder.BorderThickness = new Thickness(s.BorderThickness);

        // 重建显示模式布局
        RebuildClockLayout(s);

        // 立即触发一次更新,避免首帧空白
        UpdateClock();
    }

    /// <summary>根据显示模式返回合适的窗口尺寸(含 padding)</summary>
    private static (double w, double h) GetWindowSizeForMode(string mode, double fontSize)
    {
        // 双时区表盘:横排双表盘 420x240
        if (mode == "dual_analog")
            return (440, 260);

        // 模拟类及皮肤表盘:固定正方形,匹配组件固有尺寸 + 边框 padding
        var size = mode switch
        {
            "mechanical" => 400.0,
            "analog_premium" => 360.0,
            "progress" => 320.0,
            "analog_skin" or "pointer_editor" => 400.0,  // AnalogClockSkin 400x400
            "cyberpunk" => 400.0,                         // CyberpunkNeonSkin 400x400
            "ribbon" => 400.0,                            // RibbonClockSkin 400x400
            _ => 0.0
        };

        if (size > 0)
            return (size + 20, size + 20); // +20 为 Border padding 余量

        // 数字类:基于字号自适应(基础 56px → 320x120)
        var ratio = Math.Max(fontSize, 10) / 56.0;
        return (320 * ratio, 120 * ratio);
    }

    /// <summary>根据 DisplayMode 重建时钟布局(复用 MainWindow 的模式映射逻辑)</summary>
    private void RebuildClockLayout(AppSettings s)
    {
        var clockId = s.DisplayMode switch
        {
            "flip" => "flip_clock",
            "word" => "word_clock",
            "binary" => "binary_clock",
            "minimal" => "minimal_clock",
            "progress" => "analog_clock",
            "analog_premium" => "analog_premium_clock",
            "mechanical" => "mechanical_clock",
            // 皮肤表盘:通过 SkinHost 包装,动态注册到组件中心
            "analog_skin" or "pointer_editor" => "analog_clock_skin",
            "ribbon" => "ribbon_clock_skin",
            "dual_analog" => "dual_analog_clock_skin",
            "cyberpunk" => "cyberpunk_neon_clock_skin",
            _ => "digital_clock"
        };

        Services.Logger.Information($"[ClockWindow] RebuildClockLayout: DisplayMode={s.DisplayMode}, clockId={clockId}, current={_currentClockId}");

        // 模式未变化则不重建(避免每秒重建)
        if (clockId == _currentClockId && _registry.Get(clockId) != null)
        {
            // 仅刷新配置
            _registry.ApplyAllConfig();
            return;
        }

        // 清空注册表:清理旧组件及所有 skin 宿主(skin clockId 互不相同,切换时需全部清理)
        _registry.Unregister(_currentClockId);
        _registry.Unregister("analog_clock_skin");
        _registry.Unregister("ribbon_clock_skin");
        _registry.Unregister("dual_analog_clock_skin");
        _registry.Unregister("cyberpunk_neon_clock_skin");
        RegisterClockComponent(clockId, s);
        _currentClockId = clockId;

        // 构建布局(只激活时钟组件本身,不含 date/lunar 等)
        var layout = new LayoutConfig
        {
            Mode = "stack",
            ActiveComponents = new List<string> { clockId },
            ZOrder = new List<string> { clockId },
            DatePosition = "top"
        };

        try
        {
            _layoutEngine.BuildLayout(ContentHost, _registry, layout);
            Services.Logger.Information($"[ClockWindow] BuildLayout done for {clockId}, ContentHost.Children.Count={ContentHost.Children.Count}");
        }
        catch (Exception ex)
        {
            Services.Logger.Error($"[ClockWindow] BuildLayout failed for {clockId}", ex);
        }
    }

    /// <summary>注册指定时钟组件到注册表</summary>
    private void RegisterClockComponent(string clockId, AppSettings s)
    {
        // 皮肤表盘:通过 SkinHost 包装 IClockSkin,注入指针方案/背景/双时区配置
        var skinClockIds = new[] { "analog_clock_skin", "ribbon_clock_skin", "dual_analog_clock_skin", "cyberpunk_neon_clock_skin" };
        if (Array.IndexOf(skinClockIds, clockId) >= 0)
        {
            IClockSkin skin = clockId switch
            {
                "analog_clock_skin" => new AnalogClockSkin(),
                "dual_analog_clock_skin" => new DualAnalogClockSkin(),
                "cyberpunk_neon_clock_skin" => new CyberpunkNeonSkin(),
                _ => new RibbonClockSkin()
            };
            var host = new SkinHost(skin);

            // 装载皮肤配置(若 settings.Components 中有)
            if (!s.Components.TryGetValue(clockId, out var cc))
                cc = new ComponentConfig();

            // 注入当前激活的指针方案 ID
            if (!string.IsNullOrEmpty(s.ActivePointerSetId))
                cc.Settings["pointerSetId"] = s.ActivePointerSetId;

            // 相册背景:把全局背景参数注入 SkinHost 配置
            if (s.SkinBackgroundEnabled && !string.IsNullOrWhiteSpace(s.SkinBackgroundPath))
            {
                cc.Settings["imagePath"] = s.SkinBackgroundPath;
                cc.Settings["opacity"] = s.SkinBackgroundOpacity;
                cc.Settings["blur"] = s.SkinBackgroundBlur;
                cc.Settings["mode"] = s.SkinBackgroundStretch;
            }

            // 双时区表盘:注入时区配置
            if (clockId == "dual_analog_clock_skin")
            {
                cc.Settings["secondTimeZone"] = s.DualAnalogTimeZone;
                cc.Settings["secondLabel"] = s.DualAnalogLabel;
            }

            host.Config = cc;
            host.ApplyConfig();
            _registry.Register(host);
            return;
        }

        IClockComponent comp = clockId switch
        {
            "flip_clock" => new FlipClockComponent(),
            "word_clock" => new WordClockComponent(),
            "binary_clock" => new BinaryClockComponent(),
            "minimal_clock" => new MinimalClockComponent(),
            "analog_clock" => new AnalogClockComponent(),
            "analog_premium_clock" => new AnalogPremiumClockComponent(),
            "mechanical_clock" => new MechanicalClockComponent(),
            _ => new DigitalClockComponent()
        };

        // 注入组件配置(若 settings.Components 中有)
        if (s.Components.TryGetValue(clockId, out var cc2))
            comp.Config = cc2;

        _registry.Register(comp);
    }

    /// <summary>应用主题预设颜色(与 MainWindow.ApplyThemePreset 逻辑一致)</summary>
    private static void ApplyThemePreset(DesktopClock.AppSettings s)
    {
        if (s.ThemePreset == "default") return;

        switch (s.ThemePreset)
        {
            case "dark":
                s.FontColor = "#00d4ff"; s.BorderColor = "#00d4ff"; break;
            case "light":
                s.FontColor = "#333333"; s.BorderColor = "#007aff"; break;
            case "green":
                s.FontColor = "#00ff00"; s.BorderColor = "#00ff00"; break;
            case "blue":
                s.FontColor = "#4488ff"; s.BorderColor = "#4488ff"; break;
            default: return;
        }

        s.ThemePreset = "default";
        s.Save();
    }

    /// <summary>根据 BackgroundType 应用背景(纯色/渐变)</summary>
    private void ApplyBackground(AppSettings s)
    {
        var alpha = (byte)Math.Clamp((int)(s.BackgroundOpacity * 255), 0, 255);

        Brush bg;
        if (s.BackgroundType == "gradient")
        {
            Color start, end;
            try { start = (Color)ColorConverter.ConvertFromString(s.GradientStartColor); }
            catch { start = Colors.Black; }
            try { end = (Color)ColorConverter.ConvertFromString(s.GradientEndColor); }
            catch { end = Colors.Black; }
            start.A = alpha; end.A = alpha;

            bg = new LinearGradientBrush(start, end, s.GradientAngle);
        }
        else
        {
            Color c;
            try { c = (Color)ColorConverter.ConvertFromString(s.GradientStartColor); }
            catch { c = Colors.Black; }
            c.A = alpha;
            bg = new SolidColorBrush(c);
        }

        MainBorder.Background = bg;
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
    }
}
