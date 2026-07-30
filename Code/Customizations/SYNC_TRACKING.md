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

---

## 待同步-发布列表

> 以下功能已在本地开发并验证通过，但暂未推送到远程主项目/更新 DEV1 主 Assembly，将与其他功能一起批量发布。

| # | 功能 | 本地路径 | 远程目标 | 状态 | 计划发布批次 |
|---|------|---------|---------|------|------------|
| 1 | 厂端授信模型计算 Plugin（FcaProcCalculationPlugin） | `Code/Customizations/Plugins/FactoryCredit/Calculation/` | `SanyD365.D365Extension.Sales/Plugins/FactoryCredit/` | ✅ 本地编译通过；✅ DEV1 独立 Assembly 测试通过；✅ 临时 Assembly 已注销 | 待安排 |
| 2 | 信用画像页新增额度信息展示 | `Code/Customizations/WebResources/HTML/mcs_credit_profile.html` | 通过 Solution 发布 WebResource `mcs_credit_profile.html` | ✅ DEV1 已部署并验证；✅ 数据关系已确认；✅ 样式/文案已调整 | 待安排 |

**批次说明：**
- 厂端授信模型计算 Plugin 依赖 `mcs_fca_proc`、`mcs_fca_mdlconfig`、`mcs_fca_mdlversion`、`mcs_customermasterdata`、`mcs_customer_tag`、`mcs_outstanding` 等实体，需与相关实体/字段变更一并发布。
