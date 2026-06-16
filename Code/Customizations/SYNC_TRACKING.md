# 代码同步跟踪

> 记录本机代码与远程服务器代码的同步状态
> 远程服务器：`tx-windows` (122.51.232.70)，`C:\Projects\D365\D365\SanyD365.D365Extension.Sales`
> Git 仓库：`https://dev.azure.com/SanyGlobalCRM/D365/_git/D365`，分支 `uat`

---

## 最新同步记录（2026-06-11）

### ✅ 已同步文件（本次修复）

| 本机文件 | 远程对应文件 | 修改内容 | Git 分支 |
|---------|-------------|---------|---------|
| `CofaceIntegration/Plugin/CofaceDataSyncPlugin.cs` | `Plugins/CofaceIntegration/CofaceIntegrationDataSyncPlugin.cs` | GetScoringCardItems: `record["mcs_itemid"]` → `GetAttributeValue<string>("mcs_itemid")` + fallback 从 `mcs_credititem` Lookup 获取 | `uat-260611-peter-coface-fix` |
| `CofaceIntegration/Api/CofaceApiService.cs` | `Application/Sales/CofaceIntegration/CofaceApiService.cs` | 去掉 `format=json` 参数（36国不支持） | `uat-260611-peter-coface-fix` |
| `CofaceIntegration/Parser/Urba360Parser.cs` | `Application/Sales/CofaceIntegration/Urba360Parser.cs` | 添加 `FillUrbaMissingValues` 方法 | `uat-260611-peter-coface-fix` |

### 远程编译状态
- ✅ 编译成功：`SanyD365.D365Extension.dll` 已生成
- ⚠️ 测试项目 `Secret.json` 缺失（不影响主 DLL）

### UAT 数据修复
- ✅ Category 5（个人客户）15 条评分卡配置已从 DEV1 同步到 UAT

---

## 历史同步记录

### ✅ 已同步（12 个文件，2026-06-11 之前）

CofaceApiService、CofaceTokenManager、CofaceDataSyncPlugin、BppCallbackPlugin、BppIntegrationPlugin、CreditRecordAutoNumberPlugin、CreditScore BpfStageSync/BpfSyncHelper、CreditItemsValidation、CreditItemValueValidation、CustomerTagInit/Validation.

### ⚠️ 结构差异需确认
- 本机 `ScoreCalculator.cs` + `CreditScorePlugin.cs` → 远程可能合并为 `CreditScoreCalculationPlugin.cs`
- 本机 `AccountValidationPlugin.cs` → 远程 `AccountCreditValidationPlugin.cs`（文件名不同）

---

## 同步流程

```
本机开发 → 改命名空间 → 复制到远程 → 远程编译(D365.sln, .NET 4.6.2) → Git提交(uat) → PR → Release Tool(n8n) → 部署
```
