# D365 客户信用评估系统 - 部署脚本
# 用于注册Plugin和上传WebResource

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Plugin", "WebResource", "All")]
    [string]$DeployType,
    
    [string]$ConnectionString = "AuthType=OAuth;Url=https://dev1.crm5.dynamics.com;AppId=8e7f5a1c-3b2d-4e5f-9a8b-7c6d5e4f3a2b;RedirectUri=http://localhost;Username=gw_duanqy@sanyglobal.onmicrosoft.com;Password=1qaz2wsxE;LoginPrompt=Auto"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "D365 客户信用评估系统 - 部署工具" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查路径
$PluginPath = "./Plugins/ScoringCard/AutoNumber/bin/Release/net462/SanyD365.Plugins.ScoringCard.dll"
$JsPath = "./WebResources/JS/mcs_credit_scoringcard.js"

function Deploy-Plugin {
    Write-Host "【部署Plugin】" -ForegroundColor Yellow
    Write-Host ""
    
    # 检查DLL是否存在
    if (-not (Test-Path $PluginPath)) {
        Write-Host "错误: Plugin DLL不存在: $PluginPath" -ForegroundColor Red
        Write-Host "请先编译Plugin项目:" -ForegroundColor Yellow
        Write-Host "  cd Plugins/ScoringCard/AutoNumber" -ForegroundColor Gray
        Write-Host "  dotnet build -c Release" -ForegroundColor Gray
        return
    }
    
    Write-Host "Plugin文件: $PluginPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "请使用 Plugin Registration Tool 手动注册:" -ForegroundColor Yellow
    Write-Host "  1. 打开 Plugin Registration Tool" -ForegroundColor White
    Write-Host "  2. 连接到 DEV1 环境" -ForegroundColor White
    Write-Host "  3. 注册新Assembly: $PluginPath" -ForegroundColor White
    Write-Host "  4. 勾选 Sandbox" -ForegroundColor White
    Write-Host "  5. 注册Step:" -ForegroundColor White
    Write-Host "     - Message: Create" -ForegroundColor Gray
    Write-Host "     - Primary Entity: mcs_credit_scoringcard" -ForegroundColor Gray
    Write-Host "     - Stage: Pre-operation" -ForegroundColor Gray
    Write-Host "     - Mode: Synchronous" -ForegroundColor Gray
    Write-Host ""
}

function Deploy-WebResource {
    Write-Host "【部署WebResource】" -ForegroundColor Yellow
    Write-Host ""
    
    # 检查JS文件是否存在
    if (-not (Test-Path $JsPath)) {
        Write-Host "错误: JS文件不存在: $JsPath" -ForegroundColor Red
        return
    }
    
    Write-Host "JS文件: $JsPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "请通过Power Apps手动上传:" -ForegroundColor Yellow
    Write-Host "  1. 登录 https://make.powerapps.com" -ForegroundColor White
    Write-Host "  2. 选择环境: DEV1" -ForegroundColor White
    Write-Host "  3. 解决方案 → entity_20260603_peter" -ForegroundColor White
    Write-Host "  4. 点击 '+ 新建' → 'Web 资源'" -ForegroundColor White
    Write-Host "  5. 填写信息:" -ForegroundColor White
    Write-Host "     - 名称: mcs_credit_scoringcard.js" -ForegroundColor Gray
    Write-Host "     - 显示名称: 评分卡配置表-表单逻辑" -ForegroundColor Gray
    Write-Host "     - 类型: 脚本(JScript)" -ForegroundColor Gray
    Write-Host "  6. 上传文件: $JsPath" -ForegroundColor White
    Write-Host "  7. 保存并发布" -ForegroundColor White
    Write-Host ""
    Write-Host "【绑定到表单】" -ForegroundColor Yellow
    Write-Host "  1. 打开 '客户评分卡配置表' Main窗体" -ForegroundColor White
    Write-Host "  2. 点击 '表单属性'" -ForegroundColor White
    Write-Host "  3. 添加事件处理程序:" -ForegroundColor White
    Write-Host "     - OnLoad: ScoringCardForm.onLoad" -ForegroundColor Gray
    Write-Host "     - OnSave: ScoringCardForm.onSave" -ForegroundColor Gray
    Write-Host "  4. 保存并发布" -ForegroundColor White
    Write-Host ""
}

# 主逻辑
switch ($DeployType) {
    "Plugin" { Deploy-Plugin }
    "WebResource" { Deploy-WebResource }
    "All" {
        Deploy-Plugin
        Deploy-WebResource
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "部署说明完成" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
