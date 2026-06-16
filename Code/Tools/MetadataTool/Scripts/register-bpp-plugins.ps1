#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    注册 BPP Integration Plugin 到 D365 测试环境
.DESCRIPTION
    一键注册 BppIntegrationPlugin 和 BppCallbackPlugin，并发布 mcs_credit_record 实体
.PARAMETER Url
    D365 环境 URL，例如 https://sanyglobal-test.crm.dynamics.com
.PARAMETER AppId
    Microsoft Entra ID 应用注册 ID（可选，默认使用 DEV1 的 AppId）
.PARAMETER ClientSecret
    应用注册 Client Secret（与 Username/Password 二选一）
.PARAMETER TenantId
    租户 ID（可选）
.PARAMETER Username
    D365 用户名（使用密码认证时必填）
.PARAMETER Password
    D365 密码（使用密码认证时必填）
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Url,

    [Parameter(Mandatory = $false)]
    [string]$AppId = "51f81489-12ee-4a9e-aaae-a2591f45987d",

    [Parameter(Mandatory = $false, ParameterSetName = "ClientSecret")]
    [string]$ClientSecret,

    [Parameter(Mandatory = $false)]
    [string]$TenantId,

    [Parameter(Mandatory = $false, ParameterSetName = "Password")]
    [string]$Username = "gw_duanqy@sanyglobal.onmicrosoft.com",

    [Parameter(Mandatory = $false, ParameterSetName = "Password")]
    [SecureString]$Password
)

$ErrorActionPreference = "Stop"

# DLL 路径
$dllPath = "../../Customizations/Plugins/BppIntegration/bin/Release/net462/SanyD365.Plugins.BppIntegration.dll"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  注册 BPP Integration Plugins" -ForegroundColor Cyan
Write-Host "  目标环境: $Url" -ForegroundColor Cyan
if ($ClientSecret) {
    Write-Host "  认证方式: ClientSecret" -ForegroundColor Cyan
} else {
    Write-Host "  认证方式: OAuth / 用户名密码 ($Username)" -ForegroundColor Cyan
}
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $dllPath)) {
    Write-Host "错误: Plugin DLL 不存在: $dllPath" -ForegroundColor Red
    Write-Host "请先编译项目: cd Code/Customizations/Plugins/BppIntegration && dotnet build -c Release" -ForegroundColor Yellow
    exit 1
}

# 设置环境变量供 MetadataTool 读取
$env:D365_URL = $Url
$env:D365_APPID = $AppId
if ($TenantId) { $env:D365_TENANTID = $TenantId }

if ($ClientSecret) {
    $env:D365_CLIENTSECRET = $ClientSecret
    $env:D365_USERNAME = $null
    $env:D365_PASSWORD = $null
} else {
    $env:D365_CLIENTSECRET = $null
    $env:D365_USERNAME = $Username
    if ($Password) {
        $env:D365_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))
    }
}

Write-Host "[1/3] 注册 BppIntegrationPlugin (mcs_status 筛选)" -ForegroundColor Yellow
& dotnet run register-plugin-update $dllPath SanyD365.Plugins.BppIntegration.Plugin.BppIntegrationPlugin mcs_credit_record mcs_status
if ($LASTEXITCODE -ne 0) { throw "BppIntegrationPlugin 注册失败" }

Write-Host ""
Write-Host "[2/3] 注册 BppCallbackPlugin (mcs_bppstatus 筛选)" -ForegroundColor Yellow
& dotnet run register-plugin-update $dllPath SanyD365.Plugins.BppIntegration.Plugin.BppCallbackPlugin mcs_credit_record mcs_bppstatus
if ($LASTEXITCODE -ne 0) { throw "BppCallbackPlugin 注册失败" }

Write-Host ""
Write-Host "[3/3] 发布 mcs_credit_record 实体" -ForegroundColor Yellow
& dotnet run publish mcs_credit_record
if ($LASTEXITCODE -ne 0) { throw "实体发布失败" }

# 清理环境变量
$env:D365_CLIENTSECRET = $null
$env:D365_PASSWORD = $null

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  注册完成" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
