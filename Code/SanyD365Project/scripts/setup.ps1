#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Sany D365 项目初始化脚本
.DESCRIPTION
    配置 Power Platform CLI 认证、创建解决方案、设置开发环境
.NOTES
    运行前请确保已安装 pac CLI: dotnet tool install --global Microsoft.PowerApps.CLI.Tool
#>

$ErrorActionPreference = "Stop"

# 颜色定义
$Colors = @{
    Success = "Green"
    Info = "Cyan"
    Warning = "Yellow"
    Error = "Red"
}

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Colors[$Color]
}

# 读取配置
$configPath = Join-Path $PSScriptRoot ".." "config" "environment.json"
if (-not (Test-Path $configPath)) {
    Write-ColorOutput "错误：找不到配置文件 $configPath" "Error"
    exit 1
}

$config = Get-Content $configPath | ConvertFrom-Json
$solution = $config.solution
$devEnv = $config.environments | Where-Object { $_.type -eq "development" } | Select-Object -First 1

Write-ColorOutput "========================================" "Info"
Write-ColorOutput "  Sany D365 项目初始化" "Info"
Write-ColorOutput "========================================" "Info"
Write-ColorOutput ""

# 1. 检查 pac CLI
Write-ColorOutput "[1/5] 检查 Power Platform CLI..." "Info"
try {
    $pacVersion = pac --version 2>$null
    Write-ColorOutput "  ✓ pac CLI 已安装: $pacVersion" "Success"
} catch {
    Write-ColorOutput "  ✗ pac CLI 未安装" "Error"
    Write-ColorOutput "  请运行: dotnet tool install --global Microsoft.PowerApps.CLI.Tool" "Warning"
    exit 1
}

# 2. 配置认证
Write-ColorOutput ""
Write-ColorOutput "[2/5] 配置环境认证..." "Info"
Write-ColorOutput "  环境: $($devEnv.name) ($($devEnv.url))" "Info"

$authList = pac auth list --json 2>$null | ConvertFrom-Json -ErrorAction SilentlyContinue
$existingAuth = $authList | Where-Object { $_.Resource -eq $devEnv.url }

if ($existingAuth) {
    Write-ColorOutput "  ✓ 认证已存在，正在切换..." "Success"
    pac auth select --index $existingAuth.Index
} else {
    Write-ColorOutput "  正在创建新认证（将弹出浏览器进行 MFA）..." "Warning"
    pac auth create --url $devEnv.url --name $devEnv.name
}

# 3. 检查/创建解决方案
Write-ColorOutput ""
Write-ColorOutput "[3/5] 检查解决方案..." "Info"
Write-ColorOutput "  解决方案名称: $($solution.name)" "Info"
Write-ColorOutput "  显示名称: $($solution.displayName)" "Info"

$solutions = pac solution list --json 2>$null | ConvertFrom-Json -ErrorAction SilentlyContinue
$existingSolution = $solutions | Where-Object { $_.Name -eq $solution.name }

if ($existingSolution) {
    Write-ColorOutput "  ✓ 解决方案已存在" "Success"
} else {
    Write-ColorOutput "  创建新解决方案..." "Warning"
    
    # 创建临时目录用于解决方案创建
    $tempDir = Join-Path $env:TEMP "SanySolution_$([Guid]::NewGuid().ToString())"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    
    # 创建解决方案的 XML 定义
    $solutionXml = @"
<?xml version="1.0" encoding="utf-8"?>
<ImportExportXml version="9.2" SolutionPackageVersion="9.2">
  <SolutionManifest>
    <UniqueName>$($solution.name)</UniqueName>
    <LocalizedNames>
      <LocalizedName description="$($solution.displayName)" languagecode="1033" />
      <LocalizedName description="$($solution.displayName)" languagecode="2052" />
    </LocalizedNames>
    <Descriptions>
      <Description description="$($solution.description)" languagecode="1033" />
      <Description description="$($solution.description)" languagecode="2052" />
    </Descriptions>
    <Version>$($solution.version)</Version>
    <Managed>0</Managed>
    <Publisher>
      <UniqueName>$($solution.publisher.name)</UniqueName>
      <LocalizedNames>
        <LocalizedName description="$($solution.publisher.displayName)" languagecode="1033" />
      </LocalizedNames>
      <Descriptions>
        <Description description="Sany Global Publisher" languagecode="1033" />
      </Descriptions>
      <EMailAddress />
      <SupportingWebsiteUrl />
      <CustomizationPrefix>$($solution.publisher.prefix)</CustomizationPrefix>
      <CustomizationOptionValuePrefix>10000</CustomizationOptionValuePrefix>
      <Addresses>
        <Address>
          <AddressNumber>1</AddressNumber>
          <AddressTypeCode>1</AddressTypeCode>
          <City />
          <County />
          <Country />
          <Fax />
          <FreightTermsCode />
          <ImportSequenceNumber />
          <Latitude />
          <Line1 />
          <Line2 />
          <Line3 />
          <Longitude />
          <Name />
          <PostalCode />
          <PostOfficeBox />
          <PrimaryContactName />
          <ShippingMethodCode />
          <StateOrProvince />
          <Telephone1 />
          <Telephone2 />
          <Telephone3 />
          <UPSZone />
          <UTCOffset />
          <TimeZoneRuleVersionNumber />
          <UTCConversionTimeZoneCode />
        </Address>
      </Addresses>
    </Publisher>
    <RootComponents />
    <MissingDependencies />
  </SolutionManifest>
</ImportExportXml>
"@
    
    $solutionXmlPath = Join-Path $tempDir "solution.xml"
    $solutionXml | Out-File -FilePath $solutionXmlPath -Encoding UTF8
    
    # 创建 [Content_Types].xml
    $contentTypesXml = @"
<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="xml" ContentType="application/octet-stream" />
</Types>
"@
    $contentTypesPath = Join-Path $tempDir "[Content_Types].xml"
    $contentTypesXml | Out-File -FilePath $contentTypesPath -Encoding UTF8
    
    # 打包为 zip
    $zipPath = Join-Path $PSScriptRoot ".." "solutions" "unmanaged" "$($solution.name)_initial.zip"
    Compress-Archive -Path "$tempDir\*" -DestinationPath $zipPath -Force
    
    # 导入解决方案
    Write-ColorOutput "  正在导入初始解决方案..." "Info"
    pac solution import --path $zipPath --activate-plugins --skip-dependency-check
    
    # 清理临时文件
    Remove-Item -Path $tempDir -Recurse -Force
    
    Write-ColorOutput "  ✓ 解决方案创建完成" "Success"
}

# 4. 创建项目目录结构
Write-ColorOutput ""
Write-ColorOutput "[4/5] 创建项目目录结构..." "Info"

$directories = @(
    "src/entities",
    "src/option-sets",
    "src/forms",
    "src/views",
    "src/webresources",
    "solutions/unmanaged",
    "solutions/managed",
    "docs"
)

$projectRoot = Join-Path $PSScriptRoot ".."
foreach ($dir in $directories) {
    $fullPath = Join-Path $projectRoot $dir
    if (-not (Test-Path $fullPath)) {
        New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
        Write-ColorOutput "  + $dir" "Success"
    }
}

# 5. 创建 .gitignore
Write-ColorOutput ""
Write-ColorOutput "[5/5] 创建 Git 忽略文件..." "Info"

$gitignore = @"
# Power Platform
*.zip
!solutions/**/*.zip

# 临时文件
*.tmp
*.temp
*.log

# 认证信息
auth.json
*.pfx
*.cer

# IDE
.vscode/
.idea/
*.swp
*.swo

# OS
.DS_Store
Thumbs.db

# 构建输出
dist/
build/
"@

$gitignorePath = Join-Path $projectRoot ".gitignore"
$gitignore | Out-File -FilePath $gitignorePath -Encoding UTF8
Write-ColorOutput "  ✓ .gitignore 已创建" "Success"

Write-ColorOutput ""
Write-ColorOutput "========================================" "Success"
Write-ColorOutput "  初始化完成！" "Success"
Write-ColorOutput "========================================" "Success"
Write-ColorOutput ""
Write-ColorOutput "下一步：" "Info"
Write-ColorOutput "  1. 提供设计文档，我将生成实体和字段定义" "Info"
Write-ColorOutput "  2. 运行 ./scripts/generate-solution.ps1 生成解决方案包" "Info"
Write-ColorOutput "  3. 运行 ./scripts/import-solution.ps1 导入到环境" "Info"
Write-ColorOutput ""
