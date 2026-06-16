#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    导入解决方案到 Power Platform 环境
.DESCRIPTION
    将生成的解决方案包导入到指定的 D365/Power Apps 环境
.PARAMETER Environment
    目标环境名称（Dev/Test/Prod）
.PARAMETER SolutionPath
    解决方案包路径（可选，默认使用最新生成的包）
.PARAMETER Managed
    是否导入托管解决方案
.PARAMETER Publish
    导入后是否发布所有自定义项
.NOTES
    导入前请确保已通过 MFA 认证
#>

param(
    [Parameter()]
    [ValidateSet("Dev", "Test", "Prod")]
    [string]$Environment = "Dev",
    
    [Parameter()]
    [string]$SolutionPath = "",
    
    [Parameter()]
    [switch]$Managed,
    
    [Parameter()]
    [switch]$Publish
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

# 确定目标环境
$targetEnv = $config.environments | Where-Object { $_.name -eq "Sany$Environment" }
if (-not $targetEnv) {
    Write-ColorOutput "错误：找不到环境 Sany$Environment" "Red"
    exit 1
}

# 确定解决方案路径
if ([string]::IsNullOrEmpty($SolutionPath)) {
    $solutionFolder = if ($Managed) { "managed" } else { "unmanaged" }
    $SolutionPath = Join-Path $PSScriptRoot ".." "solutions" $solutionFolder "$($solution.name).zip"
}

if (-not (Test-Path $SolutionPath)) {
    Write-ColorOutput "错误：找不到解决方案包 $SolutionPath" "Red"
    Write-ColorOutput "请先运行 ./scripts/generate-solution.ps1 生成解决方案包" "Yellow"
    exit 1
}

Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput "  导入 Power Platform 解决方案" "Cyan"
Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput ""
Write-ColorOutput "目标环境: $($targetEnv.name) ($($targetEnv.url))" "White"
Write-ColorOutput "解决方案: $($solution.name)" "White"
Write-ColorOutput "包路径: $SolutionPath" "White"
Write-ColorOutput "托管方案: $Managed" "White"
Write-ColorOutput ""

# 检查认证
Write-ColorOutput "[1/3] 检查环境认证..." "Cyan"
$authList = pac auth list --json 2>$null | ConvertFrom-Json -ErrorAction SilentlyContinue
$currentAuth = $authList | Where-Object { $_.Resource -eq $targetEnv.url }

if (-not $currentAuth) {
    Write-ColorOutput "  未找到认证，正在创建..." "Yellow"
    Write-ColorOutput "  将弹出浏览器进行 MFA 认证" "Yellow"
    pac auth create --url $targetEnv.url --name $targetEnv.name
} else {
    Write-ColorOutput "  切换到认证: $($currentAuth.Name)" "Green"
    pac auth select --index $currentAuth.Index
}

# 导入解决方案
Write-ColorOutput ""
Write-ColorOutput "[2/3] 导入解决方案..." "Cyan"
Write-ColorOutput "  这可能需要几分钟时间..." "Yellow"

try {
    $importArgs = @(
        "solution", "import",
        "--path", $SolutionPath,
        "--activate-plugins",
        "--skip-dependency-check",
        "--async"
    )
    
    if ($Managed) {
        $importArgs += "--import-as-managed"
    }
    
    & pac @importArgs
    
    if ($LASTEXITCODE -ne 0) {
        throw "导入失败，退出代码: $LASTEXITCODE"
    }
    
    Write-ColorOutput "  ✓ 解决方案导入成功" "Green"
} catch {
    Write-ColorOutput "  ✗ 导入失败: $_" "Red"
    Write-ColorOutput ""
    Write-ColorOutput "常见解决方法：" "Yellow"
    Write-ColorOutput "  1. 确认已通过 MFA 完成认证" "White"
    Write-ColorOutput "  2. 检查解决方案包是否完整" "White"
    Write-ColorOutput "  3. 确认目标环境 URL 正确" "White"
    Write-ColorOutput "  4. 检查是否有足够的权限" "White"
    exit 1
}

# 发布自定义项
if ($Publish) {
    Write-ColorOutput ""
    Write-ColorOutput "[3/3] 发布所有自定义项..." "Cyan"
    
    try {
        pac solution publish
        Write-ColorOutput "  ✓ 发布成功" "Green"
    } catch {
        Write-ColorOutput "  ⚠ 发布失败（可手动在环境中发布）" "Yellow"
    }
} else {
    Write-ColorOutput ""
    Write-ColorOutput "[3/3] 跳过发布（使用 -Publish 参数可自动发布）" "Cyan"
}

Write-ColorOutput ""
Write-ColorOutput "========================================" "Green"
Write-ColorOutput "  导入完成！" "Green"
Write-ColorOutput "========================================" "Green"
Write-ColorOutput ""
Write-ColorOutput "请在 Power Apps 中验证：" "Cyan"
Write-ColorOutput "  1. 进入 make.powerapps.com" "White"
Write-ColorOutput "  2. 选择 $($targetEnv.name) 环境" "White"
Write-ColorOutput "  3. 检查解决方案 $($solution.name) 是否正确导入" "White"
Write-ColorOutput "  4. 验证实体、字段、表单是否正常" "White"
Write-ColorOutput ""
