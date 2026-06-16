#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    根据设计文档生成 Power Platform 解决方案包
.DESCRIPTION
    读取 src/ 目录下的实体、字段、表单、视图定义，生成可导入的解决方案 ZIP 包
.NOTES
    此脚本将 XML 定义打包为标准 D365 解决方案格式
#>

$ErrorActionPreference = "Stop"

# 颜色输出函数
function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

# 读取配置
$configPath = Join-Path $PSScriptRoot ".." "config" "environment.json"
$config = Get-Content $configPath | ConvertFrom-Json
$solution = $config.solution

Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput "  生成 Power Platform 解决方案包" "Cyan"
Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput ""

$projectRoot = Join-Path $PSScriptRoot ".."
$srcPath = Join-Path $projectRoot "src"
$solutionOutputPath = Join-Path $projectRoot "solutions" "unmanaged" "$($solution.name).zip"

# 创建临时工作目录
$tempDir = Join-Path $env:TEMP "SanySolutionBuild_$([Guid]::NewGuid().ToString())"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

Write-ColorOutput "[1/4] 准备解决方案结构..." "Cyan"

# 创建标准解决方案目录结构
$solutionFolders = @(
    "Entities",
    "OptionSets",
    "WebResources",
    "Workflows",
    "Roles",
    "FieldSecurityProfiles"
)

foreach ($folder in $solutionFolders) {
    New-Item -ItemType Directory -Path (Join-Path $tempDir $folder) -Force | Out-Null
}

# 生成 solution.xml
Write-ColorOutput "[2/4] 生成 solution.xml..." "Cyan"

$rootComponents = @()
$missingDependencies = @()

# 扫描实体定义
$entitiesPath = Join-Path $srcPath "entities"
if (Test-Path $entitiesPath) {
    $entityFiles = Get-ChildItem -Path $entitiesPath -Filter "*.xml" -Recurse
    foreach ($entityFile in $entityFiles) {
        $entityName = [System.IO.Path]::GetFileNameWithoutExtension($entityFile.Name)
        $rootComponents += "    <RootComponent type=`"1`" id={`"$([Guid]::NewGuid().ToString())`"} behavior=`"0`" />"
        
        # 复制实体文件到解决方案目录
        $targetPath = Join-Path $tempDir "Entities" "$entityName.xml"
        Copy-Item -Path $entityFile.FullName -Destination $targetPath -Force
        Write-ColorOutput "  + 实体: $entityName" "Green"
    }
}

# 扫描选项集定义
$optionSetsPath = Join-Path $srcPath "option-sets"
if (Test-Path $optionSetsPath) {
    $optionSetFiles = Get-ChildItem -Path $optionSetsPath -Filter "*.xml" -Recurse
    foreach ($optionSetFile in $optionSetFiles) {
        $optionSetName = [System.IO.Path]::GetFileNameWithoutExtension($optionSetFile.Name)
        $rootComponents += "    <RootComponent type=`"9`" id={`"$([Guid]::NewGuid().ToString())`"} behavior=`"0`" />"
        
        $targetPath = Join-Path $tempDir "OptionSets" "$optionSetName.xml"
        Copy-Item -Path $optionSetFile.FullName -Destination $targetPath -Force
        Write-ColorOutput "  + 选项集: $optionSetName" "Green"
    }
}

# 生成 solution.xml 内容
$rootComponentsXml = if ($rootComponents.Count -gt 0) { 
    $rootComponents -join "`n" 
} else { 
    "" 
}

$solutionXmlContent = @"
<?xml version="1.0" encoding="utf-8"?>
<ImportExportXml version="9.2.24052.100" SolutionPackageVersion="9.2">
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
    <RootComponents>
$rootComponentsXml
    </RootComponents>
    <MissingDependencies />
  </SolutionManifest>
</ImportExportXml>
"@

$solutionXmlPath = Join-Path $tempDir "solution.xml"
$solutionXmlContent | Out-File -FilePath $solutionXmlPath -Encoding UTF8

# 生成 [Content_Types].xml
Write-ColorOutput "[3/4] 生成 [Content_Types].xml..." "Cyan"

$contentTypesXml = @"
<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="xml" ContentType="application/octet-stream" />
  <Default Extension="xaml" ContentType="application/octet-stream" />
  <Default Extension="png" ContentType="image/png" />
  <Default Extension="jpg" ContentType="image/jpeg" />
  <Default Extension="gif" ContentType="image/gif" />
  <Default Extension="js" ContentType="application/javascript" />
  <Default Extension="css" ContentType="text/css" />
  <Default Extension="html" ContentType="text/html" />
</Types>
"@

$contentTypesPath = Join-Path $tempDir "[Content_Types].xml"
$contentTypesXml | Out-File -FilePath $contentTypesPath -Encoding UTF8

# 打包为 ZIP
Write-ColorOutput "[4/4] 打包解决方案..." "Cyan"

# 确保输出目录存在
$solutionDir = Split-Path -Parent $solutionOutputPath
if (-not (Test-Path $solutionDir)) {
    New-Item -ItemType Directory -Path $solutionDir -Force | Out-Null
}

# 删除旧的解决方案包
if (Test-Path $solutionOutputPath) {
    Remove-Item -Path $solutionOutputPath -Force
}

# 压缩文件
Compress-Archive -Path "$tempDir\*" -DestinationPath $solutionOutputPath -Force

# 清理临时目录
Remove-Item -Path $tempDir -Recurse -Force

Write-ColorOutput ""
Write-ColorOutput "========================================" "Green"
Write-ColorOutput "  解决方案包生成完成！" "Green"
Write-ColorOutput "========================================" "Green"
Write-ColorOutput ""
Write-ColorOutput "输出文件: $solutionOutputPath" "Cyan"
Write-ColorOutput ""
Write-ColorOutput "下一步操作：" "Yellow"
Write-ColorOutput "  1. 验证解决方案包内容" "White"
Write-ColorOutput "  2. 运行 ./scripts/import-solution.ps1 导入到环境" "White"
Write-ColorOutput "  3. 或使用 pac solution import --path $solutionOutputPath" "White"
Write-ColorOutput ""
