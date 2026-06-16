# Coface 财务指标对照逻辑说明

> **文档定位**：说明 D365 中 `mcs_coface_financial_indicator` 实体的作用、Excel 数据源如何映射到该实体、以及 `Urba360Parser` 如何在 Coface 数据集成时使用这些配置。
> **适用人员**：后续维护 Coface 数据集成、更新财务指标配置的开发/运维人员。
> **最后更新**：2026-06-15（对应客户 Excel `4国家财务指标-更新版0612.xlsx`）

---

## 1. 背景

Coface 财务指标对照表用于告诉系统：**从 Coface URBA360 / Report 返回的 JSON 中，每个国家、每个财务指标应该取哪个 `type.value`**。

覆盖的 4 个财务指标：

| 指标名 | 含义 | 在 Coface JSON 中的位置 |
|---|---|---|
| NetAssets | 净资产 | `productDetails.financials.balanceSheet.balanceSheetItems[].indicators[]` |
| DebtRatio | 资产负债率 | `productDetails.financials.ratios[]` |
| CurrentRatio | 流动比率 | `productDetails.financials.ratios[]` |
| NetProfitMargin | 净利润率 | `productDetails.financials.ratios[]` |

> 注：同一国家同一指标可能存在多个候选编码（例如 standalone / consolidated 两套报表），因此需要 `priority` 字段控制匹配顺序。

---

## 2. D365 实体结构

实体逻辑名：`mcs_coface_financial_indicator`

| 字段 | 类型 | 说明 |
|---|---|---|
| `mcs_countrycode` | 文本(10) | 国家代码，如 `AR`、`IN`、`MY` |
| `mcs_countryname` | 文本(100) | 国家名称，如 `ARGENTINA` |
| `mcs_indicatorname` | 文本(50) | 指标名：`NetAssets` / `DebtRatio` / `CurrentRatio` / `NetProfitMargin` |
| `mcs_typevalue` | 文本(50) | Coface JSON 中 `indicator.type.value` 或 `ratio.type.value` 的编码 |
| `mcs_indicatortype` | 选项集 | 取数位置：`1=资产负债表` / `2=比率` |
| `mcs_priority` | 整数 | 同一国家同一指标内的匹配优先级，数字越小越优先 |
| `mcs_formulafallback` | Memo | 兜底计算公式说明（仅文档用途，当前代码不执行） |
| `mcs_isactive` | 布尔 | 是否有效，导入时统一为 `true` |

---

## 3. 数据源：Excel 格式与解析规则

### 3.1 Excel 来源

客户提供的原始文件：

```
Documents/BusinessAnalysis/coface/10Coface数据字典补充/4国家财务指标-更新版0612.xlsx
```

Excel 列结构：

| 列 | 字段 | 示例 |
|---|---|---|
| A | Country Code | `AR` |
| B | ISO-3 | `ARG` |
| C | Country | `ARGENTINA` |
| D | Net Assets | `indicator(1)-indicator(9) OR indicator(21)` |
| E | formula | `total assets - total liabilities OR Equity` |
| F | Debt Ratio | `1034` |
| G | formula | `indicator(9)/indicator(1)` |
| H | Current Ratio | `1001` |
| I | formula | `indicator(3)/indicator(260)` |
| J | Net Profit Margin | `1038` |
| K | formula | `indicator(620)/indicator(7)` |

### 3.2 解析规则

Excel 会被转换成 `Code/Tools/CofaceConfigImporter/coface_financial_indicators.json`，再由导入工具写入 D365。

#### NetAssets（资产负债表，indicatorType = 1）

- Excel 中的公式通常形如：`indicator(A)-indicator(B) OR indicator(C)`
- 代码目前**只匹配单个 `type.value`**，因此提取 `OR` 后面的直接编码作为匹配键
- 如果存在多行（standalone / consolidated），分别生成记录，standalone 优先

**示例**：

```text
Excel: indicator(1)-indicator(9) OR indicator(21)
→ JSON: NetAssets / typeValue=21 / priority=1

Excel: indicator(37563)-[indicator(37573)+indicator(37576)] OR indicator(37560) for standalone
       indicator(37619)-[indicator(37629)+indicator(37632)] OR indicator(37616) for consolidated
→ JSON: NetAssets / 37560 / priority=1 (standalone)
        NetAssets / 37616 / priority=2 (consolidated)
```

#### DebtRatio / CurrentRatio / NetProfitMargin（比率，indicatorType = 2）

- 如果单元格是纯数字（如 `1034`），直接作为 `typeValue`
- 如果单元格是公式，取第一个出现的 `indicator(N)` 编码作为 `typeValue`
- 如果存在 standalone / consolidated 多行，分别生成记录

**示例**：

```text
Excel: 1034
→ JSON: DebtRatio / typeValue=1034 / priority=1

Excel: [indicator(16102) + indicator(16132)]/indicator(16066) for standalone
       [indicator(16283) + indicator(16313)]/indicator(16247) for consolidated
→ JSON: DebtRatio / 16102 / priority=1 (standalone)
        DebtRatio / 16283 / priority=2 (consolidated)

Excel: 16370 standalone
       16387 consolidated
→ JSON: CurrentRatio / 16370 / priority=1 (standalone)
        CurrentRatio / 16387 / priority=2 (consolidated)
```

#### 关于 formula 字段

- Excel 的 formula 列会写入 `mcs_formulafallback`
- **当前代码不解析、不执行这些公式**，仅作为业务文档保留
- 系统实际依赖的是 `mcs_typevalue` 与 Coface JSON 中的 `type.value` 匹配

### 3.3 N/A 处理

Excel 中标记为 `N/A` 的指标不生成记录。本次 0612 版本共 115 行国家数据，其中 87 个国家至少有一个有效指标，最终生成 **429 条记录**。

---

## 4. 代码读取逻辑

### 4.1 入口

文件：`Code/Customizations/Plugins/CofaceIntegration/Parser/Urba360Parser.cs`

构造时按国家加载配置：

```csharp
public Urba360Parser(ITracingService tracer, IOrganizationService service, string countryCode)
{
    _service = service;
    _countryCode = countryCode;
    _indicatorConfigs = LoadIndicatorConfigs(countryCode);
}
```

### 4.2 配置加载

```csharp
var query = new QueryExpression("mcs_coface_financial_indicator")
{
    ColumnSet = new ColumnSet("mcs_countrycode", "mcs_indicatorname", "mcs_typevalue",
                              "mcs_indicatortype", "mcs_priority", "mcs_formulafallback", "mcs_isactive"),
    Criteria = new FilterExpression
    {
        Conditions =
        {
            new ConditionExpression("mcs_countrycode", ConditionOperator.Equal, countryCode),
            new ConditionExpression("mcs_isactive", ConditionOperator.Equal, true)
        }
    },
    Orders =
    {
        new OrderExpression("mcs_indicatorname", OrderType.Ascending),
        new OrderExpression("mcs_priority", OrderType.Ascending)
    }
};
```

按 `mcs_indicatorname` 分组，同一指标内的配置按 `mcs_priority` 排序。

### 4.3 指标解析流程

#### NetAssets

```csharp
var configs = GetIndicatorConfigs("NetAssets");
foreach (var config in configs)
{
    decimal? value = FindBalanceSheetIndicatorValue(items, config.TypeValue);
    if (value.HasValue) return value.Value;
}
// 无配置或匹配失败时，回退到默认名称匹配
return ParseNetAssetsByDefaultNames(items);
```

`FindBalanceSheetIndicatorValue` 在 `balanceSheetItems[].indicators[]` 中匹配 `type.value`，并做单位换算和货币转换。

#### DebtRatio / CurrentRatio / NetProfitMargin

```csharp
var configs = GetIndicatorConfigs(indicatorName);
foreach (var config in configs)
{
    decimal? value = FindRatioValue(ratios, config.TypeValue);
    if (value.HasValue) return value.Value;
}
// 无配置或匹配失败时，回退到默认名称匹配
return ParseFinancialRatioByDefaultName(ratios, indicatorName);
```

`FindRatioValue` 在 `ratios[]` 中匹配 `type.value`。

### 4.4 默认名称回退

如果配置表未命中，代码会尝试按指标英文名匹配：

- NetAssets：`Net Assets` / `Net Worth` / `Equity` / `Own funds`
- DebtRatio：`Debt ratio`
- CurrentRatio：`Current Ratio`
- NetProfitMargin：`Net Profit Margin` / `ROS`

---

## 5. 当前限制

1. **公式不自动计算**
   - `mcs_formulafallback` 只是文本备注
   - 例如 `indicator(1)-indicator(9)` 这种“总资产 - 总负债”的表达式，当前代码不会自动计算
   - 依赖 Coface 返回中是否存在可直接匹配的单一编码（如 `indicator(21)` 代表 Equity）

2. **多编码优先级由 Excel 顺序决定**
   - standalone 行在前则 priority 小
   - 同一行内多个 `indicator(N)` 只取规则约定的那一个

3. **货币转换依赖汇率配置表**
   - `ConvertCurrency` 会读取 `mcs_coface_exchange_rate` 配置表
   - 若汇率未配置，保持原币值

---

## 6. 导入工具使用说明

### 6.1 工具位置

```
Code/Tools/CofaceConfigImporter/
```

### 6.2 数据文件

- 正式导入文件：`coface_financial_indicators.json`
- 历史备份：`coface_financial_indicators_backup_YYYYMMDD.json`
- 解析脚本生成的临时文件：`coface_financial_indicators_0612.json`

### 6.3 常用命令

```bash
# 进入工具目录
cd Code/Tools/CofaceConfigImporter

# 全量刷新（删除旧数据后重新导入，推荐用于完整更新）
dotnet run -- coface_financial_indicators.json --clean

# 增量导入（已存在的 countryCode+indicatorName+typeValue 会跳过）
dotnet run -- coface_financial_indicators.json
```

### 6.4 切换环境

```bash
# DEV1（默认）
export D365_URL="https://dev1.crm5.dynamics.com"

# UAT
export D365_URL="https://sany-uat.crm5.dynamics.com"
```

无 ClientSecret 时默认走 Device Code Flow，首次需浏览器登录。

### 6.5 测试 Coface 数据集成

`CofaceConfigImporter` 内置了测试命令，用于触发指定 `mcs_credit_record` 的 Coface 数据集成并查看结果。

```bash
# 列出候选记录（状态≠11，且已有 cofaceId 和 countryCode）
dotnet run test-coface list

# 触发指定记录的数据集成
dotnet run test-coface trigger <mcs_credit_record ID>
```

测试命令会输出：
- 评估记录的 `mcs_api_status`、`mcs_api_msg`
- 生成的 `mcs_customer_tag` 标签列表
- 最近 3 条 `CofaceDataSyncPlugin` 的 Plugin Trace

**2026-06-15 DEV1 测试结果**：
- 测试记录：`SCO202606110001`，国家 `PL`，CofaceID `icon#5415240`
- `mcs_api_status` = `SUCCESS`
- 成功加载 PL 配置 4 条：`CurrentRatio=181`、`DebtRatio=190`、`NetAssets=2375`、`NetProfitMargin=183`
- 成功生成 7 个客户信用标签，其中 `净资产` 解析为 `11361465.84`
- `资产负债率` 和 `流动比率` 返回 `N/A`（Coface 返回中未匹配到对应 `type.value`）

---

## 7. 更新记录

| 日期 | 更新内容 | 操作人员 |
|---|---|---|
| 2026-06-12 | 初始创建，基于 `coface_financial_indicators.json`，覆盖 36 国 193 条记录 | — |
| 2026-06-15 | 按客户 `4国家财务指标-更新版0612.xlsx` 全量刷新为 87 国 429 条记录，DEV1 已导入 | Peter |
| 2026-06-15 | DEV1 触发 PL 国家测试记录，CofaceDataSync 成功，生成 7 个标签 | Peter |
| 2026-06-15 | UAT 环境同步相同配置数据（87 国 429 条记录） | Peter |

---

## 8. 后续维护检查清单

- [ ] 客户再次更新 Excel 时，先备份当前的 `coface_financial_indicators.json`
- [ ] 检查 Excel 是否有新增列或格式变化（如 standalone/consolidated 写法变更）
- [ ] 重新运行解析脚本生成 JSON，并抽样核对关键国家（如 CN、AE、AR、IN）
- [ ] 先在 DEV1 执行 `--clean` 导入并验证
- [ ] DEV1 验证通过后，再同步到 UAT
- [ ] 更新本文档“更新记录”章节
