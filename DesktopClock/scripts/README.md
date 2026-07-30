# scripts

DesktopClock 辅助脚本目录。

## check-dotnet.ps1

检测并一键安装 .NET 9 Desktop 运行时(`Microsoft.WindowsDesktopApp 9.x`)。

DesktopClock 以 **framework-dependent** 方式发布,运行 WPF 应用需要 .NET 9 Desktop 运行时。
本脚本面向 Windows 10/11 用户,自动检测运行时是否已安装,未安装时提示用户同意后下载官方
安装程序并静默安装。兼容 PowerShell 5.1 与 PowerShell 7。

### 用法

在项目根目录下打开 PowerShell 执行:

```powershell
# 检测并在用户同意后安装 .NET 9 Desktop 运行时
.\scripts\check-dotnet.ps1

# 强制重新安装(跳过检测)
.\scripts\check-dotnet.ps1 -Force
```

### 返回码

| 返回码 | 含义               |
| ------ | ------------------ |
| 0      | 已安装或安装成功   |
| 1      | 用户拒绝安装       |
| 2      | 下载或安装失败     |

### 说明

- 安装来源为微软官方:
  `https://dotnetcli.azureedge.net/dotnet/WindowsDesktop/9.0.0/windowsdesktop-runtime-9.0.0-win-x64.exe`
- 安装程序以 `/install /quiet /norestart` 参数静默执行。
- 安装失败或用户取消时,脚本会提示前往官方下载页手动安装:
  `https://dotnet.microsoft.com/download/dotnet/9.0`
- 如遇 PowerShell 执行策略限制,可临时放行当前会话:
  ```powershell
  Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
  ```
