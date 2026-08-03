# DesktopClock 皮肤制作指南

> 版本：v2.1.0 | 适用于 DesktopClock v2.0.0+

## 1. 概述

DesktopClock 皮肤系统通过 `IClockSkin` 接口实现可换肤表盘。皮肤由 `SkinHost` 宿主装载，支持 PNG 指针方案、矢量回退、相册背景叠加。本文档指导你从零创建一个自定义皮肤。

## 2. 皮肤接口

```csharp
namespace DesktopClock.Skins;

public interface IClockSkin
{
    /// <summary>皮肤唯一 ID（如 "my_clock_skin"）</summary>
    string Id { get; }

    /// <summary>显示名称（如 "我的皮肤"）</summary>
    string DisplayName { get; }

    /// <summary>WPF 视图元素（通常为 UserControl）</summary>
    FrameworkElement View { get; }

    /// <summary>每秒调用一次，刷新指针角度等动态元素</summary>
    void UpdateTime(DateTime now);

    /// <summary>从配置字典加载皮肤参数</summary>
    void LoadConfig(Dictionary<string, object> config);

    /// <summary>导出当前配置（用于持久化/分享）</summary>
    Dictionary<string, object> SaveConfig();
}
```

## 3. 指针方案（PointerSet）

### 3.1 数据结构

每个指针方案包含三根独立配置的指针：

```csharp
public class PointerSet
{
    public string Id { get; set; }
    public string Name { get; set; }       // 方案名称
    public string Category { get; set; }    // 分类：简约/复古/夜光/极简/自制/商务
    public SinglePointerStyle HourStyle { get; set; }    // 时针
    public SinglePointerStyle MinuteStyle { get; set; }  // 分针
    public SinglePointerStyle SecondStyle { get; set; }  // 秒针
    public bool Favorite { get; set; }      // 收藏置顶
}
```

### 3.2 单根指针样式

```csharp
public class SinglePointerStyle
{
    /// <summary>PNG 路径（相对/绝对/pack URI），空则回退矢量线条</summary>
    public string ImagePath { get; set; }

    /// <summary>旋转锚点 X（0~1，0.5=水平中点）</summary>
    public double RotationCenterX { get; set; } = 0.5;

    /// <summary>旋转锚点 Y（0~1，1.0=底部=指针根部贴合圆心）</summary>
    public double RotationCenterY { get; set; } = 1.0;

    /// <summary>缩放比例（1.0=原始尺寸）</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>染色滤镜（HEX 如 "#00FFFF"，空=不着色）</summary>
    public string ColorTint { get; set; }

    /// <summary>阴影开关</summary>
    public bool ShadowEnabled { get; set; }

    /// <summary>外发光强度（0=关，1~10 递增）</summary>
    public double GlowIntensity { get; set; }

    /// <summary>透明度（0~1）</summary>
    public double Opacity { get; set; } = 1.0;
}
```

## 4. PNG 指针素材制作

### 4.1 尺寸规范

| 参数 | 值 |
|------|-----|
| 画布尺寸 | 200 × 200 px（推荐） |
| 透明背景 | 必须（PNG alpha 通道） |
| 指针方向 | 指向上方（12 点方向），根部在底部中点 |
| 格式 | PNG 32-bit（带 alpha） |

### 4.2 锚点说明

```
┌─────────────┐
│      ▲       │  ← 指针尖端（顶部）
│      │       │
│      │       │
│      │       │
│      ●       │  ← 旋转锚点（底部中点）
└─────────────┘
RotationCenterX = 0.5  (水平居中)
RotationCenterY = 1.0  (底部=根部)
```

- **时针**：较短较粗，约画布高度的 50%
- **分针**：较长较细，约画布高度的 75%
- **秒针**：最长最细，约画布高度的 85%，可加尾端配重

### 4.3 文件目录结构

```
Assets/PointerSets/
├── Cyberpunk/
│   ├── hour.png
│   ├── minute.png
│   └── second.png
├── Vintage/
│   ├── hour.png
│   ├── minute.png
│   └── second.png
└── MyCustom/
    ├── hour.png
    ├── minute.png
    └── second.png
```

路径在 `SinglePointerStyle.ImagePath` 中使用相对路径：
```json
"ImagePath": "Assets/PointerSets/MyCustom/hour.png"
```

## 5. 指针方案文件

### 5.1 pointer_sets.json

指针方案持久化在 `%LocalAppData%\DesktopClock\pointer_sets.json`：

```json
[
  {
    "Id": "abc123def456",
    "Name": "我的指针方案",
    "Category": "自制",
    "HourStyle": {
      "ImagePath": "Assets/PointerSets/MyCustom/hour.png",
      "RotationCenterX": 0.5,
      "RotationCenterY": 1.0,
      "Scale": 1.0,
      "ColorTint": "",
      "ShadowEnabled": false,
      "GlowIntensity": 3.0,
      "Opacity": 1.0
    },
    "MinuteStyle": { "..." },
    "SecondStyle": { "..." },
    "Favorite": false
  }
]
```

### 5.2 内置预置方案

| 方案名 | 分类 | 特点 |
|--------|------|------|
| Cyberpunk | 夜光 | 赛博朋克霓虹发光 |
| GlowTech | 夜光 | 科技夜光 |
| Vintage | 复古 | 复古经典 |
| Minimal | 极简 | 极简细针 |
| GhostBlue | 自制 | 幽灵蓝 |

## 6. 创建自定义皮肤

### 6.1 实现 IClockSkin

```csharp
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DesktopClock.Skins;

public partial class MyClockSkin : UserControl, IClockSkin
{
    public string Id => "my_clock_skin";
    public string DisplayName => "我的皮肤";
    public FrameworkElement View => this;

    private readonly DispatcherTimer _timer;
    private Line _hourHand, _minuteHand, _secondHand;
    private RotateTransform _hourRotate, _minuteRotate, _secondRotate;

    public MyClockSkin()
    {
        InitializeComponent();
        BuildDial();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) => UpdateSmoothHands();
        _timer.Start();
        this.Unloaded += (_, _) => _timer.Stop();
    }

    public void UpdateTime(DateTime now) => UpdateSmoothHands();

    private void UpdateSmoothHands()
    {
        var now = DateTime.Now;
        double ms = now.Millisecond / 1000.0;
        double sec = now.Second + ms;
        double min = now.Minute + sec / 60.0;
        double hour = (now.Hour % 12) + min / 60.0;

        _hourRotate.Angle = hour * 30.0;
        _minuteRotate.Angle = min * 6.0;
        _secondRotate.Angle = sec * 6.0;
    }

    public void LoadConfig(Dictionary<string, object> config)
    {
        // 读取颜色、粗细等配置
        if (config.TryGetValue("hourColor", out var hc))
        {
            try { _hourHand.Stroke = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(hc.ToString()!)); } catch { }
        }
    }

    public Dictionary<string, object> SaveConfig() => new()
    {
        ["hourColor"] = "#333333"
    };

    private void BuildDial()
    {
        // 绘制表盘刻度和指针
        // ... XAML 或代码构建
    }
}
```

### 6.2 注册皮肤

在 `ClockWindow.RegisterClockComponent()` 中添加映射：

```csharp
IClockSkin skin = clockId switch
{
    "analog_clock_skin" => new AnalogClockSkin(),
    "my_clock_skin" => new MyClockSkin(),  // ← 新增
    _ => new RibbonClockSkin()
};
var host = new SkinHost(skin);
// 注入配置...
_registry.Register(host);
```

在 `ClockWindow.RebuildClockLayout()` 的 switch 中添加：

```csharp
var clockId = s.DisplayMode switch
{
    "my_mode" => "my_clock_skin",  // ← 新增
    // ...
};
```

在 `ClockWindow.GetWindowSizeForMode()` 中添加窗口尺寸：

```csharp
var size = mode switch
{
    "my_mode" => 400.0,  // ← 新增
    // ...
};
```

## 7. 相册背景

### 7.1 配置

相册背景通过 `SkinHost.ApplyBackground()` 叠加到任意表盘：

```json
{
  "imagePath": "C:/Pictures/my_bg.jpg",
  "opacity": 0.8,
  "blur": 5,
  "mode": "UniformToFill"
}
```

| 参数 | 类型 | 说明 |
|------|------|------|
| imagePath | string | 图片路径（相对/绝对） |
| opacity | double | 透明度 0~1 |
| blur | double | 模糊半径 0~40 px |
| mode | string | 拉伸模式：UniformToFill / Uniform / Fill |

### 7.2 设置入口

设置窗口 → 组件 → 时钟 → 背景图片：
- 启用相册背景图片（开关）
- 图片路径（文本框 + 浏览按钮）
- 透明度（滑块 0-100%）
- 模糊（滑块 0-40px）
- 拉伸模式（下拉框）

## 8. 指针样式编辑器

### 8.1 打开方式

1. 设置 → 组件 → 时钟 → 显示模式选"指针表盘(自定义)"保存
2. 右键时钟窗口 → "指针样式编辑器"

### 8.2 功能

- **方案列表**：左侧显示所有方案，收藏置顶
- **实时预览**：右侧表盘预览，修改参数即时反映
- **混搭创建**：选择 A 方案时针 + B 方案分针 + C 方案秒针，另存为新方案
- **导入 PNG**：从本地导入自定义指针素材
- **分类管理**：按 简约/复古/夜光/极简/自制/商务 分类筛选

### 8.3 应用方案

点击"应用"后：
1. 方案 ID 写入 `AppSettings.ActivePointerSetId`
2. 同步到所有皮肤组件配置 `Components["analog_clock_skin"].Settings["pointerSetId"]`
3. 保存到 `settings.json`
4. 刷新所有 `SkinHost` 实例，立即生效

## 9. 渲染原理

### 9.1 PNG 指针渲染流程

```
PointerRenderer.CreateOrUpdate()
  │
  ├─ 1. LoadImage(style.ImagePath)  → BitmapSource (Freeze)
  ├─ 2. 创建/复用 Image 元素
  ├─ 3. RenderTransformOrigin = (RotationCenterX, RotationCenterY)
  ├─ 4. 计算显示尺寸: baseSize / maxDim * Scale
  ├─ 5. Canvas.SetLeft/Top: 使锚点对准表盘中心 (200, 200)
  ├─ 6. RenderTransform = RotateTransform { Angle }
  └─ 7. ApplyEffects: DropShadowEffect (染色/阴影/发光)
```

### 9.2 平滑走针

- 50ms DispatcherTimer 高频刷新
- 使用 `DateTime.Now` 毫秒精度计算角度
- 仅更新 `RotateTransform.Angle`，不重建元素
- 秒针连续滑动（非跳秒）

### 9.3 矢量回退

当 `ImagePath` 为空或 PNG 加载失败时：
- 隐藏 Image 元素
- 显示矢量 `Line` 指针
- 颜色/粗细由 `HourColor` / `MinuteColor` / `SecondColor` / `HandThickness` 控制

## 10. 调试技巧

### 10.1 查看日志

程序日志输出到控制台 / 调试器：
```
[ClockWindow] RebuildClockLayout: DisplayMode=analog_skin, clockId=analog_clock_skin
[ClockWindow] BuildLayout done for analog_clock_skin, ContentHost.Children.Count=1
```

### 10.2 配置检查

```bash
# 查看当前配置
type "%LocalAppDATA%\DesktopClock\settings.json"

# 查看指针方案
type "%LocalAppDATA%\DesktopClock\pointer_sets.json"
```

### 10.3 常见问题

| 问题 | 原因 | 解决 |
|------|------|------|
| 指针不显示 | PNG 路径错误 | 检查 `ImagePath` 是否相对于 exe 目录 |
| 指针位置偏移 | 锚点设置错误 | `RotationCenterY` 应为 1.0（底部） |
| 切换表盘无效 | DisplayMode 未保存 | 确认设置窗口"组件→时钟"中选择了模式 |
| 背景不显示 | 图片路径/格式问题 | 使用绝对路径，确保 PNG/JPG 可读 |
| 指针方案不生效 | StyleManager 未注入 | 确保通过 ClockWindow 右键菜单打开编辑器 |

## 11. 完整示例参考

仓库内置皮肤实现：

| 文件 | 说明 |
|------|------|
| [AnalogClockSkin.xaml.cs](../Skins/AnalogClockSkin.xaml.cs) | 指针表盘（PNG + 矢量回退） |
| [DualAnalogClockSkin.xaml.cs](../Skins/DualAnalogClockSkin.xaml.cs) | 双时区表盘 |
| [RibbonClockSkin.xaml.cs](../Skins/RibbonClockSkin.xaml.cs) | 缎带流光表盘 |
| [CyberpunkNeonSkin.xaml.cs](../Skins/CyberpunkNeonSkin.xaml.cs) | 赛博朋克霓虹表盘 |
