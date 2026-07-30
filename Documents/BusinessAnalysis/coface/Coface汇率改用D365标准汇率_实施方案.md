# Coface 汇率改用 D365 标准汇率实施方案

> **版本**：v1.0  
> **日期**：2026-06-21  
> **状态**：待用户审批  
> **前提**：生产环境 D365 `transactioncurrency` 汇率数据已按统一规则维护，质量可信。

---

## 1. 变更范围

| # | 文件 | 变更内容 | 影响面 |
|---|---|---|---|
| 1 | `Code/Customizations/Plugins/CofaceIntegration/CofaceExchangeRateHelper.cs` | 重写汇率读取逻辑：优先从 D365 `transactioncurrency` 读取，取倒数后返回 `1 LC → USD` | Coface 全量报告/URBA360 货币转换 |
| 2 | `Code/Customizations/Plugins/CofaceIntegration/Parser/FullReportParser.cs` | 仅更新注释，无逻辑改动 | 文档一致性 |
| 3 | `Code/Customizations/Plugins/CofaceIntegration/Parser/Urba360Parser.cs` | 仅更新注释，无逻辑改动 | 文档一致性 |
| 4 | `mcs_coface_exchange_rate` 实体数据 | 可选保留，作为备份/审计 | 数据层 |

**不变更**：
- 不删除 `mcs_coface_exchange_rate` 实体及历史数据。
- 不修改 `ConvertCurrency` 调用方代码（因为 helper 返回的仍是 `1 LC → USD`）。

---

## 2. 核心设计

### 2.1 汇率方向处理

D365 `transactioncurrency.exchangerate` 的含义：

```
1 Base Currency (USD) = exchangerate Local Currency
```

Coface 调用方期望 helper 返回：

```
1 Local Currency = rate USD
```

因此 helper 内部转换：

```csharp
var rateToUsd = 1m / d365ExchangeRate;
```

### 2.2 USD 特殊处理

D365 中 USD 作为基础货币，其 `exchangerate` 固定为 `1.0`。helper 直接返回 `1m`，避免无意义查询。

### 2.3 除零与异常保护

- D365 汇率 ≤ 0 时，返回 `0m`，调用方保持原金额不变。
- `transactioncurrency` 中未找到币种时，返回 `0m`。
- 查询异常时记录 trace 并返回 `0m`，不影响主流程。

### 2.4 精度处理

D365 `exchangerate` 字段精度为 10 位小数，`decimal` 运算不会产生 double 精度问题。最终返回的 `rateToUsd` 保持原始精度，由调用方 `amount * rate` 决定结果精度。

---

## 3. 推荐方案 A：直接替换（默认走 D365）

### 3.1 修改后代码

```csharp
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanyD365.Plugins.CofaceIntegration
{
    /// <summary>
    /// Coface 汇率配置读取帮助类
    /// 从 D365 transactioncurrency 实体读取标准汇率，并转换为 1 LC => USD 方向返回
    /// </summary>
    public static class CofaceExchangeRateHelper
    {
        // 原自定义汇率实体字段常量保留，便于未来回滚或审计
        public const string LegacyEntityName = "mcs_coface_exchange_rate";
        public const string CurrencyCodeField = "mcs_currencycode";
        public const string CurrencyNameField = "mcs_currencyname";
        public const string RateToUsdField = "mcs_rate_to_usd";
        public const string EffectiveDateField = "mcs_effectivedate";
        public const string IsActiveField = "mcs_isactive";
        public const string RemarkField = "mcs_remark";

        /// <summary>
        /// 获取 1 LC => USD 的汇率
        /// 优先从 D365 transactioncurrency 读取标准汇率，并取倒数转换方向
        /// </summary>
        /// <param name="service">组织服务</param>
        /// <param name="tracer">跟踪服务</param>
        /// <param name="currencyCode">ISO 货币代码，如 EUR/CNY</param>
        /// <returns>汇率值（1 LC => USD）；未配置或异常时返回 0</returns>
        public static decimal GetRateToUsd(
            IOrganizationService service,
            ITracingService tracer,
            string currencyCode)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
            {
                tracer.Trace("货币代码为空，保持原值");
                return 0m;
            }

            if (currencyCode.Equals("USD", StringComparison.OrdinalIgnoreCase))
            {
                return 1m;
            }

            if (service == null)
            {
                tracer.Trace("IOrganizationService 为空，无法查询 D365 标准汇率");
                return 0m;
            }

            var normalizedCode = currencyCode.ToUpperInvariant();

            try
            {
                var query = new QueryExpression("transactioncurrency")
                {
                    ColumnSet = new ColumnSet("isocurrencycode", "currencyname", "exchangerate"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("isocurrencycode", ConditionOperator.Equal, normalizedCode)
                        }
                    },
                    TopCount = 1
                };

                var records = service.RetrieveMultiple(query);
                var record = records.Entities.FirstOrDefault();

                if (record == null)
                {
                    tracer.Trace($"D365 标准汇率未配置: {normalizedCode}，保持原值");
                    return 0m;
                }

                var d365Rate = record.GetAttributeValue<decimal>("exchangerate");

                if (d365Rate <= 0m)
                {
                    tracer.Trace($"D365 标准汇率值无效: {normalizedCode}={d365Rate}，保持原值");
                    return 0m;
                }

                // D365 汇率方向：1 USD => LC
                // Coface 需要方向：1 LC => USD
                var rateToUsd = 1m / d365Rate;

                tracer.Trace($"D365 标准汇率: {normalizedCode} => USD, d365Rate={d365Rate}, rateToUsd={rateToUsd}");

                return rateToUsd;
            }
            catch (Exception ex)
            {
                tracer.Trace($"查询 D365 标准汇率异常 [{normalizedCode}]: {ex.Message}");
                return 0m;
            }
        }

        /// <summary>
        /// 加载所有可用的 D365 标准汇率（按货币代码，转换为 1 LC => USD 方向）
        /// </summary>
        public static Dictionary<string, decimal> LoadAllRates(
            IOrganizationService service,
            ITracingService tracer)
        {
            var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var query = new QueryExpression("transactioncurrency")
                {
                    ColumnSet = new ColumnSet("isocurrencycode", "exchangerate"),
                    Orders =
                    {
                        new OrderExpression("isocurrencycode", OrderType.Ascending)
                    }
                };

                var records = service.RetrieveMultiple(query);
                foreach (var record in records.Entities)
                {
                    var code = record.GetAttributeValue<string>("isocurrencycode");
                    if (string.IsNullOrWhiteSpace(code) || rates.ContainsKey(code))
                        continue;

                    var d365Rate = record.GetAttributeValue<decimal>("exchangerate");
                    if (d365Rate > 0m)
                    {
                        rates[code.ToUpperInvariant()] = 1m / d365Rate;
                    }
                }

                tracer.Trace($"加载 D365 标准汇率: {rates.Count} 条");
            }
            catch (Exception ex)
            {
                tracer.Trace($"加载 D365 标准汇率异常: {ex.Message}");
            }

            return rates;
        }
    }
}
```

### 3.2 Parser 注释更新

`FullReportParser.cs` 和 `Urba360Parser.cs` 中 `ConvertCurrency` 方法的注释从：

```csharp
/// 汇率来源: D365 mcs_coface_exchange_rate 配置表（Coface 年度预算汇率）
```

更新为：

```csharp
/// 汇率来源: D365 transactioncurrency 标准汇率（1 LC => USD，由 CofaceExchangeRateHelper 转换）
```

---

## 4. 备选方案 B：配置开关（双轨读取）

若希望保留回滚能力，可在 helper 中增加一个配置开关，根据环境或组织设置决定走 D365 还是原自定义表。

### 4.1 配置项设计

新增环境配置实体/字段（推荐复用现有配置实体，如 `mcs_system_config`）：

| 配置键 | 示例值 | 说明 |
|---|---|---|
| `Coface.UseD365ExchangeRate` | `true` / `false` | `true` 走 D365 标准汇率，`false` 走 `mcs_coface_exchange_rate` |

### 4.2 helper 读取配置伪代码

```csharp
private static bool UseD365ExchangeRate(IOrganizationService service)
{
    // 示例：从 mcs_system_config 读取配置
    var query = new QueryExpression("mcs_system_config")
    {
        ColumnSet = new ColumnSet("mcs_value"),
        Criteria = new FilterExpression
        {
            Conditions =
            {
                new ConditionExpression("mcs_key", ConditionOperator.Equal, "Coface.UseD365ExchangeRate")
            }
        },
        TopCount = 1
    };
    var record = service.RetrieveMultiple(query).Entities.FirstOrDefault();
    if (record == null) return false;
    return string.Equals(record.GetAttributeValue<string>("mcs_value"), "true", StringComparison.OrdinalIgnoreCase);
}
```

### 4.3 GetRateToUsd 入口调整

```csharp
public static decimal GetRateToUsd(IOrganizationService service, ITracingService tracer, string currencyCode)
{
    if (UseD365ExchangeRate(service))
    {
        return GetRateToUsdFromD365(service, tracer, currencyCode);
    }
    return GetRateToUsdFromLegacy(service, tracer, currencyCode); // 保留原逻辑
}
```

**推荐**：若业务确认长期使用 D365 汇率，采用方案 A（更简洁）；若希望保留切换能力，采用方案 B。

---

## 5. 部署步骤

1. **本地修改**
   - 修改 `CofaceExchangeRateHelper.cs`。
   - 更新 `FullReportParser.cs` 和 `Urba360Parser.cs` 注释。

2. **本地编译验证**
   ```bash
   cd Code/Customizations/Plugins/CofaceIntegration
   dotnet build
   ```

3. **同步到远程主项目**（按现有流程）
   ```bash
   python3 Code/Tools/sync-plugin-to-remote.py --dry-run
   python3 Code/Tools/sync-plugin-to-remote.py --pull-dll Code/Customizations/Plugins/CofaceIntegration/SanyD365.D365Extension.Sales.dll
   ```

4. **注册 Plugin**
   - 在远程 `tx-windows` 服务器上重新编译 `SanyD365.D365Extension.Sales`。
   - 使用 Plugin Registration Tool 更新 `CofaceIntegration` 程序集。

5. **环境验证**
   - 在 DEV1 触发一条 Coface 报告同步，检查 `tracer` 日志中汇率读取是否正确。
   - 抽样核对几个币种（EUR/CNY/JPY）转换后的 USD 金额是否符合预期。

6. **发布到 UAT / PRD**
   - 按常规发布流程（Solution 导出导入 + Plugin 注册）。

---

## 6. 回滚策略

- **代码回滚**：Git 回退 `CofaceExchangeRateHelper.cs` 到上一版本即可。
- **数据回滚**：`mcs_coface_exchange_rate` 实体及数据保留不动，无需回滚数据。
- **快速切换**：若采用方案 B（配置开关），仅需修改配置值为 `false`，无需重新部署代码。

---

## 7. 测试要点

| # | 测试场景 | 预期结果 |
|---|---|---|
| 1 | USD 币种金额转换 | 金额不变 |
| 2 | CNY 金额转换 | `USD 金额 = CNY 金额 / D365_CNY_ExchangeRate` |
| 3 | EUR 金额转换 | `USD 金额 = EUR 金额 / D365_EUR_ExchangeRate` |
| 4 | 未配置币种 | helper 返回 0，调用方保持原金额，trace 记录警告 |
| 5 | D365 汇率为 0 或负数 | helper 返回 0，不抛异常 |
| 6 | 查询异常（权限/网络） | helper 返回 0，主流程不受影响 |

---

## 8. 决策建议

- **如果确定长期使用 D365 标准汇率**：采用方案 A，直接替换，代码最简洁。
- **如果希望保留原有 2026 Budget 汇率作为备份/切换能力**：采用方案 B，增加 `Coface.UseD365ExchangeRate` 配置开关。

请确认采用哪个方案，以及是否需要我立即按方案修改本地代码。
