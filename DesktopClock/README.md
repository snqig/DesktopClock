# DesktopClock

一个基于 WPF (.NET 9) 的桌面时钟应用,提供多种精美显示模式、自由布局、插件系统和提醒功能。

## 功能特性

### 多种显示模式

| 模式 | 说明 |
|------|------|
| 数字时钟 | 经典数字显示 |
| 翻转时钟 | 卡片翻转动画 |
| 二进制时钟 | 二进制点阵显示 |
| 模拟时钟 | 简约圆盘指针时钟 |
| 超精美模拟时钟 | 玻璃圆盘 + 旋转光晕 + 轨道点 |
| 机械时钟 | 金属齿轮表盘 + 铆钉 + 机械指针 |
| 极简时钟 | 极简数字显示 |

### 布局与交互

- **Stack 布局**:组件垂直排列,简洁紧凑
- **Free 布局**:自由拖拽定位,支持锁定/删除/重置大小
- **组件管理**:日期、农历、世界时钟、主时钟可独立显隐
- **窗口置顶**:可选始终置于最前
- **透明背景**:模拟时钟模式下圆盘外完全透明
- **鼠标穿透**:可选点击穿透至下层窗口

### 自定义外观

- 字体、字号、颜色全可配置
- 纯色 / 渐变背景
- 12/24 小时制
- 显隐秒数

### 提醒系统

- 自定义提醒时间与内容
- 通知弹窗提醒
- 重复提醒去重

### 插件系统

- 通过 `IPlugin` / `IPluginComponent` 接口扩展组件
- 运行时加载插件程序集
- 参考示例:[Plugins/HelloWorldPlugin](Plugins/HelloWorldPlugin)

### 农历支持

- 农历日期显示
- 传统节日与节气

## 项目结构

```
DesktopClock/
├── Components/          # 时钟组件(数字/翻转/二进制/模拟/机械等)
│   ├── IClockComponent.cs       # 组件接口
│   ├── ClockComponentBase.cs    # 组件基类
│   ├── ComponentRegistry.cs     # 组件注册中心
│   └── *Component.xaml(.cs)     # 各显示模式组件
├── Contracts/           # 插件接口
│   ├── IPlugin.cs
│   └── IPluginComponent.cs
├── Models/              # 数据模型
│   ├── AppSettings.cs           # 应用设置(含持久化与迁移)
│   ├── GlobalConfig.cs          # 全局配置
│   ├── LayoutConfig.cs          # 布局配置
│   ├── ComponentConfig.cs       # 组件配置
│   └── ComponentPosition.cs     # 自由布局位置
├── Services/            # 核心服务
│   ├── LayoutEngine.cs          # 布局引擎(stack/free 双模式)
│   ├── SettingsProvider.cs      # 设置单例(实时通知)
│   ├── PluginHost.cs            # 插件宿主
│   └── PluginManager.cs         # 插件加载管理
├── Plugins/             # 示例插件
│   └── HelloWorldPlugin/
├── MainWindow.xaml(.cs) # 主窗口
├── SettingsWindow.xaml(.cs) # 设置窗口
├── ReminderDialog.xaml(.cs)  # 提醒对话框
├── LunarCalendar.cs     # 农历计算
└── App.xaml(.cs)        # 应用入口
```

## 技术栈

- **.NET 9** + **WPF**
- **C# 13** (Nullable 引用类型启用)
- **FontAwesome.WPF** 图标库
- 无第三方 UI 框架,纯原生 WPF 实现

## 快速开始

### 环境要求

- .NET 9 SDK
- Windows 10/11

### 构建运行

```bash
dotnet build
dotnet run
```

### 发布单文件

```bash
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

## 配置

设置文件位于 `%LOCALAPPDATA%\DesktopClock\settings.json`,包含:

- 显示模式与组件启用状态
- 字体、颜色、背景
- 布局模式与自由布局位置
- 提醒列表
- 插件启用状态

窗口位置保存于 `%LOCALAPPDATA%\DesktopClock\pos.txt`。

## 开发指南

### 新增显示模式

1. 在 `Components/` 下创建 `XxxClockComponent.xaml(.cs)`
2. 实现 `IClockComponent` 接口(或继承 `ClockComponentBase`)
3. 在 [MainWindow.xaml.cs](MainWindow.xaml.cs) 的 `RegisterComponents` 中注册
4. 在 [MainWindow.xaml.cs](MainWindow.xaml.cs) 的 `DisplayMode` switch 中添加映射
5. 在 [SettingsWindow.xaml](SettingsWindow.xaml) 的显示模式 ComboBox 中添加选项

### 开发插件

1. 创建类库项目,引用 `Contracts/IPlugin.cs` 与 `IPluginComponent.cs`
2. 实现 `IPlugin` 接口
3. 编写 `manifest.json`
4. 将编译产物放入插件目录

参考 [Plugins/HelloWorldPlugin](Plugins/HelloWorldPlugin/README.md)。

## 许可证

MIT
