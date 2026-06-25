# Coface 汇率改用 D365 标准汇率 — 方案 A 实施文档

> **方案名称**：方案 A — 直接替换  
> **日期**：2026-06-21  
> **状态**：待最终确认后实施  
> **适用范围**：Coface `FullReportParser` 与 `Urba360Parser` 中的货币转换逻辑  
> **前提**：生产环境 D365 `transactioncurrency` 汇率已按统一规则维护，质量可信。

---

## 1. 变更清单

| # | 文件路径 | 变更类型 | 说明 |
|---|---|---|---|
| 1 | `Code/Customizations/Plugins/CofaceIntegration/CofaceExchangeRateHelper.cs` | 修改 | 从 `mcs_coface_exchange_rate` 改为读取 `transactioncurrency`，并取倒数 |
| 2 | `Code/Customizations/Plugins/CofaceIntegration/Parser/FullReportParser.cs` | 注释更新 | 更新汇率来源注释 |
| 3 | `Code/Customizations/Plugins/CofaceIntegration/Parser/Urba360Parser.cs` | 注释更新 | 更新汇率来源注释 |

**不删除、不修改**：
- `mcs_coface_exchange_rate` 实体及历史数据保留，仅停止读取。
- `ConvertCurrency` 调用方代码不修改，因为 helper 返回格式仍为 `1 LC → USD`。

---

## 2. 汇率方向说明

D365 标准字段 `transactioncurrency.exchangerate` 的含义：

```
1 Base Currency (USD) = exchangerate Local Currency
```

Coface 调用方期望的汇率含义：

```
1 Local Currency = rate USD
```

因此 helper 内部需要取倒数：

```csharp
var rateToUsd = 1m / d365ExchangeRate;
```

调用方继续使用 `amount * rate` 完成转换，无需任何改动。

---

## 3. 修改后完整代码

### 3.1 CofaceExchangeRateHelper.cs

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
        /// 从 D365 transactioncurrency 读取标准汇率，并取倒数转换方向
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

### 3.2 FullReportParser.cs 注释更新

将 `ConvertCurrency` 方法上方的注释：

```csharp
/// <summary>
/// 货币转换（统一转USD）
/// 汇率来源: D365 mcs_coface_exchange_rate 配置表（Coface 年度预算汇率）
/// </summary>
```

替换为：

```csharp
/// <summary>
/// 货币转换（统一转USD）
/// 汇率来源: D365 transactioncurrency 标准汇率（1 LC => USD，由 CofaceExchangeRateHelper 转换方向）
/// </summary>
```

### 3.3 Urba360Parser.cs 注释更新

同上，更新 `ConvertCurrency` 方法注释。

---

## 4. 部署与验证计划

### 4.1 本地编译

```bash
cd Code/Customizations/Plugins/CofaceIntegration
dotnet build
```

### 4.2 同步到远程主项目

```bash
# 预览
python3 Code/Tools/sync-plugin-to-remote.py --dry-run

# 正式同步 + 编译 + 拉回 DLL
python3 Code/Tools/sync-plugin-to-remote.py --pull-dll Code/Customizations/Plugins/CofaceIntegration/SanyD365.D365Extension.Sales.dll
```

### 4.3 注册 Plugin

在远程 `tx-windows` 服务器上：
1. 重新编译 `SanyD365.D365Extension.Sales`。
2. 使用 Plugin Registration Tool 更新 `CofaceIntegration` 程序集。

### 4.4 环境验证

在 DEV1 触发一条 Coface 报告同步，检查 Plugin Trace Log 中是否出现如下记录：

```
D365 标准汇率: CNY => USD, d365Rate=7.0288, rateToUsd=0.142271...
货币转换: CNY => USD, 汇率=0.142271..., 原金额=..., 转换后=...
```

### 4.5 发布到 UAT / PRD

按常规 Solution 导入 + Plugin 注册流程执行。

---

## 5. 回滚方案

若上线后需要回滚：

1. **代码回滚**：Git 还原 `CofaceExchangeRateHelper.cs` 至修改前版本（读取 `mcs_coface_exchange_rate`）。
2. **数据回滚**：无需操作，`mcs_coface_exchange_rate` 数据保留未动。
3. **重新部署**：按 4.2 ~ 4.5 步骤重新编译、同步、注册。

---

## 6. 测试用例建议

| # | 场景 | 输入 | 预期 |
|---|---|---|---|
| 1 | USD 不转换 | `currency=USD, amount=1000` | 返回 1000 |
| 2 | 主流币种转换 | `currency=CNY, amount=1000` | 返回 `1000 / D365_CNY_Rate` |
| 3 | 欧洲币种转换 | `currency=EUR, amount=1000` | 返回 `1000 / D365_EUR_Rate` |
| 4 | 小币种转换 | `currency=VND, amount=1000000` | 返回 `1000000 / D365_VND_Rate` |
| 5 | 未配置币种 | `currency=XXX, amount=1000` | 保持原值 1000，trace 提示未配置 |
| 6 | 异常保护 | 查询 `transactioncurrency` 异常 | 保持原值，trace 提示异常信息 |

---

## 7. 决策确认

本方案待以下确认后实施：

- [ ] 确认采用方案 A（直接替换，不保留配置开关）。
- [ ] 确认生产环境 `transactioncurrency` 汇率已覆盖 Coface 所需全部币种。
- [ ] 确认生产环境汇率方向为 `1 USD = X LC`（即 USD 自身汇率为 1）。
- [ ] 确认允许停止读取 `mcs_coface_exchange_rate`，但保留该实体数据。

**请审批后告知，我将按本文档修改本地代码并进入编译验证阶段。**
