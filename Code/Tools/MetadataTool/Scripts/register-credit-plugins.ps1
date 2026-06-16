#Requires -Version 5.0

<#
.SYNOPSIS
    批量注册 SanyD365.D365Extension.Sales 所有 Plugin Step 到 D365 DEV1
.DESCRIPTION
    使用 MetadataTool 的 register-plugin-advanced 命令，一键注册 15 个 Step + 发布相关实体
.PARAMETER DllPath
    Plugin DLL 完整路径（默认 Debug 版本）
.PARAMETER Url
    D365 环境 URL（默认 DEV1）
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$DllPath = "C:\Users\Peter\source\repos\D365\D365\SanyD365.D365Extension.Sales\bin\Debug\SanyD365.D365Extension.Sales.dll",

    [Parameter(Mandatory = $false)]
    [string]$Url = "https://dev1.crm5.dynamics.com"
)

$ErrorActionPreference = "Stop"

# 设置认证账号（DEV1 无需密码/MFA）
$env:D365_USERNAME = "gw_qiuzw@sanyglobal.onmicrosoft.com"

# ==================== 验证 DLL ====================
if (-not (Test-Path $DllPath)) {
    Write-Host "错误: 找不到 DLL: $DllPath" -ForegroundColor Red
    Write-Host "请先在 VS 中单独生成 SanyD365.D365Extension.Sales 项目" -ForegroundColor Yellow
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  注册 Credit Plugin Assembly + Steps" -ForegroundColor Cyan
Write-Host "  DLL: $DllPath" -ForegroundColor Cyan
Write-Host "  目标: $Url" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ==================== Step 配置列表 ====================
# Stage: 10=PreValidation, 20=PreOperation, 40=PostOperation
$steps = @(
    # Account
    @{ Name = "AccountCreditValidationPlugin (Create)";       Class = "SanyD365.D365Extension.Sales.Plugins.Account.AccountCreditValidationPlugin";         Message = "Create"; Entity = "account";            Stage = 10; Filter = $null },
    @{ Name = "AccountCreditValidationPlugin (Update)";       Class = "SanyD365.D365Extension.Sales.Plugins.Account.AccountCreditValidationPlugin";         Message = "Update"; Entity = "account";            Stage = 10; Filter = $null },
    
    # CreditRecord
    @{ Name = "CreditRecordAutoNumberPlugin";                 Class = "SanyD365.D365Extension.Sales.Plugins.CreditRecord.CreditRecordAutoNumberPlugin";       Message = "Create"; Entity = "mcs_credit_record";   Stage = 20; Filter = $null },
    @{ Name = "CreditRecordBppIntegrationPlugin";             Class = "SanyD365.D365Extension.Sales.Plugins.CreditRecord.CreditRecordBppIntegrationPlugin";   Message = "Update"; Entity = "mcs_credit_record";   Stage = 40; Filter = "mcs_status" },
    @{ Name = "CreditRecordBppCallbackPlugin";                Class = "SanyD365.D365Extension.Sales.Plugins.CreditRecord.CreditRecordBppCallbackPlugin";      Message = "Update"; Entity = "mcs_credit_record";   Stage = 40; Filter = "mcs_bppstatus" },
    @{ Name = "CreditScoreCalculationPlugin";                 Class = "SanyD365.D365Extension.Sales.Plugins.CreditScore.CreditScoreCalculationPlugin";       Message = "Update"; Entity = "mcs_credit_record";   Stage = 40; Filter = "mcs_status" },
    @{ Name = "CreditScoreBpfStageSyncPlugin";                Class = "SanyD365.D365Extension.Sales.Plugins.CreditScore.CreditScoreBpfStageSyncPlugin";      Message = "Update"; Entity = "mcs_credit_record";   Stage = 40; Filter = "mcs_status" },
    @{ Name = "CofaceIntegrationDataSyncPlugin";              Class = "SanyD365.D365Extension.Sales.Plugins.CofaceIntegration.CofaceIntegrationDataSyncPlugin"; Message = "Update"; Entity = "mcs_credit_record";   Stage = 40; Filter = "mcs_status" },
    
    # CreditItems
    @{ Name = "CreditItemsValidationPlugin (Create)";         Class = "SanyD365.D365Extension.Sales.Plugins.CreditItems.CreditItemsValidationPlugin";         Message = "Create"; Entity = "mcs_credit_items";    Stage = 10; Filter = $null },
    @{ Name = "CreditItemsValidationPlugin (Update)";         Class = "SanyD365.D365Extension.Sales.Plugins.CreditItems.CreditItemsValidationPlugin";         Message = "Update"; Entity = "mcs_credit_items";    Stage = 10; Filter = $null },
    
    # CreditItemValue
    @{ Name = "CreditItemValueValidationPlugin (Create)";     Class = "SanyD365.D365Extension.Sales.Plugins.CreditItemValue.CreditItemValueValidationPlugin"; Message = "Create"; Entity = "mcs_credit_itemvalue"; Stage = 10; Filter = $null },
    @{ Name = "CreditItemValueValidationPlugin (Update)";     Class = "SanyD365.D365Extension.Sales.Plugins.CreditItemValue.CreditItemValueValidationPlugin"; Message = "Update"; Entity = "mcs_credit_itemvalue"; Stage = 10; Filter = $null },
    
    # CustomerTag
    @{ Name = "CustomerTagInitPlugin";                        Class = "SanyD365.D365Extension.Sales.Plugins.CustomerTag.CustomerTagInitPlugin";               Message = "Create"; Entity = "mcs_customer_tag";    Stage = 40; Filter = $null },
    @{ Name = "CustomerTagValidationPlugin";                  Class = "SanyD365.D365Extension.Sales.Plugins.CustomerTag.CustomerTagValidationPlugin";         Message = "Update"; Entity = "mcs_customer_tag";    Stage = 20; Filter = $null },
    
    # ScoringCard
    @{ Name = "ScoringCardAutoNumberPlugin";                  Class = "SanyD365.D365Extension.Sales.Plugins.ScoringCard.ScoringCardAutoNumberPlugin";        Message = "Create"; Entity = "mcs_credit_scoringcard"; Stage = 20; Filter = $null },
)

# ==================== 批量注册 ====================
$total = $steps.Count
$current = 0

foreach ($step in $steps) {
    $current++
    Write-Host "[$current/$total] $($step.Name) ..." -ForegroundColor Yellow -NoNewline
    
    $cmdArgs = @("run", "register-plugin-advanced", $DllPath, $step.Class, $step.Entity, $step.Message, $step.Stage)
    if ($step.Filter) {
        $cmdArgs += $step.Filter
    }
    
    $output = & dotnet @cmdArgs 2>&1
    $exitCode = $LASTEXITCODE
    
    if ($exitCode -ne 0) {
        Write-Host " 失败!" -ForegroundColor Red
        Write-Host $output -ForegroundColor Red
        throw "注册失败: $($step.Name)"
    }
    
    # 简单判断输出结果
    if ($output -match "已存在") {
        Write-Host " 已存在，跳过" -ForegroundColor DarkGray
    } elseif ($output -match "已更新") {
        Write-Host " 已更新" -ForegroundColor Green
    } else {
        Write-Host " 完成" -ForegroundColor Green
    }
}

# ==================== 发布实体 ====================
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  发布相关实体" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$entities = @("account", "mcs_credit_record", "mcs_credit_items", "mcs_credit_itemvalue", "mcs_customer_tag", "mcs_credit_scoringcard")
foreach ($entity in $entities) {
    Write-Host "发布: $entity ..." -ForegroundColor Yellow -NoNewline
    $output = & dotnet run publish $entity 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host " 失败 (非致命)" -ForegroundColor DarkYellow
    } else {
        Write-Host " 完成" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  全部完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "下一步:" -ForegroundColor Cyan
Write-Host "  1. 在 PRT 中把 Assembly 和 Steps 加入 McsPlugin Solution" -ForegroundColor White
Write-Host "  2. 让同事导出 McsPlugin Solution 并导入 UAT" -ForegroundColor White
