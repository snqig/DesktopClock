# 更新日志

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 规范，
版本号采用 [语义化版本](https://semver.org/lang/zh-CN/)。

## [1.0.5] - 2026-07-31

### 新增

- **倒计时组件**：支持 `displayMode=time`（HH:MM:SS）与 `displayMode=days`（D天HH:MM:SS）两种显示模式，归零统一显示 `00:00:00`，移除"时间到!"中文提示
- **系统监控跑马灯**：CPU/内存/网速/电池信息以跑马灯方式从右到左滚动显示，支持字体、颜色、字号自定义，宽度与时钟右边缘对齐
- **滚动待办跑马灯**：容器宽度与全局容器一致，文字从右到左无缝滚动，与时钟最后一个数字右对齐
- **设置 UI 改进**：外观与日期字体下拉框支持选择系统字体；倒计时目标时间改用日期控件选择
- **LICENSE 文件**：补充 MIT 许可证全文
- **CHANGELOG.md**：新增更新日志

### 修复

- **时区偏移**：倒计时目标时间存储为 UTC，注入组件时转为本地时间，避免 `DateTime.TryParse` 导致的时区偏移
- **多组件显示不全**：窗口高度计算纳入倒计时、待办滚动、系统监控、天气、媒体信息等附加组件高度，避免最后一行被截断
- **Release 发布方式**：`release.yml` 改为 `--self-contained true`，与 README"无需安装 .NET 运行时"描述一致
- **版本号不一致**：`csproj` 中 `Version` 统一为 1.0.5
- **CI 测试不阻塞**：移除 `continue-on-error: true`，测试失败将真正阻断构建

### 变更

- **README.md 重写**：新增 GitHub 徽章、下载与安装章节、Releases 引导，发布命令改为 `--self-contained true`

## [1.0.4] - 2026-07-30

### 新增

- 多实例支持（`--instance=N`）
- Windows 11 Mica/Acrylic/Tabbed 背景效果
- 指针样式编辑器
- 相册背景通用包装器（BackgroundWrapper）

## [1.0.3] - 2026-07-30

### 新增

- 赛博朋克霓虹表盘
- 双时区表盘
- 缎带表盘

## [1.0.2] - 2026-07-29

### 新增

- 插件系统（IPlugin / IPluginComponent）
- 皮肤系统（IClockSkin / SkinHost）
- HelloWorldPlugin 示例插件

## [1.0.1] - 2026-07-29

### 新增

- 天气组件（Open-Meteo API）
- 媒体播放信息组件
- AOD 省电模式
- 全局热键

## [1.0.0] - 2026-07-29

### 新增

- 初始版本
- 11 种显示模式（数字/翻转/文字/二进制/极简/模拟/超精美模拟/机械/指针表盘/缎带/双时区）
- Stack / Free 双布局引擎
- 农历、节气、生肖、世界时钟、系统监控、倒计时、滚动待办
- 托盘图标、提醒系统、多语言（zh/en/ja）
