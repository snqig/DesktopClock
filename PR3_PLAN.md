# PR3 — 自定义布局引擎

## 目标

实现组件自由拖拽布局，支持 Stack（自动排列）和 Free（绝对定位）两种模式，布局持久化到配置文件。

---

## 1. 新增/修改文件

| 文件 | 说明 |
|---|---|
| `Services/LayoutEngine.cs` | 布局引擎：Stack/Free 模式切换、组件定位管理 |
| `Services/LayoutSerializer.cs` | 布局序列化/反序列化（写入 `LayoutConfig`） |
| `Models/LayoutConfig.cs` | 升级，新增 Free 模式坐标存储 |
| `MainWindow.xaml` | 新增 Free 模式 Canvas 层 |
| `MainWindow.xaml.cs` | Free 模式下拖拽事件处理 |
| `SettingsWindow.xaml` | 功能区新增"自由布局"开关 |
| `SettingsWindow.xaml.cs` | 布局模式保存 |

---

## 2. LayoutConfig 升级

```csharp
public class LayoutConfig
{
    public string Mode { get; set; } = "stack";  // stack | free
    public List<string> ActiveComponents { get; set; } = new() { "digital_clock" };
    public List<string> ZOrder { get; set; } = new() { "date", "lunar", "digital_clock", "world_clock" };
    public Dictionary<string, ComponentPosition> Positions { get; set; } = new();
}

public class ComponentPosition
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Locked { get; set; }
}
```

---

## 3. LayoutEngine 核心逻辑

```
LayoutEngine
├── Mode: Stack | Free
├── SwitchMode(newMode)
├── RebuildLayout(container, components, config)
│   ├── Stack → Grid 模式（当前逻辑）
│   └── Free  → Canvas 模式
├── StartDrag(component, point)
├── OnDrag(delta)
├── EndDrag()
└── SaveLayout(config)
```

### Free 模式实现

```csharp
public void BuildFreeLayout(Panel container, List<IClockComponent> components, LayoutConfig config)
{
    var canvas = new Canvas();
    foreach (var comp in components)
    {
        var pos = config.Positions.GetValueOrDefault(comp.Id, new ComponentPosition());
        Canvas.SetLeft(comp.View, pos.X);
        Canvas.SetTop(comp.View, pos.Y);
        comp.View.Width = pos.Width > 0 ? pos.Width : double.NaN;
        comp.View.Height = pos.Height > 0 ? pos.Height : double.NaN;
        canvas.Children.Add(comp.View);

        // Drag support
        comp.View.MouseDown += (s, e) => { if (!pos.Locked) BeginDrag(comp.View, e); };
    }
    container.Children.Add(canvas);
}
```

---

## 4. 交互行为

| 操作 | 行为 |
|---|---|
| 拖拽组件 | 按住拖动手柄（右上角 ≡ 图标）拖动 |
| 点击组件 | 选中高亮边框 |
| Delete 键 | 移除选中组件（实际为隐藏） |
| 双击空白 | 添加组件弹窗 |
| 右键菜单 | 锁定/解锁位置、重置大小、移除 |

---

## 5. 兼容性

- 旧 `LayoutConfig.Mode` 不存在（v1 config）→ 默认 `stack`
- Free 模式下无 `Positions` 记录 → 组件居中排列
- Stack 模式下新增 `freeze: true` 可锁定行序

---

## 6. 实施步骤

1. **Step A** — `LayoutConfig` 升级 + `ComponentPosition` 模型
2. **Step B** — `LayoutEngine` 核心（Stack/Free 切换 + Canvas 构建）
3. **Step C** — 拖拽交互（MouseDown/Move/Up + 手柄 UI）
4. **Step D** — 布局持久化（序列化到 settings.json）
5. **Step E** — Settings 面板开关 + 验证
