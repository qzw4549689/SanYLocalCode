# D365 Plugin 注册最佳实践 — DLL 与 Step 分离注册

> 经验来源：2026-06-10 将 6 个独立 Assembly + 15 个 Plugin Step 合并为 1 个统一 Assembly 的迁移实践

---

## 1. 问题背景

当 Plugin Assembly 的 DLL 较大时（如 8MB+），通过命令行或代码直接上传 DLL 到 D365 极易超时：

```
错误: An error occurred while sending the request.
```

原因：
- DLL 需先转 Base64，8MB → ~10.7MB 文本
- D365 Online 对单次 Web API 请求有大小和超时限制
- 网络不稳定时大文件上传失败率极高

**结论：大 DLL 不要和 Step 注册混在一个流程里反复上传。**

---

## 2. 核心策略：DLL 只传一次，Step 批量注册

### 2.1 两步走流程

```
第1步：register-plugin-advanced（只执行1次）
  → 上传 DLL
  → 注册/更新 Assembly
  → 注册/更新 Plugin Type
  → 顺便注册第1个 Step

第2步：register-step-only（执行 N 次，每个 Step 一次）
  → 只注册 Step
  → 不碰 DLL，不碰 Assembly
  → 每个 Step 仅需 2-5 秒
```

### 2.2 对比

| 方式 | 每次耗时 | 15个Step总耗时 | 稳定性 |
|---|---|---|---|
| `register-plugin-advanced` × 15 | 3-5分钟/个 | 45-75分钟 | ❌ 极易超时 |
| `register-plugin-advanced` × 1 + `register-step-only` × 14 | 3-5分钟 + 2-5秒/个 | ~5分钟 | ✅ 稳定 |

---

## 3. 命令详解

### 3.1 第一步：注册 Assembly + Type + 第一个 Step

```bash
cd /d/AIWork/Code/Tools/MetadataTool

dotnet run register-plugin-advanced \
  "<DLL完整路径>" \
  "<完整类名>" \
  "<实体名>" \
  "<消息名>" \
  "<阶段值>" \
  "[筛选属性]"
```

参数说明：

| 参数 | 示例 | 说明 |
|---|---|---|
| DLL路径 | `C:\...\bin\Debug\SanyD365.D365Extension.Sales.dll` | 绝对路径，含空格需引号包裹 |
| 完整类名 | `SanyD365.D365Extension.Sales.Plugins.CofaceIntegration.CofaceIntegrationDataSyncPlugin` | 含命名空间 |
| 实体名 | `mcs_credit_record` | **注意：必须用 schema name，不是显示名称** |
| 消息名 | `Update` / `Create` | 大写首字母 |
| 阶段值 | `10`/`20`/`40` | 10=PreValidation, 20=PreOperation, 40=PostOperation |
| 筛选属性 | `mcs_status` | 可选，只监听指定字段变化 |

**关键：只要执行一次，Assembly 和 Type 就都有了。**

### 3.2 第二步：只注册剩余 Step

```bash
cd /d/AIWork/Code/Tools/MetadataTool

dotnet run register-step-only \
  "<完整类名>" \
  "<实体名>" \
  "<消息名>" \
  "<阶段值>" \
  "[筛选属性]"
```

**特点：**
- 不读取 DLL 文件
- 不上传 Assembly
- 只查询已有 PluginType，然后创建/更新 SdkMessageProcessingStep
- 即使 Step 已存在也会更新配置（如筛选属性变化）

---

## 4. 批量脚本模板

将以下脚本保存为 `.ps1`，执行前修改 `$dllPath` 和 ` $assemblyName`：

```powershell
# 配置
$dllPath = "C:\Users\Peter\source\repos\D365\D365\SanyD365.D365Extension.Sales\bin\Debug\SanyD365.D365Extension.Sales.dll"
$toolPath = "d:\AIWork\Code\Tools\MetadataTool"

# Step 定义：@("类名", "实体", "消息", "阶段", "筛选属性")
$steps = @(
    # 第1个 Step：用 register-plugin-advanced（会上传DLL）
    @("SanyD365.D365Extension.Sales.Plugins.CofaceIntegration.CofaceIntegrationDataSyncPlugin", "mcs_credit_record", "Update", "40", "mcs_status"),
    
    # 剩余 Step：用 register-step-only（只注册Step）
    @("SanyD365.D365Extension.Sales.Plugins.AccountCreditValidationPlugin", "account", "Create", "10", $null),
    @("SanyD365.D365Extension.Sales.Plugins.AccountCreditValidationPlugin", "account", "Update", "10", $null),
    @("SanyD365.D365Extension.Sales.Plugins.CreditRecordAutoNumberPlugin", "mcs_credit_record", "Create", "20", $null),
    @("SanyD365.D365Extension.Sales.Plugins.CreditRecordBppIntegrationPlugin", "mcs_credit_record", "Update", "40", "mcs_status"),
    @("SanyD365.D365Extension.Sales.Plugins.CreditRecordBppCallbackPlugin", "mcs_credit_record", "Update", "40", "mcs_bppstatus"),
    @("SanyD365.D365Extension.Sales.Plugins.CreditScoreCalculationPlugin", "mcs_credit_record", "Update", "40", "mcs_status"),
    @("SanyD365.D365Extension.Sales.Plugins.CreditScoreBpfStageSyncPlugin", "mcs_credit_record", "Update", "40", "mcs_status"),
    @("SanyD365.D365Extension.Sales.Plugins.CreditItemsValidationPlugin", "mcs_credit_items", "Create", "10", $null),
    @("SanyD365.D365Extension.Sales.Plugins.CreditItemsValidationPlugin", "mcs_credit_items", "Update", "10", $null),
    @("SanyD365.D365Extension.Sales.Plugins.CreditItemValueValidationPlugin", "mcs_credititem_value", "Create", "10", $null),
    @("SanyD365.D365Extension.Sales.Plugins.CreditItemValueValidationPlugin", "mcs_credititem_value", "Update", "10", $null),
    @("SanyD365.D365Extension.Sales.Plugins.CustomerTagInitPlugin", "mcs_customer_tag", "Create", "40", $null),
    @("SanyD365.D365Extension.Sales.Plugins.CustomerTagValidationPlugin", "mcs_customer_tag", "Update", "20", $null),
    @("SanyD365.D365Extension.Sales.Plugins.ScoringCardAutoNumberPlugin", "mcs_credit_scoringcard", "Create", "20", $null),
    @("SanyD365.D365Extension.Sales.Plugins.ScoringCard.CreditScoringCardValidationPlugin", "mcs_credit_scoringcard", "Create", "20", $null),
    @("SanyD365.D365Extension.Sales.Plugins.ScoringCard.CreditScoringCardValidationPlugin", "mcs_credit_scoringcard", "Update", "20", $null)
)

Set-Location $toolPath

$first = $true
foreach ($step in $steps) {
    $class = $step[0]
    $entity = $step[1]
    $msg = $step[2]
    $stage = $step[3]
    $filter = $step[4]
    
    Write-Host "`n=== 注册: $class ===" -ForegroundColor Cyan
    
    if ($first) {
        # 第一个用 register-plugin-advanced（上传DLL）
        if ($filter) {
            dotnet run register-plugin-advanced "$dllPath" "$class" "$entity" "$msg" "$stage" "$filter"
        } else {
            dotnet run register-plugin-advanced "$dllPath" "$class" "$entity" "$msg" "$stage"
        }
        $first = $false
    } else {
        # 剩余用 register-step-only（跳过DLL上传）
        if ($filter) {
            dotnet run register-step-only "$class" "$entity" "$msg" "$stage" "$filter"
        } else {
            dotnet run register-step-only "$class" "$entity" "$msg" "$stage"
        }
    }
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "注册失败: $class" -ForegroundColor Red
    }
}

Write-Host "`n全部注册完成!" -ForegroundColor Green
```

---

## 5. 常见坑点

### 5.1 实体名必须用 Schema Name

| 错误写法 | 正确写法 |
|---|---|
| `mcs_scoring_card` | `mcs_credit_scoringcard` |
| `mcs_credit_itemvalue` | `mcs_credititem_value` |
| `mcs_credit_item_value` | `mcs_credititem_value` |

**查实体 Schema Name：** 设置 → 自定义项 → 实体 → 查看字段的"名称"列

### 5.2 阶段数值

| 文本 | 数值 |
|---|---|
| PreValidation | 10 |
| PreOperation | 20 |
| PostOperation | 40 |

### 5.3 筛选属性格式

- 单个字段：`mcs_status`
- 多个字段：`mcs_status,mcs_bppstatus`
- 无筛选：不传或传 `$null`

### 5.4 Depth 限制

某些 Plugin 需防止递归（如 Update 触发 Update）：

```csharp
if (context.Depth > 2)  // 或 > 3，视业务而定
{
    tracer?.Trace("深度超过限制，跳过");
    return;
}
```

### 5.5 连接字符串超时

大 DLL 上传时，在 MetadataTool 连接字符串中加入：

```
MaxConnectionTimeout=00:10:00;
```

### 5.6 Device Code Flow 超时

MetadataTool 默认使用 Device Code Flow 时，`ServiceClient` 会落到 SDK 默认 4 分钟超时。发布大 DLL 前，必须在 `Code/Tools/D365ToolCommon/Connection/D365ConnectionFactory.cs` 中显式设置：

```csharp
ServiceClient.MaxConnectionTimeout = TimeSpan.FromMinutes(10);
```

设置后重新编译 MetadataTool：

```bash
cd Code/Tools/MetadataTool
dotnet build -c Release
```

---

## 6. 大 DLL 专用发布流程

当 DLL 超过 5MB 时，推荐使用 `update-assembly` + `register-step-only` 分离流程，避免反复上传大文件。

### 6.1 第一步：单独更新 Assembly

```bash
cd Code/Tools/MetadataTool
dotnet bin/Release/net10.0/D365MetadataTool.dll update-assembly <DLL路径>
```

此命令只更新 `pluginassembly.content`，不上传 Step。

### 6.2 第二步：批量注册 Step

- 第一个 Step 用 `register-plugin-advanced`（创建 Plugin Type）
- 后续 Step 用 `register-step-only`（不再上传 DLL）

```bash
# 创建 Type 并注册第一个 Step
dotnet bin/Release/net10.0/D365MetadataTool.dll register-plugin-advanced \
  <DLL路径> <完整类名> <实体> <消息> <阶段> [筛选属性]

# 注册剩余 Step
dotnet bin/Release/net10.0/D365MetadataTool.dll register-step-only \
  <完整类名> <实体> <消息> <阶段> [筛选属性]
```

### 6.3 失败处理

| 现象 | 处理 |
|---|---|
| `请求通道在 00:04:00 以后尝试发送超时` | 检查 `MaxConnectionTimeout` 是否已设为 10 分钟 |
| `An error occurred while sending the request` | 等待 1-2 分钟重试；确认无人执行 Publish All |
| 操作卡住超过 5 分钟 | 可能 D365 正在 Publish All，等待完成后再试 |

详细操作手册见：
`Documents/DevelopmentStandards/D365-大DLL插件发布操作手册.md`

---

## 7. 清理旧 Assembly

迁移/合并 Assembly 时，先删除旧 Assembly：

```bash
dotnet run unregister-assembly "旧Assembly名称"
```

**注意：** 此命令会级联删除 Assembly 下的所有 Plugin Type 和 Step，**务必确认无其他系统依赖**。

---

## 8. 完整流程清单（Checklist）

```
□ 1. 合并代码到统一项目，编译通过（0 Error）
□ 2. 确认目标框架 ≤ v4.6.2（D365 Sandbox 限制）
□ 3. 确认所有 Plugin 类都在同一个 Assembly 中
□ 4. 备份/记录旧 Assembly 名称和 Step 配置
□ 5. 执行 unregister-assembly 清理旧 Assembly
□ 6. 执行 register-plugin-advanced × 1（上传 DLL + 注册第一个 Step）
□ 7. 执行 register-step-only × N（批量注册剩余 Step）
□ 8. PRT 中验证所有 Step 状态正常（无红色错误图标）
□ 9. 业务场景测试验证
□ 10. 将 Assembly + Steps 加入 Solution 准备导出
```

---

## 9. 相关文件位置

| 文件 | 路径 |
|---|---|
| MetadataTool | `d:\AIWork\Code\Tools\MetadataTool\` |
| 批量注册脚本 | `d:\AIWork\Code\Tools\MetadataTool\register-credit-plugins.ps1` |
| 统一 Plugin 项目 | `SanyD365.D365Extension.Sales.csproj` |
| DLL 输出 | `bin\Debug\SanyD365.D365Extension.Sales.dll` |
