# DesktopClock 插件接口文档

> 版本：v2.1.0 | 适用于 DesktopClock v2.0.0+

## 1. 概述

DesktopClock 插件系统允许开发者通过编写 .NET 类库（DLL）扩展桌面时钟功能。插件在运行时被 `PluginManager` 扫描加载，通过 `PluginHost` 与主程序交互。

## 2. 接口定义

### 2.1 IPlugin（基础插件接口）

```csharp
namespace DesktopClock.Contracts;

public interface IPlugin
{
    /// <summary>插件唯一 ID（用于配置启用/禁用状态）</summary>
    string Id { get; }

    /// <summary>显示名称</summary>
    string Name { get; }

    /// <summary>版本号（如 "1.0.0"）</summary>
    string Version { get; }

    /// <summary>描述信息</summary>
    string Description { get; }

    /// <summary>加载时调用，插件在此注册组件、初始化资源</summary>
    void Load(PluginHost host);

    /// <summary>卸载时调用，插件在此清理资源</summary>
    void Unload();
}
```

### 2.2 IPluginComponent（带 UI 视图的插件）

```csharp
namespace DesktopClock.Contracts;

public interface IPluginComponent : IPlugin
{
    /// <summary>组件 ID（注册到 ComponentRegistry 的键）</summary>
    string ComponentId { get; }

    /// <summary>WPF 视图元素（UserControl 等）</summary>
    FrameworkElement View { get; }
}
```

## 3. PluginHost API

`PluginHost` 是主程序提供给插件的交互桥梁：

```csharp
namespace DesktopClock.Services;

public class PluginHost
{
    /// <summary>插件所在目录（用于加载插件自带资源）</summary>
    public string PluginDirectory { get; }

    /// <summary>注册一个组件视图到布局系统</summary>
    /// <param name="id">组件唯一 ID（不能与内置组件冲突）</param>
    /// <param name="view">WPF 视图元素</param>
    public void RegisterComponent(string id, FrameworkElement view);

    /// <summary>注销组件</summary>
    public void UnregisterComponent(string id);

    /// <summary>写入日志（显示在设置窗口的插件列表中）</summary>
    public void Log(string message);
}
```

### 内置组件 ID（不可冲突）

| ID | 组件 |
|----|------|
| `clock` | 数字时钟 |
| `calendar` | 日历 |
| `weather` | 天气 |
| `countdown` | 倒计时 |
| `interval_reminder` | 间隔提醒 |
| `pomodoro` | 番茄钟 |
| `daily_sentence` | 每日一言 |
| `habit_check` | 习惯打卡 |

## 4. 快速开始

### 4.1 创建项目

```bash
dotnet new classlib -n MyPlugin -f net9.0-windows
cd MyPlugin
```

### 4.2 编辑 csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

### 4.3 实现插件

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopClock.Contracts;
using DesktopClock.Services;

namespace MyPlugin;

public class MyClockPlugin : IPlugin
{
    private PluginHost? _host;

    public string Id => "my_clock_plugin";
    public string Name => "我的插件";
    public string Version => "1.0.0";
    public string Description => "示例插件：在桌面显示自定义内容";

    public void Load(PluginHost host)
    {
        _host = host;
        _host.Log("我的插件已加载");

        var view = new MyPluginView();
        _host.RegisterComponent(Id, view);
    }

    public void Unload()
    {
        _host?.UnregisterComponent(Id);
        _host?.Log("我的插件已卸载");
    }
}

public class MyPluginView : UserControl
{
    public MyPluginView()
    {
        Width = double.NaN;
        Height = double.NaN;
        Background = new SolidColorBrush(Color.FromArgb(40, 0, 212, 255));

        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock
        {
            Text = "Hello from Plugin!",
            FontSize = 16,
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold
        });
        Content = panel;
    }
}
```

### 4.4 编译与部署

```bash
dotnet build -c Release
```

将编译产物 `MyPlugin.dll` 复制到 DesktopClock 的 `Plugins/` 目录：

```
DesktopClock/
├── DesktopClock.exe
└── Plugins/
    └── MyPlugin/
        └── MyPlugin.dll
```

## 5. manifest.json（可选）

在插件目录下放置 `manifest.json` 可提供元数据：

```json
{
  "id": "my_clock_plugin",
  "name": "我的插件",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "示例插件",
  "entry": "MyPlugin.dll"
}
```

> 注意：当前版本 `PluginManager` 通过反射扫描 DLL 中的 `IPlugin` 实现类型，`manifest.json` 作为元数据参考，不影响加载逻辑。

## 6. 配置与启用状态

插件的启用/禁用状态存储在 `settings.json` 的 `Plugins` 字典中：

```json
{
  "Plugins": {
    "hello_world": true,
    "my_clock_plugin": false
  }
}
```

- 新插件首次加载时默认启用
- 用户可在设置窗口 → 全局设置 → 插件列表中切换启用状态
- 禁用的插件不会被 `PluginManager.LoadAll()` 加载

## 7. 最佳实践

### 7.1 资源管理

```csharp
public void Load(PluginHost host)
{
    _host = host;
    // 加载插件目录下的资源
    var iconPath = Path.Combine(host.PluginDirectory, "icon.png");
    // ...
}

public void Unload()
{
    // 释放 Timer、事件订阅等
    _timer?.Stop();
    _host?.UnregisterComponent(Id);
}
```

### 7.2 日志

```csharp
public void Load(PluginHost host)
{
    host.Log($"[{Name}] 初始化开始");
    try
    {
        // 初始化逻辑
        host.Log($"[{Name}] 初始化完成");
    }
    catch (Exception ex)
    {
        host.Log($"[{Name}] 初始化失败: {ex.Message}");
    }
}
```

### 7.3 组件 ID 命名

- 使用 `your_name.feature` 格式避免冲突（如 `snqig.stock_ticker`）
- 不要使用内置 ID（`clock`、`weather` 等）

## 8. 示例插件

仓库内置 [HelloWorldPlugin](../Plugins/HelloWorldPlugin/) 作为参考，展示：
- 实现 `IPlugin` 接口
- 注册自定义 `UserControl` 组件
- 使用 `PluginHost.Log()` 输出日志
- 使用 `DispatcherTimer` 实现动态更新

## 9. API 版本兼容性

| 主程序版本 | 插件接口 | 兼容说明 |
|-----------|---------|---------|
| v2.0.0 | IPlugin / IPluginComponent | 初始版本 |
| v2.1.0 | 同上 | 无接口变更，向后兼容 |

插件需使用与主程序相同的 `net9.0-windows` 目标框架编译。
