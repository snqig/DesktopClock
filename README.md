# DesktopClock 🕐

一款基于 .NET 9 WPF 的桌面时钟应用，支持多种显示模式和丰富的自定义设置。

## 功能特色

### 显示模式
| 模式 | 说明 |
|------|------|
| 数字 | 标准 HH:mm:ss 数字时钟 |
| 翻转 (Flip) | 翻牌动画，每位数字独立翻转 |
| 文字 (Word) | 中文时间显示，如"三点十五分三十秒" |
| 二进制 (Binary) | LED 点阵二进制显示 |
| 进度 (Progress) | 圆形进度弧表示时/分/秒 |
| 极简 (Minimal) | 纯时间数字，无日期装饰 |

### 显示增强
- 12h / 24h 格式切换
- 秒显示开关
- 背景纯色 / 渐变（双色 + 角度）
- 边框颜色和粗细自定义
- 世界时钟（多时区）
- 整点报时

### 功能增强
- 系统托盘图标（右键菜单：显示/设置/退出）
- 全局热键 `Ctrl+H` 隐藏/显示
- 鼠标穿透模式
- 窗口贴边自动吸附
- 锁定位置（禁止拖拽）

### 体验优化
- 主题预设：默认 / 暗黑 / 明亮 / 绿色 / 蓝色
- 中/英文界面切换
- 开机自启开关
- 窗口位置自动记忆

## 技术栈

- .NET 9.0 + WPF
- Windows Forms (NotifyIcon, ColorDialog)
- Win32 API (全局热键, 鼠标穿透)

## 快速开始

```bash
git clone https://github.com/snqig/DesktopClock.git
cd DesktopClock/DesktopClock
dotnet build
dotnet run
```

## 截图

<!-- TODO: 添加截图 -->

## 许可证

MIT License
