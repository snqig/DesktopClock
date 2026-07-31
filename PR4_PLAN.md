# PR4 — 插件系统

## 目标

为 Desktop Clock 引入插件架构，支持第三方 .NET 程序集动态加载，每个插件可贡献 UI 组件和设置项，无需修改主程序。

---

## 1. 架构设计

```
DesktopClock.exe
├── Core (主程序)
│   ├── PluginManager          ← 插件发现/加载/生命周期
│   ├── PluginHost             ← 插件沙箱 + 服务注入
│   ├── IPlugin                ← 插件契约接口
│   └── Plugins/               ← 插件安装目录
│       ├── MyPlugin/
│       │   ├── MyPlugin.dll
│       │   └── manifest.json
│       └── ...
├── Components (内置组件)
└── Services (内置服务)
```

### 插件生命周期

```
扫描目录 → 加载程序集 → 查找 IPlugin 实现 → 实例化 → Load() → RegisterComponent() → 运行
                                                                                    ↓
                                                                               Unload() → 释放
```

---

## 2. 新增/修改文件

| 文件 | 说明 |
|---|---|
| `Services/PluginManager.cs` | 插件扫描、加载、生命周期管理 |
| `Services/PluginHost.cs` | 插件沙箱，提供服务访问（受限 API） |
| `Contracts/IPlugin.cs` | 插件接口：Id, Name, Version, Description, View (可选), Load(), Unload() |
| `Contracts/IPluginComponent.cs` | 插件组件接口（继承 IPlugin + IClockComponent） |
| `Models/AppSettings.cs` | 新增 `Plugins` 节记录启用/禁用状态 |
| `MainWindow.xaml.cs` | 初始化时调用 PluginManager.LoadAll() |
| `SettingsWindow.xaml` | 功能页新增"插件管理"子面板 |
| `PR4_PLAN.md` | 本文件 |

---

## 3. 接口定义

```csharp
public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Description { get; }
    string Author { get; }
    
    void Load(PluginHost host);
    void Unload();
}

public interface IPluginComponent : IPlugin
{
    string ComponentId { get; }
    FrameworkElement? View { get; }
    object? SettingsControl { get; }
}
```

### PluginHost API（插件可访问）

```csharp
public class PluginHost
{
    public string PluginDirectory { get; }
    public T? GetService<T>() where T : class;
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
├── ReloadPlugin(id)
├── EnablePlugin(id)
├── DisablePlugin(id)
└── Settings 读写
```

---

## 5. 插件发现规则

1. 扫描 `./Plugins/` 下所有**子目录**
2. 每个子目录必须包含 `manifest.json`:
   ```json
   {
     "id": "weather-plugin",
     "name": "天气插件",
     "version": "1.0.0",
     "entry": "WeatherPlugin.dll",
     "mainClass": "WeatherPlugin.PluginEntry"
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

1. **Step A** — 定义 `IPlugin`、`IPluginComponent`、`PluginHost` 接口
2. **Step B** — 实现 `PluginManager`（扫描、加载、生命周期）
3. **Step C** — 集成到 MainWindow（初始化 + 组件注册）
4. **Step D** — Settings 插件管理面板（启用/禁用、信息展示）
5. **Step E** — 示例插件（HelloWorld + 天气插件）
