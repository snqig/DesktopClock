# PR4 — 插件系统（完整版）

## 目标

构建一个完整的插件系统，支持Desktop Clock热插件化，每个插件可动态贡献UI组件和设置项，无需修改主程序。

---

## 1. 架构设计

```
DesktopClock.exe
├── Core (主程序)
│   ├── PluginManager          ← 插件发现/加载/生命周期管理
│   ├── PluginHost             ← 插件沙箱 + 服务注入
│   ├── IPlugin                ← 插件契约接口
│   └── Plugins/               ← 插件安装目录
│       ├── HelloWorldPlugin/
│       │   ├── HelloWorldPlugin.dll
│       │   └── manifest.json
│       └── ...
├── Components (内置组件)
└── Services (内置服务)
```

### 插件生命周期

```
扫描目录 → 加载程序集 → 查找 IPlugin 实现 → 实例化 → Load() → RegisterComponent() → 运行
                                                                                    ↓
                                                                               Unload() → 释放资源
```

---

## 2. 新增/修改文件

| 文件 | 说明 |
|---|---|
| `Services/PluginManager.cs` | 插件扫描、加载、生命周期管理 |
| `Services/PluginHost.cs` | 插件沙箱，提供服务访问（受限 API） |
| `Contracts/IPlugin.cs` | 插件接口：Id, Name, Version, Description, Load(), Unload() |
| `Models/AppSettings.cs` | 新增 `Plugins` 节记录启用/禁用状态 |
| `MainWindow.xaml.cs` | 初始化时调用 PluginManager.LoadAll() |
| `SettingsWindow.xaml` | 功能区新增"插件管理"子面板 |
| `SettingsWindow.xaml.cs` | 插件列表绑定 + 启用状态保存 |

---

## 3. 接口定义

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
```

### PluginHost API（插件可访问）

```csharp
public class PluginHost
{
    public string PluginDirectory { get; }

    private readonly ComponentRegistry _registry;
    private readonly Action<string> _logAction;

    public PluginHost(string pluginDir, ComponentRegistry registry, Action<string> logAction)
    {
        PluginDirectory = pluginDir;
        _registry = registry;
        _logAction = logAction;
    }

    public void RegisterComponent(string id, FrameworkElement view);
    public void UnregisterComponent(string id);
    public void Log(string message);
}
```

---

## 4. PluginManager 核心逻辑

```
PluginManager
├── PluginsPath          ← 扫描路径（内置：./Plugins/）
├── LoadAll()
│   ├── ScanDirectories()
│   ├── ValidateAssembly()
│   ├── CreateInstance()
│   └── plugin.Load(host)
├── GetPlugin(id)
├── UnloadPlugin(id)
├── UnloadAll()
└── Settings 读写
```

---

## 5. 插件发现规则

1. 扫描 `./Plugins/` 下所有**子目录**
2. 每个子目录必须包含 `manifest.json`:
   ```json
   {
     "id": "hello_world",
     "name": "Hello World Plugin",
     "version": "1.0.0",
     "author": "DesktopClock contributors",
     "description": "示例插件，展示插件系统功能",
     "entry": "HelloWorldPlugin.dll"
   }
   ```
3. 加载入口程序集 → 反射查找实现 `IPlugin` 的类 → 实例化
4. 加载失败则跳过，记录日志，不终止主程序

---

## 6. 兼容性与安全性

- 插件运行在**同一进程**（WPF 要求 UI 同线程），但通过 `PluginHost` 限制 API 访问
- 如果插件抛异常 → 主 `try-catch` 捕获，禁用该插件，状态持久化
- 插件的 `View` 是 `FrameworkElement`，由主程序 `MainContainer` 托管
- 插件可以订阅 `ComponentRegistry` 事件总线（未来 `IEventBus`）
- 旧版本无插件 → 正常运行（`Plugins/` 不存在或为空）

---

## 7. 实施步骤

### ✅ Step A (已完成)
- 定义 `IPlugin` 接口
- 实现 `PluginHost` 服务注入
- 更新 `AppSettings` 新增 `Plugins` 字典

### ✅ Step B (已完成)
- 实现 `PluginManager`
- 插件扫描（`./Plugins/`）
- 程序集加载与反射
- 插件生命周期管理

### ✅ Step C (已完成)
- 在 `MainWindow` 中初始化 `PluginManager`
- 加载所有插件
- 注册外部组件

### ✅ Step D (已完成)
- 在 Settings 功能区新增"插件管理"面板
- 插件列表展示（Id, Name, Version, 开关）
- 开关状态保存到 `AppSettings.Plugins`
- 加载状态回显

### ✅ Step E (已完成)
- 示例插件 `./DesktopClock/Plugins/HelloWorldPlugin/`
  - `manifest.json` - 插件元数据
  - `HelloWorldPlugin.csproj` - 构建配置
  - `Program.cs` - 运行中插件实现

---

## 8. 主要功能

### 🚀 启动时自动发现
```csharp
// MainWindow.xaml.cs
_pluginManager = new PluginManager(
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins"), _registry);
_pluginManager.LoadAll(_settings.Plugins);
```

### 🎨 动态组件注册
```csharp
// HelloWorldPlugin.Program.cs
_host.RegisterComponent(Id, helloWorldControl);
```

### 🔧 设置持久化
```csharp
// SettingsWindow.xaml.cs
foreach (var pi in _plugins)
    Settings.Plugins[pi.Id] = pi.Enabled;
```

### 🔄 安全加载
```csharp
try
{
    var assembly = Assembly.LoadFrom(dllPath);
    // ... 插件加载逻辑 ...
}
catch (Exception ex)
{
    _log.Add($"[PluginManager] Failed to load {dllPath}: {ex.Message}");
    // 不终止主程序
}
```

---

## 9. 示例插件：HelloWorldPlugin

```
DesktopClock/
└── Plugins/
    └── HelloWorldPlugin/
        ├── manifest.json
        ├── HelloWorldPlugin.csproj
        └── Program.cs
```

### Program.cs 核心代码

```csharp
// HelloWorldPlugin/Program.cs
public class HelloWorldControl : UserControl
{
    public HelloWorldControl()
    {
        Width = double.NaN;
        Height = double.NaN;
        
        // ✅ 修复CornerRadius变量冲突
        var containerCornerRadius = new CornerRadius(8);
        CornerRadius = containerCornerRadius;
        
        Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        Padding = new Thickness(12);
        BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
        BorderThickness = new Thickness(1);

        // ✅ TextWrapping.WordWrap正确引用
        var message = new TextBlock
        {
            Text = "This plugin was dynamically loaded",
            TextWrapping = TextWrapping.WordWrap
        };
    }
}
```

---

## 10. Usage

### 1. 启动 DesktopClock
```bash
cd DesktopClock
cdotnet run
```

### 2. 加载HelloWorldPlugin
```bash
# 该插件已内置，直接可用
cd DesktopClock
start DesktopClock.exe
```

### 3. 查看设置
```
1. 点击托盘图标 ►
2. 选择 "设置"
3. 切换到 "功能" 标签页
4. 查看 "插件" 面板
5. 启用/禁用HelloWorldPlugin
```

### 4. 插件拖拽
```
1. 打开"插件"面板后，HelloWorldPlugin组件将显示
2. 点击组件右上角的"≡ "手柄
3. 拖动组件到任意位置
4. 组件支持自由缩放和隐藏
```

---

## 11. TODO (未来扩展)

- 🎯 插件事件总线系统
- 🎯 组件数据绑定支持
- 🎯 插件配置界面
- 🎯 插件市场/商店
- 🎯 插件作者工具
