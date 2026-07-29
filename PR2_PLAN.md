# PR2 — 架构拆解与组件化重构计划

## 目标

将单体 MainWindow 拆分为 **组件化架构**，为后续 PR3（自定义布局引擎）、PR4（插件系统）奠定基础。

---

## 1. 新增目录结构

```
DesktopClock/
├── Components/             ← 所有 UI 组件
│   ├── IClockComponent.cs  ← 组件接口
│   ├── ClockPanel.xaml/.cs ← 数字时钟面板（现有 DigitalPanel 内容）
│   ├── AnalogClockPanel.xaml/.cs
│   ├── FlipClockPanel.xaml/.cs
│   ├── WordClockPanel.xaml/.cs
│   ├── BinaryClockPanel.xaml/.cs
│   ├── MinimalClockPanel.xaml/.cs
│   ├── LunarPanel.xaml/.cs
│   ├── WorldClockPanel.xaml/.cs
│   └── ReminderPanel.xaml/.cs
├── Services/
│   ├── SettingsService.cs   ← 配置读写 + 版本迁移
│   ├── ThemeService.cs      ← 主题/样式管理
│   └── ReminderService.cs   ← 提醒调度（从 MainWindow 剥离）
├── Models/
│   ├── ComponentConfig.cs   ← 组件级配置模型
│   └── AppSettings.cs       ← 升级为带版本号的配置
└── MainWindow.xaml/.cs      ← 精简为容器 + 组件注册
```

---

## 2. 组件接口设计

```csharp
public interface IClockComponent
{
    string Id { get; }
    string DisplayName { get; }
    FrameworkElement View { get; }           // 组件 UI
    ComponentConfig Config { get; set; }     // 组件配置
    void Update(DateTime now);               // 每秒刷新
    void ApplyConfig();                      // 配置变更后重绘
}
```

基类：

```csharp
public abstract class ClockComponentBase : IClockComponent
{
    public string Id { get; protected set; }
    public string DisplayName { get; protected set; }
    public FrameworkElement View { get; protected set; }
    public ComponentConfig Config { get; set; } = new();

    public abstract void Update(DateTime now);
    public virtual void ApplyConfig() { }
}
```

---

## 3. 配置升级 (`settings.json` v2)

```json
{
  "version": 2,
  "global": {
    "language": "zh",
    "themePreset": "default",
    "opacity": 0.85,
    "clickThrough": false,
    "snapToEdge": false,
    "lockPosition": false,
    "autoStart": false,
    "hotkeyHide": "Ctrl+H",
    "windowWidth": 500,
    "windowHeight": 120
  },
  "layout": {
    "activeComponents": ["digital_clock"],
    "zOrder": ["date", "lunar", "digital_clock", "world_clock"]
  },
  "components": {
    "digital_clock": {
      "enabled": true,
      "position": "center",
      "fontSize": 64,
      "fontFamily": "DS-Digital",
      "fontColor": "#00d4ff",
      "use24Hour": true,
      "showSeconds": true
    },
    "analog_clock": {
      "enabled": false,
      "position": "center",
      "faceSize": 260,
      "faceColor": "#252535",
      "hourHandColor": "#4a5a6a",
      "minuteHandColor": "#7a8a9a",
      "secondHandColor": "#e65e5e"
    },
    "lunar": {
      "enabled": false,
      "position": "top",
      "fontSize": 14,
      "fontColor": "#aaaaaa",
      "showSolarTerm": true,
      "showZodiac": true
    },
    "date": {
      "enabled": true,
      "position": "top",
      "fontSize": 16,
      "fontFamily": "Consolas",
      "fontColor": "#00FF00"
    },
    "world_clock": {
      "enabled": false,
      "position": "bottom",
      "timeZone": "China Standard Time"
    },
    "reminder": {
      "enabled": false,
      "items": []
    }
  }
}
```

### 版本迁移逻辑

```csharp
// SettingsService.Load()
// 1. 读取 settings.json
// 2. 检查 version
// 3. version == 1 → 映射旧属性到新结构（如 global.fontSize + components.digital_clock.fontSize）
// 4. version == 2 → 直接反序列化
// 5. 无文件 → 返回默认配置
```

---

## 4. 组件注册与生命周期

```csharp
// MainWindow 初始化时
public partial class MainWindow : Window
{
    private readonly List<IClockComponent> _components = new();
    private readonly Dictionary<string, IClockComponent> _componentMap = new();

    private void RegisterComponents()
    {
        Register(new DateComponent(settings));
        Register(new DigitalClockComponent(settings));
        Register(new AnalogClockComponent(settings));
        Register(new LunarComponent(settings));
        Register(new WorldClockComponent(settings));
    }

    private void Register(IClockComponent component)
    {
        _components.Add(component);
        _componentMap[component.Id] = component;
    }

    // Timer_Tick
    private void Timer_Tick(...)
    {
        var now = DateTime.Now;
        foreach (var c in _components)
            if (_config.IsActive(c.Id))
                c.Update(now);
    }
}
```

`MainContainer` Grid 改为动态插拔子元素：

```csharp
private void RebuildLayout()
{
    MainContainer.Children.Clear();
    foreach (var id in _config.Layout.ZOrder)
    {
        if (_componentMap.TryGetValue(id, out var comp) && _config.IsActive(id))
        {
            MainContainer.Children.Add(comp.View);
        }
    }
}
```

---

## 5. 现有面板迁移清单

| 现有 x:Name | 目标 UserControl | 迁移内容 |
|---|---|---|
| DigitalPanel | DigitalClockPanel | TimeToChinese / NumberToChinese 迁入 |
| FlipPanel | FlipClockPanel | AnimateFlip 迁入 |
| WordPanel | WordClockPanel | 中文时间格式化逻辑迁入 |
| BinaryPanel | BinaryClockPanel | BuildBinaryPanel 迁入 |
| MinimalPanel | MinimalClockPanel | 无额外逻辑 |
| ProgressPanel | AnalogClockPanel | BuildAnalogClock + 指针更新迁入 |
| DateText | DateComponent | 新的 UserControl |
| LunarText | LunarComponent | 新的 UserControl |
| WorldClockText | WorldClockComponent | 新的 UserControl |
| Reminder | ReminderService | 调度逻辑从 MainWindow 剥离 |

---

## 6. 配置兼容性

- 旧 `settings.json`（无 version 字段）→ 视为 v1，走迁移映射
- 新增字段缺失 → 取各自默认值（`enabled: false` 保证不破坏现有体验）
- `SettingsService.Save()` 始终输出 v2 格式

---

## 7. 渐进式替换策略

为降低单次 PR 风险，按依赖顺序分 3 步提交：

1. **Step A — 基础设施**：创建目录 + 接口 + 基类 + SettingsService（v2 配置读写 + v1→v2 迁移），不影响现有面板
2. **Step B — 组件迁移**：逐一将现有面板抽成 UserControl，注册到 ComponentRegistry，MainWindow 改为动态布局
3. **Step C — 清理**：删除 MainWindow.xaml 中废弃的面板 XAML，验证所有显示模式功能一致

---

## 8. 验收标准

- [ ] 所有 6 种显示模式（digital/flip/word/binary/minimal/analog）行为与 PR1 完全一致
- [ ] 农历、世界时钟、报时、提醒功能正常工作
- [ ] 旧 settings.json 自动迁移到 v2，不丢失配置
- [ ] 配置中可单独禁用/启用每个组件
- [ ] `dotnet build` 0 错误 0 警告
