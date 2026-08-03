# DesktopClock 桌面时钟 v2.1.0

[![GitHub](https://img.shields.io/badge/GitHub-snqig%2FDesktopClock-blue?logo=github)](https://github.com/snqig/DesktopClock)
[![Release](https://img.shields.io/badge/Release-v2.1.0-brightgreen)](https://github.com/snqig/DesktopClock/releases)

一个基于 **WPF (.NET 9)** 的桌面时钟应用，提供 **10+ 种精美显示模式**、可插拔皮肤系统、PNG 指针方案管理器、插件体系，以及倒计时、番茄钟、习惯打卡等生产力组件。

> **v2.1.0 更新**：设置 UI 重构（删除 6 个老 Tab，全局设置下沉组件 Tab），修复表盘切换 bug，移植指针样式管理器到 ClockWindow。

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
| 指针表盘(自定义) | 自定义底图 + PNG 指针方案，支持指针样式编辑器 |
| 缎带表盘 | 流光缎带动效 |
| 双时区表盘 | 本地 + 第二时区并排显示 |
| 赛博朋克表盘 | 霓虹发光指针 + 暗色表盘 |

### 指针方案管理器

- 5 套内置预置方案（赛博朋克/科技夜光/复古/极简/幽灵蓝）
- 支持 PNG 指针素材导入
- 三根指针独立配置：旋转锚点、缩放、染色、阴影、发光、透明度
- 混搭创建：A 方案时针 + B 方案分针 + C 方案秒针
- 50ms 高频平滑走针
- PNG 加载失败自动回退矢量线条

### 皮肤系统 (IClockSkin)

- `IClockSkin` 接口抽象表盘皮肤，通过 `SkinHost` 宿主统一管理
- 内置底图：`Clock\1.PNG` 作为嵌入资源，运行时释放到 `%TEMP%\DesktopClock\default_dial.png`
- 矢量绘制指针（时/分/秒），通过 `RotateTransform` 绑定角度动态更新
- `Viewbox` 缩放确保窗口缩放时矢量元素不失真
- **相册背景通用能力**：`BackgroundWrapper` 可包裹任意表盘，叠加自定义图片背景（路径/透明度/模糊/拉伸模式）

### 独立悬浮窗口架构

每个功能模块 = 独立悬浮窗口，可自由摆放桌面、独立配置、独立开关：

| 组件 | 说明 |
|------|------|
| 时钟 | 10+ 种显示模式，右键切换指针样式编辑器 |
| 日历 | 公历日期、农历、节气、生肖 |
| 天气 | 对接 Open-Meteo API，温度 / 图标 / 日出日落 |
| 倒计时 | 多任务列表 + 自动轮播，目标时间/显示模式/结束动作 |
| 间隔提醒 | 喝水/站立/眼操等周期性健康提醒，工作时段限制 |
| 番茄钟 | 25分钟专注+5分钟休息循环，长休息间隔，今日统计 |
| 每日一言 | 每日轮换名言/诗词，跑马灯滚动，本地语录库+在线API |
| 习惯打卡 | 自定义习惯列表+点击打卡，7天热力图+今日进度条 |

### 系统集成

- **托盘图标**：快速访问设置、切换组件、退出
- **全局热键**：`Ctrl+H` 隐藏 / 显示，可自定义
- **多实例支持**：`--instance=N` 启动多个独立时钟窗口
- **AOD 省电模式**：闲置 N 分钟后隐藏秒针并降低亮度
- **跟随系统主题**：自动切换深色 / 浅色配色

### 插件系统

- 通过 `IPlugin` / `IPluginComponent` 接口扩展组件
- 运行时加载插件 DLL
- 参考示例：[Plugins/HelloWorldPlugin](Plugins/HelloWorldPlugin)

---

## 文档

| 文档 | 说明 |
|------|------|
| [技术白皮书](docs/WHITEPAPER.md) | 架构设计、核心机制、性能优化 |
| [插件接口文档](docs/PLUGIN_API.md) | IPlugin 接口、PluginHost API、插件开发指南 |
| [皮肤制作指南](docs/SKIN_GUIDE.md) | IClockSkin 接口、PNG 指针制作、指针方案配置 |

---

## 项目结构

```
DesktopClock/
├── Components/          # 时钟组件（数字/翻转/二进制/模拟/机械等）
├── Contracts/           # 插件接口（IPlugin / IPluginComponent）
├── Core/                # 核心架构
│   ├── BaseFloatWindow.cs       # 悬浮窗口基类
│   └── ComponentManager.cs      # 组件管理器单例
├── FloatWindows/        # 独立悬浮窗口
│   ├── ClockWindow.xaml(.cs)    # 时钟窗口（含指针样式管理器）
│   ├── CountdownWindow.xaml(.cs)
│   └── ...
├── Models/              # 数据模型
│   ├── AppSettings.cs           # 应用设置（含持久化与迁移）
│   ├── PointerSet.cs            # 指针方案
│   └── SinglePointerStyle.cs    # 单根指针样式
├── Services/            # 核心服务
│   ├── PointerStyleManager.cs   # 指针方案管理器
│   ├── PointerRenderer.cs       # 指针渲染器（PNG/矢量）
│   ├── PluginManager.cs         # 插件加载管理
│   └── LayoutEngine.cs          # 布局引擎
├── Skins/               # 皮肤系统
│   ├── IClockSkin.cs            # 皮肤接口
│   ├── SkinHost.cs              # 皮肤宿主
│   ├── AnalogClockSkin.xaml(.cs)        # 指针表盘
│   ├── DualAnalogClockSkin.xaml(.cs)    # 双时区表盘
│   └── RibbonClockSkin.xaml(.cs)        # 缎带表盘
├── Assets/PointerSets/  # 内置指针素材
│   ├── Cyberpunk/               # 赛博朋克（hour/minute/second.png）
│   ├── Vintage/                 # 复古
│   └── ...
├── Plugins/             # 示例插件
│   └── HelloWorldPlugin/
├── docs/                # 文档
│   ├── WHITEPAPER.md            # 技术白皮书
│   ├── PLUGIN_API.md            # 插件接口文档
│   └── SKIN_GUIDE.md            # 皮肤制作指南
└── SettingsWindow.xaml(.cs)     # 设置窗口（组件 Tab 架构）
```

---

## 技术栈

- **.NET 9** + **WPF**
- **C# 13**（Nullable 引用类型启用）
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
- 字体、颜色、背景、相册背景图片
- 布局模式与自由布局位置
- 插件启用状态
- 指针方案 ID 与皮肤配置
- 多实例、AOD 等高级选项

指针方案文件：`%LOCALAPPDATA%\DesktopClock\pointer_sets.json`

窗口位置文件：`%LOCALAPPDATA%\DesktopClock\pos.txt`

---

## 使用指南

### 切换表盘模式

设置 → 组件 → 时钟 → 显示模式 → 选择模式 → 确定

### 自定义指针

1. 切换到"指针表盘(自定义)"模式
2. 右键时钟窗口 → "指针样式编辑器"
3. 选择方案或导入 PNG → 应用

### 添加倒计时任务

设置 → 组件 → 倒计时 → 设置目标时间和标题 → 确定

### 开发插件

参考 [插件接口文档](docs/PLUGIN_API.md) 和 [HelloWorldPlugin](Plugins/HelloWorldPlugin/) 示例。

### 制作皮肤

参考 [皮肤制作指南](docs/SKIN_GUIDE.md)。

---

## 许可证

MIT
