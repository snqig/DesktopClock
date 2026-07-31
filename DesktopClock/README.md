# DesktopClock 桌面时钟 v1.0.5

[![GitHub](https://img.shields.io/badge/GitHub-snqig%2FDesktopClock-blue?logo=github)](https://github.com/snqig/DesktopClock)

一个基于 **WPF (.NET 9)** 的桌面时钟应用，提供 **10+ 种精美显示模式**、可插拔皮肤系统、自由布局、插件体系、系统监控与提醒功能。

> **最新 Release**：前往 [Releases](https://github.com/snqig/DesktopClock/releases) 页面下载已打包好的 `.exe` 单文件版本，无需安装 .NET 运行时即可运行（自包含发布）。

---

## 下载与安装

| 方式 | 说明 |
|------|------|
| **Releases 下载** | 访问 [https://github.com/snqig/DesktopClock/releases](https://github.com/snqig/DesktopClock/releases) 下载最新 `DesktopClock.exe` |
| **自行编译** | 克隆仓库后执行 `dotnet publish`（见下方[快速开始](#快速开始)） |

系统要求：Windows 10 / Windows 11

---

## 功能特性

### 多种显示模式

| 模式 | 说明 |
|------|------|
| 数字时钟 | 经典数字显示，支持 HH:MM:SS |
| 翻转时钟 | 卡片翻转动画 |
| 二进制时钟 | 二进制点阵显示 |
| 模拟时钟 | 简约圆盘指针时钟 |
| 超精美模拟时钟 | 玻璃圆盘 + 旋转光晕 + 轨道点 |
| 机械时钟 | 金属齿轮表盘 + 铆钉 + 机械指针 |
| 极简时钟 | 极简数字显示 |
| 指针表盘(自定义) | 自定义底图 + 矢量指针，颜色/粗细/刻度可配置 |
| 缎带表盘 | 流光缎带动效 |
| 双时区表盘 | 本地 + 第二时区并排显示 |

### 皮肤系统 (IClockSkin)

- `IClockSkin` 接口抽象表盘皮肤，通过 `SkinHost` 宿主统一管理
- 内置底图：`Clock\1.PNG` 作为嵌入资源，运行时释放到 `%TEMP%\DesktopClock\default_dial.png`
- 矢量绘制指针（时/分/秒），通过 `RotateTransform` 绑定角度动态更新
- `Viewbox` 缩放确保窗口缩放时矢量元素不失真
- 配置导入导出：序列化为 `.dskin` JSON 文件，支持分享与备份
- **相册背景通用能力**：`BackgroundWrapper` 可包裹任意表盘，叠加自定义图片背景（路径/透明度/模糊/拉伸模式）
  - 仅指针表盘（自定义 / 缎带 / 双时区）启用相册背景
  - 切换到其他表盘时自动关闭，避免背景层残留

### 布局与交互

- **Stack 布局（默认）**：组件垂直排列，简洁紧凑，无标题栏标签
- **Free 布局**：自由拖拽定位，支持锁定 / 删除 / 重置大小
- **可视化布局编辑器**：右键菜单"编辑布局模式"启用拖拽，退出自动保存
- **日期位置**：top / bottom 任选，自动调整组件顺序
- **窗口置顶 / 透明背景 / 鼠标穿透**：可选点击穿透，按住 Ctrl 可拖动穿透窗口
- **多实例支持**：`--instance=N` 启动多个独立时钟窗口，各自独立配置与托盘图标

### 可扩展组件

| 组件 | 说明 |
|------|------|
| 日期 / 农历 | 公历日期、农历、节气、生肖 |
| 世界时钟 | 自定义时区 |
| 系统监控 | CPU / 内存 / 网速 / 电池，跑马灯滚动显示，支持字体/颜色自定义 |
| 天气 | 对接 Open-Meteo API，温度 / 图标 / 日出日落 |
| 倒计时 | 自定义目标时间与标签，支持日期控件选择，归零显示 `00:00:00` |
| 滚动待办 | 跑马灯平滑滚动，宽度与时钟对齐，支持字体/颜色自定义 |
| 音乐播放信息 | Windows.Media.Control 获取当前播放歌曲 |

### 自定义外观

- 字体（支持系统字体下拉选择）、字号、颜色全可配置
- 纯色 / 渐变背景
- 12/24 小时制，显隐秒数
- 指针表盘：时针 / 分针 / 秒针颜色、粗细倍率（0.3x-3.0x）、刻度颜色与显隐、中心点显隐
- 全局滤镜：暗角 / 灰度 / 色温调节
- Windows 11 Mica / Acrylic / Tabbed 背景效果（通过 DWM API）

### 系统集成

- **AOD 省电模式**：`GetLastInputInfo` 检测系统闲置，闲置 N 分钟后隐藏秒针并降低亮度
- **跟随系统主题**：监听 `UserPreferenceChanged`，自动切换深色 / 浅色配色
- **定时自动切换表盘**：按设定时段切换白天 / 夜间表盘
- **托盘图标**：快速访问设置、切换表盘、退出
- **全局热键**：`Ctrl+H` 隐藏 / 显示，可自定义

### 提醒系统

- 自定义提醒时间与内容
- 通知弹窗提醒
- 重复提醒去重

### 插件系统

- 通过 `IPlugin` / `IPluginComponent` 接口扩展组件
- 运行时加载插件程序集
- 参考示例：[Plugins/HelloWorldPlugin](Plugins/HelloWorldPlugin)

---

## 项目结构

```
DesktopClock/
├── Components/          # 时钟组件（数字/翻转/二进制/模拟/机械等）
│   ├── IClockComponent.cs       # 组件接口
│   ├── ClockComponentBase.cs    # 组件基类
│   ├── ComponentRegistry.cs     # 组件注册中心
│   └── *Component.xaml(.cs)     # 各显示模式组件
├── Contracts/           # 插件接口
│   ├── IPlugin.cs
│   └── IPluginComponent.cs
├── Models/              # 数据模型
│   ├── AppSettings.cs           # 应用设置（含持久化与迁移）
│   ├── GlobalConfig.cs          # 全局配置
│   ├── LayoutConfig.cs          # 布局配置
│   ├── ComponentConfig.cs       # 组件配置
│   └── ComponentPosition.cs     # 自由布局位置
├── Services/            # 核心服务
│   ├── LayoutEngine.cs          # 布局引擎（stack/free 双模式）
│   ├── SettingsProvider.cs      # 设置单例（实时通知）
│   ├── PluginHost.cs            # 插件宿主
│   ├── PluginManager.cs         # 插件加载管理
│   └── WindowBackdrop.cs        # Mica/Acrylic 背景效果
├── Skins/               # 皮肤系统
│   ├── IClockSkin.cs            # 皮肤接口
│   ├── SkinHost.cs              # 皮肤宿主（管理激活皮肤）
│   ├── BackgroundWrapper.cs     # 相册背景通用包装器
│   ├── SkinBackgroundConfig.cs  # 背景配置
│   ├── AnalogClockSkin.xaml(.cs)        # 指针表盘（自定义）
│   ├── DualAnalogClockSkin.xaml(.cs)    # 双时区表盘
│   └── RibbonClockSkin.xaml(.cs)        # 缎带表盘
├── Plugins/             # 示例插件
│   └── HelloWorldPlugin/
├── Clock/               # 内置表盘底图资源
│   └── 1.png
├── MainWindow.xaml(.cs)         # 主窗口
├── SettingsWindow.xaml(.cs)     # 设置窗口
├── ReminderDialog.xaml(.cs)     # 提醒对话框
├── LunarCalendar.cs             # 农历计算
└── App.xaml(.cs)                # 应用入口
```

---

## 技术栈

- **.NET 9** + **WPF**
- **C# 13**（Nullable 引用类型启用）
- **FontAwesome.WPF** 图标库
- **System.Windows.Extensions**（WinForms 互操作用于托盘与系统 API）
- 无第三方 UI 框架，纯原生 WPF 实现

---

## 快速开始

### 环境要求

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows 10 / 11

### 克隆仓库

```bash
git clone https://github.com/snqig/DesktopClock.git
cd DesktopClock
```

### 构建运行

```bash
dotnet build
dotnet run
```

### 发布单文件（Releases 用）

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:TrimMode=partial
```

发布后的可执行文件位于：
```
bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\DesktopClock.exe
```

### 启动多实例

```bash
DesktopClock.exe --instance=1
DesktopClock.exe --instance=2
```

每个实例使用独立的配置文件 `settings_instance_{N}.json` 与位置文件 `pos_instance_{N}.txt`。

---

## 配置

设置文件位于 `%LOCALAPPDATA%\DesktopClock\settings.json`，包含：

- 显示模式与组件启用状态
- 字体、颜色、背景、全局滤镜
- 布局模式与自由布局位置
- 提醒列表
- 插件启用状态
- 皮肤配置（存储在 `Components["analog_clock_skin"]` 等字典中）
- 多实例、AOD、自动切换表盘等高级选项

窗口位置保存于 `%LOCALAPPDATA%\DesktopClock\pos.txt`。

> 说明：序列化使用 `System.Text.Json` 并启用 `AllowNamedFloatingPointLiterals`，以兼容 `Infinity` / `NaN` 等特殊浮点值。

---

## 开发指南

### 新增显示模式

1. 在 `Components/` 下创建 `XxxClockComponent.xaml(.cs)`
2. 实现 `IClockComponent` 接口（或继承 `ClockComponentBase`）
3. 在 [MainWindow.xaml.cs](MainWindow.xaml.cs) 的 `RegisterComponents` 中注册
4. 在 [MainWindow.xaml.cs](MainWindow.xaml.cs) 的 `RebuildLayout` 的 `DisplayMode` switch 中添加映射
5. 在 [SettingsWindow.xaml](SettingsWindow.xaml) 的显示模式 ComboBox 中添加选项

### 新增皮肤 (Skin)

1. 在 `Skins/` 下创建 `XxxClockSkin.xaml(.cs)`，实现 `IClockSkin`
2. 在 [MainWindow.xaml.cs](MainWindow.xaml.cs) 的 `RebuildLayout` 中加入 `clockId` 分支与 `SkinHost` 包装逻辑
3. 皮肤配置存储在 `ComponentConfig.Settings` 字典，导出 `.dskin` 时序列化为 JSON

### 开发插件

1. 创建类库项目，引用 `Contracts/IPlugin.cs` 与 `IPluginComponent.cs`
2. 实现 `IPlugin` 接口
3. 编写 `manifest.json`
4. 将编译产物放入插件目录

参考 [Plugins/HelloWorldPlugin](Plugins/HelloWorldPlugin/README.md)。

---

## 工程约定

- 所有模式默认使用 Stack 布局，组件上方不显示任何名称标签
- 重建布局时需先断开视图元素与旧父容器的逻辑关系（`DetachFromParent`）
- `ThemePreset` 应用预设颜色后应重置为 `'default'` 以允许用户手动修改
- 皮肤配置存储在 `ComponentConfig.Settings` 字典，导出 `.dskin` 时序列化为 JSON
- 所有图片资源路径使用相对路径，兼容配置导出
- 默认优先矢量渲染，图片仅作为表盘底图，动态旋转元素尽量不用图片

---

## 许可证

MIT
