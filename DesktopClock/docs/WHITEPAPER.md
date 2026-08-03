# DesktopClock 技术白皮书

> 版本：v2.1.0 | 更新日期：2026-08-03

## 1. 概述

DesktopClock 是一款基于 **WPF (.NET 9)** 的桌面时钟应用，采用**独立悬浮窗口架构**——每个功能模块都是一个可独立摆放、独立配置、独立开关的桌面悬浮窗口。应用提供 10+ 种时钟显示模式、可插拔皮肤系统、指针方案管理器、插件体系，以及倒计时、番茄钟、习惯打卡等生产力组件。

## 2. 设计目标

| 目标 | 实现方式 |
|------|----------|
| 模块解耦 | 每个组件 = 独立 `Window`，通过 `ComponentManager` 统一管理生命周期 |
| 点击不抢占焦点 | `WS_EX_NOACTIVATE` + `WS_EX_LAYERED` 窗口样式 |
| 配置独立 | 每个组件拥有独立的 `ComponentWindowConfig`，持久化到 `settings.json` |
| 皮肤可扩展 | `IClockSkin` 接口 + `SkinHost` 宿主，支持 PNG 指针方案与矢量回退 |
| 插件热加载 | `IPlugin` 接口 + `PluginManager` 运行时扫描 DLL |
| 跨版本兼容 | `AppSettings` 双向迁移（Flat ↔ Structured），支持 v1→v2 无感升级 |

## 3. 系统架构

```
┌─────────────────────────────────────────────────────┐
│                    App.xaml.cs                       │
│              (应用入口 / 托盘初始化)                   │
├─────────────────────────────────────────────────────┤
│              ComponentManager (单例)                  │
│   ┌──────────┐ ┌──────────┐ ┌──────────┐           │
│   │ClockWindow│ │Countdown │ │ Weather  │  ...8组件 │
│   │(BaseFloat)│ │  Window  │ │  Window  │           │
│   └─────┬─────┘ └──────────┘ └──────────┘           │
│         │                                            │
│    ┌────▼────────────────────────┐                   │
│    │   BaseFloatWindow (基类)     │                   │
│    │ • 无边框 / 透明 / 拖拽       │                   │
│    │ • 右键菜单 / 位置锁定        │                   │
│    │ • WS_EX_NOACTIVATE           │                   │
│    └─────────────────────────────┘                   │
├─────────────────────────────────────────────────────┤
│                   服务层                             │
│  ┌────────────┐ ┌──────────────┐ ┌────────────────┐ │
│  │LayoutEngine│ │SettingsProvider│ │PointerStyleMgr│ │
│  │(stack/free)│ │  (实时通知)    │ │ (PNG方案管理)  │ │
│  └────────────┘ └──────────────┘ └────────────────┘ │
│  ┌────────────┐ ┌──────────────┐ ┌────────────────┐ │
│  │PluginManager│ │ PluginHost   │ │PointerRenderer │ │
│  │(DLL扫描加载)│ │(组件注册桥)   │ │(PNG/矢量渲染)  │ │
│  └────────────┘ └──────────────┘ └────────────────┘ │
├─────────────────────────────────────────────────────┤
│                   数据层                             │
│  AppSettings ←→ %LocalAppData%/DesktopClock/        │
│    • settings.json (全局+组件配置)                   │
│    • pointer_sets.json (指针方案)                    │
│    • pos.txt (窗口位置)                              │
└─────────────────────────────────────────────────────┘
```

## 4. 核心机制

### 4.1 独立悬浮窗口

每个组件继承 `BaseFloatWindow`，具备以下特性：

- **无边框透明**：`WindowStyle=None` + `AllowsTransparency=True` + `ResizeMode=NoResize`
- **不抢占焦点**：通过 `CreateParams` 设置 `WS_EX_NOACTIVATE`，点击窗口不会激活
- **拖拽与锁定**：`MouseLeftButtonDown` 拖拽，`IsLocked` 属性锁定位置
- **右键菜单**：锁定/置顶/设置/关闭，子类可 `override OpenComponentSettings()` 自定义
- **配置驱动**：`LoadFromConfig()` / `SavePosition()` / `ApplyConfigChange()` 三个生命周期方法

### 4.2 ComponentManager 单例

```csharp
public sealed class ComponentManager
{
    // 8 个组件窗口的注册表
    private readonly Dictionary<string, BaseFloatWindow> _windows;
    // 组件工厂注册
    private readonly Dictionary<string, Func<BaseFloatWindow>> _factories;
    // 配置持久化
    private ComponentWindowConfigCollection _configs;
}
```

职责：
- 启动时按配置创建并显示已启用的组件窗口
- 托盘图标管理（双击设置、右键菜单切换组件）
- 设置变更后 `NotifyConfigChange()` 通知所有组件 `ApplyConfigChange()`
- 退出时统一保存所有窗口位置

### 4.3 配置体系

```
settings.json
├── Flat 属性 (向后兼容 v1)
│   ├── FontSize / FontColor / FontFamily
│   ├── DisplayMode / BackgroundType
│   └── ...60+ 扁平字段
├── Global (结构化全局配置)
├── Layout (布局模式)
├── Components (组件配置字典)
│   ├── "clock": { Enabled, Topmost, LockPosition, Opacity, Settings{...} }
│   ├── "countdown": { ... }
│   └── ...
├── PointerSets (指针方案数组)
└── Plugins (插件启用状态)
```

**双向迁移**：`Save()` 时调用 `PopulateStructuredFromFlat()` 同步 Flat→Structured；`Load()` 时调用 `MigrateFlatFromStructured()` 同步 Structured→Flat，确保两套访问方式数据一致。

### 4.4 显示模式切换

`ClockWindow.RebuildClockLayout()` 根据 `Settings.DisplayMode` 映射到组件 ID：

| DisplayMode | clockId | 渲染方式 |
|-------------|---------|----------|
| digital | digital_clock | TextBlock + DataSource |
| flip | flip_clock | 翻转动画 |
| analog_premium | analog_premium_clock | 玻璃圆盘 + 光晕 |
| mechanical | mechanical_clock | 齿轮 + 铆钉 |
| analog_skin | analog_clock_skin | SkinHost + PNG/矢量指针 |
| cyberpunk | cyberpunk_neon_clock_skin | SkinHost + 霓虹发光 |

皮肤模式通过 `SkinHost` 包装 `IClockSkin`，注入指针方案 ID 和背景参数。

### 4.5 指针方案管理器

```
PointerStyleManager
├── 数据源: pointer_sets.json
├── 内置 5 套预置 (Cyberpunk/GlowTech/Vintage/Minimal/GhostBlue)
├── 操作: Add / Update / Delete / Duplicate / CreateMix (混搭)
└── 注入: AnalogClockSkin.StyleManager (静态属性)
```

**PointerSet 结构**：
- 三根指针独立配置（`HourStyle` / `MinuteStyle` / `SecondStyle`）
- 每根支持：PNG 路径、旋转锚点 (X/Y)、缩放、染色、阴影、发光、透明度
- PNG 加载失败自动回退矢量 Line 指针

**渲染流程**：
1. `PointerRenderer.CreateOrUpdate()` 创建 `Image` 元素
2. `RenderTransformOrigin` 设为锚点比例坐标
3. `Canvas.SetLeft/Top` 定位使锚点对准表盘中心 (200, 200)
4. 50ms 高频 Timer 调用 `UpdateAngle()` 实现平滑走针

## 5. 皮肤系统

### 5.1 接口定义

```csharp
public interface IClockSkin
{
    string Id { get; }
    string DisplayName { get; }
    FrameworkElement View { get; }
    void UpdateTime(DateTime now);
    void LoadConfig(Dictionary<string, object> config);
    Dictionary<string, object> SaveConfig();
}
```

### 5.2 SkinHost 宿主

`SkinHost` 实现 `IClockComponent`，作为 `IClockSkin` 与组件注册表之间的桥梁：
- 构造时将皮肤 `View` 加入 `Grid`
- `ApplyConfig()` 调用 `LoadConfig()` 并叠加相册背景
- `Update()` 转发到皮肤的 `UpdateTime()`

### 5.3 相册背景

`BackgroundWrapper` / `SkinHost.ApplyBackground()` 为任意表盘叠加图片背景：
- 配置项：路径 / 透明度 (0-1) / 模糊 (0-40px) / 拉伸模式 (UniformToFill/Uniform/Fill)
- 背景层插入 `Grid.Children[0]`（最底层），Tag 标记 `"skin-background"`

## 6. 插件系统

### 6.1 接口

```csharp
public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Description { get; }
    void Load(PluginHost host);
    void Unload();
}

public interface IPluginComponent : IPlugin
{
    string ComponentId { get; }
    FrameworkElement View { get; }
}
```

### 6.2 加载流程

1. `PluginManager.LoadAll()` 扫描 `Plugins/` 目录下所有 `.dll`
2. 通过反射查找实现 `IPlugin` 的类型
3. 检查 `settings.json` 中 `Plugins` 字典的启用状态
4. 创建 `PluginHost` 实例，调用 `plugin.Load(host)`
5. 插件通过 `host.RegisterComponent()` 将视图注册到组件注册表

详见 [插件接口文档](PLUGIN_API.md)。

## 7. 性能优化

| 策略 | 实现 |
|------|------|
| 高频走针 | 50ms DispatcherTimer，仅更新 RotateTransform.Angle |
| 配置缓存 | SettingsProvider 单例 + 事件通知，避免重复读文件 |
| 图像冻结 | `BitmapImage.Freeze()` 跨线程安全 |
| 闲置降帧 | AOD 模式闲置 N 分钟后隐藏秒针、降低亮度 |
| 模式切换防抖 | `RebuildClockLayout` 检测 clockId 未变则仅 `ApplyAllConfig()` |

## 8. 文件清单

| 路径 | 用途 |
|------|------|
| `%LocalAppData%/DesktopClock/settings.json` | 全局 + 组件配置 |
| `%LocalAppData%/DesktopClock/pointer_sets.json` | 指针方案 |
| `%LocalAppData%/DesktopClock/pos.txt` | 窗口位置 |
| `%TEMP%/DesktopClock/default_dial.png` | 内置底图 |
| `Assets/PointerSets/{方案名}/{hour,minute,second}.png` | 内置指针素材 |

## 9. 版本演进

| 版本 | 关键变更 |
|------|----------|
| v1.0.x | 统一容器架构，UserControl 模式 |
| v2.0.0 | 独立悬浮窗口重构，8 组件迁移，SettingsWindow 组件 Tab |
| v2.1.0 | 设置 UI 重构（删除 6 个老 Tab，全局设置下沉组件 Tab），修复表盘切换 bug，移植指针样式管理器到 ClockWindow |

## 10. 许可证

MIT License
