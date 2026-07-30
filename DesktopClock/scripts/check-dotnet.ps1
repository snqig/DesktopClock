<#
.SYNOPSIS
    检测并一键安装 .NET 9 Desktop 运行时 (WindowsDesktopApp 9.x)。
.DESCRIPTION
    DesktopClock 应用以 framework-dependent 方式发布,运行需要 .NET 9 Desktop 运行时。
    本脚本检测系统中是否已安装 Microsoft.WindowsDesktopApp 9.x,若未安装则提示用户
    下载并静默安装官方 .NET 9 Desktop Runtime installer。
    兼容 PowerShell 5.1 与 PowerShell 7。
.PARAMETER Force
    强制重新安装 .NET 9 Desktop 运行时,跳过检测步骤。
.EXAMPLE
    .\scripts\check-dotnet.ps1
    检测并在用户同意后安装 .NET 9 Desktop 运行时。
.EXAMPLE
    .\scripts\check-dotnet.ps1 -Force
    强制重新安装 .NET 9 Desktop 运行时。
.NOTES
    返回码:
      0 = 已安装或安装成功
      1 = 用户拒绝安装
      2 = 下载或安装失败
#>
[CmdletBinding()]
param(
    [switch]$Force
)

# 确保 TLS 1.2(对旧版 PowerShell 5.1 默认协议进行修正)
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
} catch {
    # 某些环境无法设置,忽略错误继续
}

$ErrorActionPreference = "Stop"

$DownloadUrl    = "https://dotnetcli.azureedge.net/dotnet/WindowsDesktop/9.0.0/windowsdesktop-runtime-9.0.0-win-x64.exe"
$DownloadPage   = "https://dotnet.microsoft.com/download/dotnet/9.0"
$InstallerPath  = Join-Path $env:TEMP "dotnet-runtime-install.exe"
$RequiredMajor  = 9

function Write-Ok {
    param([string]$Message)
    Write-Host "[OK]   $Message" -ForegroundColor Green
}

function Write-Err {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[WARN]  $Message" -ForegroundColor Yellow
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO]  $Message" -ForegroundColor Cyan
}

function Get-DotNetRuntimes {
    # 返回 dotnet --list-runtimes 的输出行数组;命令不存在或失败时返回 $null。
    try {
        $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
        if (-not $cmd) { return $null }
        $output = & dotnet --list-runtimes 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
        return $output
    } catch {
        return $null
    }
}

function Test-DotNet9DesktopRuntime {
    # 检查是否安装 Microsoft.WindowsDesktopApp 9.x
    $runtimes = Get-DotNetRuntimes
    if (-not $runtimes) { return $false }
    foreach ($line in $runtimes) {
        if ($line -match "^Microsoft\.WindowsDesktopApp\s+(\d+)\.") {
            if ([int]$Matches[1] -eq $RequiredMajor) {
                return $true
            }
        }
    }
    return $false
}

function Get-InstalledDesktopRuntimeVersion {
    # 返回已安装的 WindowsDesktopApp 版本号字符串;未找到返回 $null
    $runtimes = Get-DotNetRuntimes
    if (-not $runtimes) { return $null }
    foreach ($line in $runtimes) {
        if ($line -match "^Microsoft\.WindowsDesktopApp\s+([\d\.]+)") {
            return $Matches[1]
        }
    }
    return $null
}

function Update-EnvPath {
    # 从注册表刷新当前会话的 PATH(安装完成后 dotnet 可能刚加入系统 PATH)
    try {
        $machinePath = [Environment]::GetEnvironmentVariable("PATH", "Machine")
        $userPath    = [Environment]::GetEnvironmentVariable("PATH", "User")
        if ($machinePath -and $userPath) {
            $env:PATH = "$machinePath;$userPath"
        } elseif ($machinePath) {
            $env:PATH = $machinePath
        } elseif ($userPath) {
            $env:PATH = $userPath
        }
    } catch {
        # 忽略刷新失败
    }
}

Write-Host ""
Write-Info "DesktopClock - .NET 9 Desktop 运行时检测工具"
Write-Host ""

# ========== 检测阶段 ==========
if (-not $Force) {
    Write-Info "正在检测已安装的 .NET 运行时..."
    if (Test-DotNet9DesktopRuntime) {
        $ver = Get-InstalledDesktopRuntimeVersion
        Write-Ok "已检测到 .NET 9 Desktop 运行时 (版本 $ver)"
        Write-Host ""
        Write-Ok "环境就绪,可以运行 DesktopClock。"
        exit 0
    }
    $currentVer = Get-InstalledDesktopRuntimeVersion
    if ($currentVer) {
        Write-Warn "检测到 WindowsDesktopApp 版本 $currentVer,但不是 $RequiredMajor.x"
    } else {
        Write-Warn "未检测到 Microsoft.WindowsDesktopApp 运行时"
    }
} else {
    Write-Warn "已指定 -Force 参数,将强制重新安装 .NET 9 Desktop 运行时"
}

# ========== 询问阶段 ==========
Write-Host ""
Write-Info "WPF 应用 DesktopClock 需要 .NET 9 Desktop 运行时才能运行。"
Write-Info "将下载官方安装程序并静默安装。"
Write-Info "下载地址: $DownloadUrl"
Write-Host ""

$answer = Read-Host "是否同意安装 .NET 9 Desktop 运行时? (Y/n)"
if ($answer -and ($answer -notmatch "^[Yy]")) {
    Write-Err "用户取消安装。"
    Write-Info "您可以手动下载安装: $DownloadPage"
    exit 1
}

# ========== 下载阶段 ==========
Write-Host ""
Write-Info "正在下载安装程序到: $InstallerPath"
try {
    # 删除可能存在的旧文件
    if (Test-Path $InstallerPath) {
        Remove-Item $InstallerPath -Force
    }
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $InstallerPath -UseBasicParsing
} catch {
    Write-Err "下载失败: $($_.Exception.Message)"
    Write-Info "请手动下载安装: $DownloadPage"
    exit 2
}

if (-not (Test-Path $InstallerPath)) {
    Write-Err "下载文件未生成,安装中止。"
    Write-Info "请手动下载安装: $DownloadPage"
    exit 2
}

$size = (Get-Item $InstallerPath).Length
Write-Ok "下载完成 (大小: $([math]::Round($size / 1MB, 2)) MB)"

# ========== 安装阶段 ==========
Write-Host ""
Write-Info "正在静默安装,请稍候 (可能需要 1-2 分钟)..."
$exitCode = $null
try {
    $proc = Start-Process -FilePath $InstallerPath `
                          -ArgumentList "/install","/quiet","/norestart" `
                          -Wait -PassThru
    $exitCode = $proc.ExitCode
} catch {
    Write-Err "安装程序启动失败: $($_.Exception.Message)"
    Write-Info "请手动下载安装: $DownloadPage"
    exit 2
}

# 0 = 成功;1638 = 已安装相同或更高版本(视为成功)
if ($exitCode -ne 0 -and $exitCode -ne 1638) {
    Write-Err "安装失败,安装程序退出码: $exitCode"
    Write-Info "请手动下载安装: $DownloadPage"
    exit 2
}

if ($exitCode -eq 1638) {
    Write-Warn "系统已安装相同或更高版本 (退出码 1638)"
} else {
    Write-Ok "安装程序执行完成"
}

# ========== 验证阶段 ==========
Write-Host ""
Write-Info "正在验证安装结果..."
Start-Sleep -Seconds 2
Update-EnvPath

if (Test-DotNet9DesktopRuntime) {
    $ver = Get-InstalledDesktopRuntimeVersion
    Write-Ok "验证成功,已安装 .NET 9 Desktop 运行时 (版本 $ver)"
    Write-Host ""
    Write-Ok "环境就绪,可以运行 DesktopClock。"
    exit 0
} else {
    Write-Warn "安装后未能立即检测到运行时。"
    Write-Info "可能原因:当前终端的 PATH 尚未刷新。"
    Write-Info "请关闭并重新打开终端后再次运行本脚本验证,或直接启动 DesktopClock。"
    Write-Info "如仍有问题,请手动安装: $DownloadPage"
    exit 2
}
