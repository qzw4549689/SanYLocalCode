#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    从 Power Platform 环境导出解决方案
.DESCRIPTION
    将环境中的解决方案导出为 ZIP 包，用于备份或部署到其他环境
.PARAMETER Environment
    源环境名称（Dev/Test/Prod）
.PARAMETER Managed
    导出为托管解决方案
.PARAMETER IncrementVersion
    导出时递增版本号
.NOTES
    导出前请确保已通过 MFA 认证
#>

param(
    [Parameter()]
    [ValidateSet("Dev", "Test", "Prod")]
    [string]$Environment = "Dev",
    
    [Parameter()]
    [switch]$Managed,
    
    [Parameter()]
    [switch]$IncrementVersion
)

$ErrorActionPreference = "Stop"

function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

# 读取配置
$configPath = Join-Path $PSScriptRoot ".." "config" "environment.json"
$config = Get-Content $configPath | ConvertFrom-Json
$solution = $config.solution

# 确定源环境
$sourceEnv = $config.environments | Where-Object { $_.name -eq "Sany$Environment" }
if (-not $sourceEnv) {
    Write-ColorOutput "错误：找不到环境 Sany$Environment" "Red"
    exit 1
}

# 确定输出路径
$solutionFolder = if ($Managed) { "managed" } else { "unmanaged" }
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outputFileName = if ($Managed) { 
    "$($solution.name)_$($solution.version)_$timestamp`_managed.zip" 
} else { 
    "$($solution.name)_$($solution.version)_$timestamp`.zip" 
}
$outputPath = Join-Path $PSScriptRoot ".." "solutions" $solutionFolder $outputFileName

# 确保输出目录存在
$outputDir = Split-Path -Parent $outputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput "  导出 Power Platform 解决方案" "Cyan"
Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput ""
Write-ColorOutput "源环境: $($sourceEnv.name) ($($sourceEnv.url))" "White"
Write-ColorOutput "解决方案: $($solution.name)" "White"
Write-ColorOutput "版本: $($solution.version)" "White"
Write-ColorOutput "托管方案: $Managed" "White"
Write-ColorOutput "输出路径: $outputPath" "White"
Write-ColorOutput ""

# 检查认证
Write-ColorOutput "[1/2] 检查环境认证..." "Cyan"
$authList = pac auth list --json 2>$null | ConvertFrom-Json -ErrorAction SilentlyContinue
$currentAuth = $authList | Where-Object { $_.Resource -eq $sourceEnv.url }

if (-not $currentAuth) {
    Write-ColorOutput "  未找到认证，正在创建..." "Yellow"
    Write-ColorOutput "  将弹出浏览器进行 MFA 认证" "Yellow"
    pac auth create --url $sourceEnv.url --name $sourceEnv.name
} else {
    Write-ColorOutput "  切换到认证: $($currentAuth.Name)" "Green"
    pac auth select --index $currentAuth.Index
}

# 导出解决方案
Write-ColorOutput ""
Write-ColorOutput "[2/2] 导出解决方案..." "Cyan"
Write-ColorOutput "  这可能需要几分钟时间..." "Yellow"

try {
    $exportArgs = @(
        "solution", "export",
        "--name", $solution.name,
        "--path", $outputPath
    )
    
    if ($Managed) {
        $exportArgs += "--managed"
    }
    
    if ($IncrementVersion) {
        $exportArgs += "--increment-version"
    }
    
    & pac @exportArgs
    
    if ($LASTEXITCODE -ne 0) {
        throw "导出失败，退出代码: $LASTEXITCODE"
    }
    
    Write-ColorOutput "  ✓ 解决方案导出成功" "Green"
} catch {
    Write-ColorOutput "  ✗ 导出失败: $_" "Red"
    exit 1
}

Write-ColorOutput ""
Write-ColorOutput "========================================" "Green"
Write-ColorOutput "  导出完成！" "Green"
Write-ColorOutput "========================================" "Green"
Write-ColorOutput ""
Write-ColorOutput "文件位置: $outputPath" "Cyan"
Write-ColorOutput ""
Write-ColorOutput "用途：" "Yellow"
if ($Managed) {
    Write-ColorOutput "  此托管解决方案可用于部署到 Test/Prod 环境" "White"
} else {
    Write-ColorOutput "  此非托管解决方案可用于版本控制和备份" "White"
}
Write-ColorOutput ""
