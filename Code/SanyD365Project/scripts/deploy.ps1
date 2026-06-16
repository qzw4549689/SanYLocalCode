#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    完整部署脚本：生成并导入解决方案
.DESCRIPTION
    一键执行：生成解决方案包 → 导入目标环境 → 发布自定义项
.PARAMETER Environment
    目标环境（Dev/Test/Prod）
.PARAMETER SkipGenerate
    跳过生成步骤（使用现有包）
.PARAMETER Managed
    导入托管解决方案
.NOTES
    完整 CI/CD 流程的本地版本
#>

param(
    [Parameter()]
    [ValidateSet("Dev", "Test", "Prod")]
    [string]$Environment = "Dev",
    
    [Parameter()]
    [switch]$SkipGenerate,
    
    [Parameter()]
    [switch]$Managed
)

$ErrorActionPreference = "Stop"

function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

$scriptRoot = $PSScriptRoot

Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput "  Sany D365 完整部署流程" "Cyan"
Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput ""
Write-ColorOutput "目标环境: $Environment" "White"
Write-ColorOutput "托管方案: $Managed" "White"
Write-ColorOutput ""

# 步骤 1：生成解决方案（可选跳过）
if (-not $SkipGenerate) {
    Write-ColorOutput ">>> 步骤 1: 生成解决方案包" "Yellow"
    & "$scriptRoot\generate-solution.ps1"
    
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "生成失败，部署中止" "Red"
        exit 1
    }
} else {
    Write-ColorOutput ">>> 步骤 1: 跳过生成（使用现有包）" "Yellow"
}

# 步骤 2：导入解决方案
Write-ColorOutput ""
Write-ColorOutput ">>> 步骤 2: 导入解决方案到 $Environment" "Yellow"

$importArgs = @{
    Environment = $Environment
    Publish = $true
}
if ($Managed) {
    $importArgs.Managed = $true
}

& "$scriptRoot\import-solution.ps1" @importArgs

if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput "导入失败，部署中止" "Red"
    exit 1
}

# 完成
Write-ColorOutput ""
Write-ColorOutput "========================================" "Green"
Write-ColorOutput "  部署完成！" "Green"
Write-ColorOutput "========================================" "Green"
Write-ColorOutput ""
Write-ColorOutput "下一步建议：" "Cyan"
Write-ColorOutput "  1. 登录 make.powerapps.com 验证部署结果" "White"
Write-ColorOutput "  2. 运行自动化测试（如有）" "White"
Write-ColorOutput "  3. 如部署到 Test/Prod，通知相关团队" "White"
Write-ColorOutput ""
