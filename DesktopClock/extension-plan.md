# DesktopClock 功能扩展规划

> 基于现有组件架构的可扩展功能 todo list，不做代码实现，仅做规划。
>
> 更新时间：2026-07-31（对齐 v1.0.5 现有能力）

---

## 现有能力盘点（扩展基础）

| 能力 | 位置 | 可复用点 |
|------|------|----------|
| `IClockComponent` 接口 | [Components/IClockComponent.cs](Components/IClockComponent.cs) | 新组件统一接入布局引擎 |
| `WidgetManager` 挂件系统 | [Services/WidgetManager.cs](Services/WidgetManager.cs) | 独立悬浮窗口（位置/透明度/置顶持久化） |
| `CountdownWindow` 独立挂件 | [Views/Widgets/CountdownWindow](MainWindow.xaml.cs) | 脱离主窗口的悬浮挂件容器 |
| `CountdownTask` 多任务模型 | [Models/CountdownTask.cs](Models/CountdownTask.cs) | 多事件倒计时数据已就绪 |
| 跑马灯滚动 | [SysMonComponent.xaml.cs](Components/SysMonComponent.xaml.cs)、[ScrollingTodoComponent.xaml.cs](Components/ScrollingTodoComponent.xaml.cs) | 文字右到左无缝滚动 + 宽度对齐 |
| 提醒系统 | `RemindersJson` + `ReminderDialog.xaml` | 定时触发 + 弹窗 + 去重 |
| 全局热键 | `RegisterHotKey` | `Ctrl+H` / `Ctrl+Shift+D` / `Ctrl+Shift+S` |
| 托盘通知 | `System.Windows.Forms.NotifyIcon` | `ShowBalloonTip` 桌面通知 |
| 配置持久化 | [Models/AppSettings.cs](Models/AppSettings.cs) | `System.Text.Json` + 版本迁移 |

---

## 1. 间隔提醒组件（HealthReminderComponent）

**核心需求**：每小时喝水、每 45 分钟站立、每 20 分钟眼保健操……可自定义间隔的周期性健康提醒。

### 功能清单

- [ ] **多提醒条目管理**
  支持同时配置多条提醒（喝水 / 站立 / 眼保健操 / 伸展），每条独立设置间隔。
  ```json
  // 配置示例
  {
    "reminders": [
      { "id": "water", "label": "喝水", "intervalMinutes": 60, "enabled": true },
      { "id": "stand", "label": "站立", "intervalMinutes": 45, "enabled": true },
      { "id": "eyes", "label": "眼保健操", "intervalMinutes": 20, "enabled": false }
    ]
  }
  ```

- [ ] **倒计时显示**
  主窗口内嵌组件，显示「下次喝水：00:48:32」格式的倒计时，距最近一条提醒还有多久。

- [ ] **桌面通知触发**
  时间到时通过现有 `_trayIcon.ShowBalloonTip` 弹出通知，可播放自定义提示音。

- [ ] **今日完成计数**
  「喝水 3/8 次」，当日 0:00 重置。

- [ ] **工作时间段限制**
  可选仅在 9:00-18:00 之间触发提醒，晚上不打扰。

- [ ] **暂停/跳过**
  右键菜单可「暂停 1 小时」或「跳过本次」。

### 技术对接点

| 现有能力 | 复用方式 |
|----------|----------|
| `IClockComponent` 接口 | 新建 `HealthReminderComponent` 实现，接入主窗口 Stack 布局 |
| `ReminderDialog.xaml` | 参考弹窗 UI，复用通知逻辑 |
| `AppSettings` | 新增 `HealthRemindersJson` 字段 |
| `_trayIcon.ShowBalloonTip` | 直接调用，无需新建通知通道 |
| `SettingsWindow`「功能」标签 | 新增「健康提醒」子面板 |
| `ComponentRegistry` | `Register(new HealthReminderComponent())` |

---

## 2. 番茄钟组件（PomodoroComponent）

**核心需求**：25 分钟专注 + 5 分钟休息循环，帮助专注工作。

### 功能清单

- [ ] **双阶段循环**
  专注阶段（默认 25 分钟）→ 休息阶段（默认 5 分钟），自动切换。

- [ ] **长休息**
  每完成 4 个番茄（专注 → 休息），触发长休息（默认 15 分钟）。

- [ ] **双形态展示**
  - 主窗口内嵌：替换或叠加在主时钟区域，显示 `🍅 专注 18:42`
  - 独立挂件：通过 `WidgetManager` 注册为可拖动悬浮窗，参考 `CountdownWindow`

- [ ] **控制按钮**
  右键菜单或设置面板提供：开始 / 暂停 / 重置 / 跳过当前阶段。

- [ ] **完成音效 + 通知**
  一个番茄完成时播放提示音 + `_trayIcon.ShowBalloonTip` 通知「休息一下吧」。

- [ ] **今日统计**
  「今日已完成 3 个番茄」，当日重置。

- [ ] **可配置时长**
  专注时长（15/25/30/45 分钟可选）、短休息、长休息、长休息间隔均可调。

### 技术对接点

| 现有能力 | 复用方式 |
|----------|----------|
| `IClockComponent` 接口 | 新建 `PomodoroComponent` 实现 |
| `CountdownComponent` 倒计时逻辑 | 参考倒计时显示格式（HH:MM:SS）与归零行为 |
| `WidgetManager` + `CountdownWindow` | 独立挂件形态复用挂件窗口的拖动/置顶/透明度持久化 |
| 全局热键系统 | 新增 `Ctrl+Shift+P` 启动/暂停番茄钟 |
| `AppSettings` | 新增 `PomodoroConfig` 节 |
| `RebuildLayout` 的 active 列表 | 新增 `pomodoro` 组件 ID |

---

## 3. 每日一言组件（DailyQuoteComponent）

**核心需求**：每天随机展示一条名言/诗词/鸡汤，增加人文气息。

### 功能清单

- [ ] **本地语录库**
  内置 50+ 条中外名言，分类（励志 / 古诗词 / 哲理 / 幽默）。

- [ ] **每天自动轮换**
  每日 0:00 自动切换到下一条，支持手动切换。

- [ ] **滚动字幕显示**
  复用现有跑马灯能力，文字从右到左无缝滚动。

- [ ] **可选 API 源**
  支持配置一言 API（如 https://hitokoto.cn/），在线获取并本地缓存。

### 技术对接点

| 现有能力 | 复用方式 |
|----------|----------|
| `ScrollingTodoComponent` 跑马灯 | 直接复刻滚动逻辑（`Canvas` + `ActualWidth` 测量） |
| `IClockComponent` | 新建 `DailyQuoteComponent` |
| `EmbeddedResource` | 语录数据文件嵌入程序集 |
| `SyncScrollComponentWidths` | 宽度自动与时钟右边缘对齐 |

---

## 4. 习惯打卡组件（HabitTrackerComponent）

**核心需求**：每日习惯追踪，可视化打卡。

### 功能清单

- [ ] **习惯列表**
  自定义习惯名 + 图标（运动 / 阅读 / 冥想 / 早起……）。

- [ ] **当天打卡**
  点击完成 → 显示 ✅ → 计入今日统计。

- [ ] **7 天热力图**
  最近 7 天完成情况用色块展示（绿=完成，灰=未完成），一目了然。

- [ ] **今日进度条**
  「今日习惯 3/5 ████████░░░░ 60%」

### 技术对接点

| 现有能力 | 复用方式 |
|----------|----------|
| `IClockComponent` 接口 | 新建 `HabitTrackerComponent` |
| `AppSettings` | 新增 `HabitsJson` 字段，按日期存储打卡记录 |
| 独立挂件形态 | 习惯打卡交互较多，建议作为独立挂件（参考 `CountdownWindow`）而非主窗口内嵌 |

---

## 5. 多事件倒计时 UI 完善（基于现有 CountdownTask）

**核心需求**：完善已存在的多任务倒计时数据模型，补齐 UI 与交互。

> ⚠️ 注意：`CountdownTask` 模型与 `CountdownTasks` 列表已存在于 [Models/CountdownTask.cs](Models/CountdownTask.cs)，本项为 **UI 完善**，不是新建组件。

### 已具备

- ✅ `CountdownTask` 实体（Id / Title / TargetTimeUtc / Enabled / DisplayMode / EndAction）
- ✅ `AppSettings.CountdownTasks` 列表
- ✅ `CountdownTaskRotationSeconds` 轮播间隔配置
- ✅ 独立 `CountdownWindow` 挂件（位置/透明度/置顶持久化）

### 待实现

- [ ] **任务管理 UI**
  设置面板新增「倒计时任务」列表：增删改查、排序、启用/禁用。

- [ ] **轮播展示**
  `CountdownComponent` 按 `CountdownTaskRotationSeconds` 自动轮播启用的任务，显示标题 + 倒计时。

- [ ] **横向排列选项**
  多事件时可选「轮播」或「横向并列」两种布局。

- [ ] **到期提醒**
  事件到期当天高亮 + `_trayIcon.ShowBalloonTip` 通知。

- [ ] **农历支持**
  支持农历日期倒计时（复用 [LunarCalendar.cs](LunarCalendar.cs) 计算能力）。

### 技术对接点

| 现有能力 | 复用方式 |
|----------|----------|
| `CountdownTask` 模型 | **已就绪**，无需新建 |
| `CountdownComponent` | 扩展 `Update` 支持任务列表轮播 |
| `CountdownWindow` | 独立挂件复用 |
| `LunarCalendar` | 农历日期倒计时计算 |
| `SettingsWindow` | 新增任务管理列表 UI |

---

## 实施优先级建议

| 优先级 | 功能 | 理由 |
|--------|------|------|
| 🔴 P0 | 多事件倒计时 UI 完善 | 数据模型已就绪，仅需补 UI 与轮播逻辑，投入产出比最高 |
| 🟠 P1 | 间隔提醒 | 需求最明确，复用现有提醒系统与托盘通知 |
| 🟡 P2 | 番茄钟 | 独立组件，可复用 `WidgetManager` 独立挂件形态 |
| 🟢 P3 | 每日一言 | 锦上添花，跑马灯能力可直接复用 |
| 🔵 P4 | 习惯打卡 | UI 交互较多，建议作为独立挂件后置 |

---

## 扩展成本评估

每新增一个组件，需要的改动点：

| 改动文件 | 工作量 | 说明 |
|----------|--------|------|
| `Components/XxxComponent.cs` | 中 | 核心逻辑：定时器、配置读取、UI 构建 |
| `Models/AppSettings.cs` | 小 | 新增 5-10 个配置属性 |
| `SettingsWindow.xaml` | 中 | 新增子面板（Tab + Grid + Controls） |
| `SettingsWindow.xaml.cs` | 小 | 配置读写绑定 |
| `MainWindow.xaml.cs` | 小 | `RegisterComponents` + `RebuildLayout` active 列表 |
| `Resources/Strings.zh.xaml` | 小 | 3-5 条字符串 |

**单个组件预估 150-400 行 C# + 50-100 行 XAML**，不影响现有功能。

### 独立挂件形态额外成本

若组件以独立悬浮窗口形式提供（如番茄钟、习惯打卡），还需：

| 改动文件 | 工作量 | 说明 |
|----------|--------|------|
| `Views/Widgets/XxxWindow.xaml(.cs)` | 中 | 独立窗口 UI，参考 `CountdownWindow` |
| `MainWindow.xaml.cs` 挂件注册 | 小 | 在 `InitializeWidgetRuntime` 注册到 `WidgetManager` |
| `AppSettings` 窗口位置字段 | 小 | `XxxWindowLeft/Top/Width/Height/Opacity` |
