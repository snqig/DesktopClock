# DesktopClock 项目重构完整 TODO 清单

> 已合并【内置倒计时挂件】需求，适配 Trae 直接执行
>
> **项目**：WPF DesktopClock 透明悬浮桌面时钟（内置时钟组件 + 倒计时组件双挂件）
> **框架**：.NET 8 / WPF
> **规范**：✅完成 | ⏳进行中 | ⏰待开发 | ⚠️风险点 | 🧪测试项
> **运行形态**：倒计时作为程序内置 Widget 组件，不独立 EXE；时钟、倒计时各自独立悬浮窗口，共用底层窗口引擎
>
> **兼容性约束**：必须兼容现有 `settings.json` / `pos.txt` / `pointer_sets.json` 配置，支持自动迁移

---

## 一、前期准备 & 项目基础改造（最高优先级 P0）

- ⏰【项目清单】更新项目文件，开启 PerMonitorV2 DPI 感知，嵌入 app.manifest
- ⏰【目录重构】按照规范重建解决方案目录

```plaintext
DesktopClock/
├── Core                // Win32 API、底层窗口封装
├── Config              // 配置模型、持久化、版本迁移
├── Render              // 渲染调度、帧率控制器
├── Services            // 业务服务层
│   ├── HotkeyService
│   ├── TrayService
│   ├── WindowManager
│   ├── LogService
│   └── WidgetManager       ✅【新增：挂件生命周期管理器】
├── Layouts             // 时钟布局模板定义
├── Plugins             // 预留插件架构（三期迭代）
├── Views
│   ├── MainFloatWindow（废弃单一主窗口，改为多独立悬浮窗口）
│   ├── SettingWindow
│   └── Widgets             ✅【新增组件目录】
│       ├── ClockWidget.xaml
│       └── CountdownWidget.xaml
├── Themes              // ResourceDictionary 样式/主题
└── Utils               // 通用帮助类
```

- ⏰【清理债务】移除老旧硬编码配置、零散静态变量；统一使用依赖注入
- ⏰【DI 容器引入】引入 Microsoft.Extensions.DependencyInjection 统一管理服务生命周期
- ⚠️【架构约束】禁止 View 直接调用 Win32 API，所有窗口操作交由 WindowManager
- ⏰【配置模型扩展】主配置 AppConfig 新增时钟、倒计时两套独立挂件配置实体
  - ClockWidgetConfig
  - CountdownWidgetConfig ✅

---

## 二、底层窗口核心模块（P0，性能基石）

- ⏰ 封装 Win32 NativeMethods 静态类：分层窗口、扩展样式、窗口位置、穿透控制
- ⏰ 实现分层窗口 LayeredWindow 管理器，替代原生 AllowsTransparency 透明方案
- ⏰ 实现【鼠标穿透】动态切换：WS_EX_TRANSPARENT 实时开关，无需重启
- ⏰ 窗口拖拽重构：支持拖拽移动、屏幕边缘吸附
- ⏰ 多显示器适配：
  - 通过显示器唯一 ID 持久化窗口坐标
  - 显示器拔插自动回落到主屏幕
- ⏰ 置顶策略双模式实现：普通置顶 / 强制顶层；增加全屏窗口检测
- 🧪 测试：高分屏 125%/150%/200% 缩放、多屏拼接、显示器热插拔场景
- ⚠️ 禁止频繁调用 SetWindowPos，增加调用防抖
- ⏰【适配双挂件】WindowManager 支持同时管理多个独立 Layered 悬浮窗口

---

## 三、挂件管理器 WidgetManager（P0，新增核心模块）

- ⏰ 统一管理 Clock、Countdown 两个挂件实例生命周期
- ⏰ 程序启动：读取配置，自动启用 / 关闭对应挂件
- ⏰ 创建 / 销毁悬浮窗口、加载对应组件视图
- ⏰ 对外统一接口：显示 / 隐藏指定挂件
- ⏰ 程序退出时统一释放所有窗口、渲染资源
- ⏰ 隔离两个挂件窗口状态，互不干扰
- 🧪 测试场景：仅开时钟、仅开倒计时、两者同时启用、交替开关

---

## 四、渲染调度 & 性能优化（P0，常驻后台核心）

- ⏰ 废弃单一 DispatcherTimer，新建 FrameRenderScheduler 统一调度刷新
- ⏰ 分层帧率策略：
  - 默认时钟 / 倒计时：1FPS（仅时间变更刷新）
  - 拖拽 / 设置编辑模式：30FPS
  - 闲置休眠模式：暂停动画、降低渲染负载
- ⏰ 资源泄漏防护：统一管控 Brush、Image 资源，提供资源回收接口
- ⏰ 系统事件监听：锁屏 / 解锁、系统休眠 / 唤醒，自动暂停 / 恢复渲染
- ⏰【倒计时逻辑】实现时间差值计算、两种显示模式格式化
  - D 天 H:M:S
  - 仅 H:M:S
- ⏰ 倒计时到达目标时间，触发结束动作调度
- 🧪 长时间压力测试：连续运行 72h，监控内存增长、CPU 占用
- 🎯 性能目标：闲置 CPU＜0.3%，稳定内存＜35MB（双挂件同时开启）

---

## 五、配置系统重构（P0）

- ⏰ 定义完整配置实体模型
  - WindowSetting：窗口通用属性（位置、透明度、置顶、穿透、尺寸）
  - ClockWidgetConfig：原有时钟全套样式与配置
  - CountdownWidgetConfig ✅【新增】
    - Window：独立窗口配置
    - Style：字体、字号、文字颜色、描边、阴影
    - Timer：目标时间、标题、显示模式、倒计时结束动作
    - IsEnabled：挂件启用开关
    - DateTimeFormat：时钟时间格式
  - HotkeySetting：全局快捷键集合
  - PluginSetting：启用插件列表
- ⏰ 基于 System.Text.Json 实现异步读写
- ⏰ 配置防抖保存（延迟 500ms 写入磁盘，避免频繁 IO）
- ⏰ 配置文件自动备份机制，程序崩溃时恢复上一版有效配置
- ⏰ 配置版本号设计，实现旧配置自动迁移，兼容升级
- ⏰ 样式导入导出：时钟主题 `.clocktheme`、倒计时独立样式 `.countdowntheme` ✅
- ⚠️ IO 操作全部放到后台线程，严禁 UI 线程阻塞

---

## 六、UI 组件开发（P1）

- ⏰ ClockWidget.xaml 原有时钟组件重构，适配挂件容器
- ⏰ 新建 CountdownWidget.xaml ✅
  - 可选标题文本、大号倒计时数字展示
  - 绑定样式：字体、字号、颜色、描边、阴影
  - 倒计时结束文字闪烁动画
- ⏰ 样式全部移入 Themes 资源字典，分离样式与业务代码
- ⏰ 设置窗口重构，增加 Tab 分页：【时钟设置】｜【倒计时设置】✅
- ⏰ 倒计时设置面板功能：
  - 启用 / 关闭倒计时挂件
  - 目标时间选择器（年月日时分秒）
  - 标题文本输入框 + 显示标题开关
  - 切换显示模式：D 天 H:M:S / 仅时分秒
  - 字体下拉框：枚举系统全部已安装字体 `Fonts.SystemFontFamilies`，选中实时预览
  - 字号滑块、文字拾色器
  - 描边开关、描边粗细、描边颜色
  - 阴影开关、阴影大小、阴影颜色
  - 挂件透明度调节
  - 倒计时结束动作选择：无动作 / 文字闪烁 / 弹窗通知 / 提示音
- ⏰ 内置布局预设；设置面板所有参数实时预览，无需保存生效
- ⏰ 增加游戏模式开关：检测全屏应用自动隐藏 / 取消置顶

---

## 七、托盘、全局快捷键、开机自启（P1）

- ⏰ TrayService 封装托盘图标、右键菜单动态生成
  - 显示 / 隐藏时钟
  - ✅ 显示 / 隐藏倒计时
  - ✅ 显示 / 隐藏全部挂件
  - 切换穿透、切换置顶
  - 打开设置、重启程序、退出
  - 开机自启快捷开关
- ⏰ HotkeyService：封装 Win32 全局热键，支持自定义快捷键
  - 原有热键：显示隐藏全部、切换全局穿透 / 置顶
  - ✅ 新增热键：单独显示 / 隐藏倒计时、切换倒计时穿透、切换倒计时置顶
- ⏰ 开机自启双方案兼容（自动降级）
  - 当前用户注册表（无管理员权限优先）
  - 任务计划程序（适配 Win11 高权限场景）
- ⏰ 热键设置页面新增倒计时相关快捷键配置项

---

## 八、日志 & 全局异常处理（P1）

- ⏰ LogService 分级日志：Info/Warn/Error
- ⏰ 日志滚动策略，限制单文件大小，自动清理旧日志
- ⏰ 全局异常捕获
  - DispatcherUnhandledException（UI 异常）
  - AppDomain.UnhandledException（后台线程异常）
- ✅ 轻度异常静默记录日志；严重异常弹窗提供重启选项，避免直接闪退
- ⏰ 日志目录开放入口，设置面板一键打开日志文件夹
- ⏰ 增加倒计时相关事件日志（倒计时抵达、样式修改、挂件启停）

---

## 九、插件系统【二期 P2，基础稳定后开发】

- ⏰ 基于自定义 AssemblyLoadContext 实现插件隔离
- ⏰ 定义标准插件接口 IWidgetPlugin

```csharp
public interface IWidgetPlugin
{
    string Name { get; }
    string Description { get; }
    UIElement GetPluginContent();
    void Load();
    void Unload();
}
```

- ⏰ 插件管理器：加载、卸载、热插拔、启用 / 禁用
- ⏰ 插件目录规范：`Plugins/{插件名}/plugin.json` 元文件
- ⏰ 设置面板插件管理列表
- ⚠️ 隔离插件异常，单个插件崩溃不能导致主程序退出
- ⏰ 官方示例插件预留：农历、系统监控（CPU / 内存）、倒计时（未来可迁移为插件）

---

## 十、可选增值功能（P3，按需迭代）

- ✅ 夜间定时自动降低挂件透明度
- ⏰ 文字描边优化，适配各类桌面壁纸
- ⏰ 背景渐变 / 图片背景支持
- ✅ 窗口吸附距离自定义参数
- ✅ 倒计时支持多个任务（多组倒计时挂件）

---

## 十一、测试与发布清单（Release 前必做）

- 🧪 Win10 / Win11 双系统兼容性测试
- 🧪 不同 DPI 缩放、多显示器场景测试
- 🧪 时钟 + 倒计时双悬浮窗口并行运行冲突测试
- 🧪 长时间后台驻留稳定性测试（内存泄漏检测）
- 🧪 权限测试：普通用户、管理员权限运行
- 🧪 异常场景：强制结束进程、系统注销、休眠唤醒
- 🧪 配置升级兼容测试（旧版本配置导入新版本）
- 🧪 倒计时专项测试：跨天倒计时、倒计时结束各类触发动作
- ⏰ 打包方案：Publish 单文件发布，启用 Trim，减小程序体积

---

## 十二、技术债务与长期约束（持续遵守）

- ⚠️ 禁止 UI 线程执行文件 IO、序列化、耗时计算
- ⚠️ 禁止持续新建 Brush、Pen 等 WPF 资源，做好复用与释放
- ⚠️ 不滥用 `AllowsTransparency=True`，优先分层窗口方案
- ⚠️ 所有窗口位置相关操作统一由 WindowManager 管控
- ⚠️ 定时器统一由 FrameRenderScheduler 管理，禁止随处新建 DispatcherTimer
- ⚠️ 时钟、倒计时窗口状态完全隔离，共享底层窗口 API，业务配置相互独立

---

## 执行约束（与现有代码的兼容性承诺）

1. **配置兼容**：现有 `settings.json` 必须能自动迁移到新 AppConfig，不丢失用户配置
2. **位置兼容**：现有 `pos.txt` / `pos_instance_{N}.txt` 必须能被新 WindowManager 识别
3. **指针方案兼容**：现有 `pointer_sets.json` 必须能被新 ClockWidgetConfig 读取
4. **多实例兼容**：`--instance=N` 启动参数必须继续生效
5. **渐进式迁移**：每个模块完成后必须能独立编译运行，不允许一次性大爆炸式重构
