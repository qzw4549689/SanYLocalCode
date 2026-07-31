# 厂端授信余额调整 API 调用说明

> **接口形态**：D365 Custom API（D365 内部接口）  
> **Custom API 唯一名**：`mcs_AdjustFcaQuotaBalance`  
> **部署环境**：DEV1 / UAT / PROD  
> **实施方案**：`Documents/Planning/厂端授信余额调整接口_实施方案.md`  
> **最后更新**：2026-07-22

---

## 1. 接口概述

本接口是厂端授信余额的**统一调整接口**，供合同评审、订单发货/取消/退货、回款解款等各业务环节调用，统一处理厂端授信余额的**初始化 / 占用 / 释放**，并自动写入厂端授信额度动态调整管理台账（`mcs_fca_records`）。

核心不变式：**授信额度 = 授信余额 + 占用金额**（`mcs_sellergrant = mcs_sellerbalance + mcs_usedsellerbalance`）。

调用方应为 D365 内部模块（Plugin、JS、Power Automate、其他 Custom API 等）。

---

## 2. 调用方式

### 2.1 C# / Plugin 中调用

```csharp
var request = new OrganizationRequest("mcs_AdjustFcaQuotaBalance");
request["mcs_accountid"] = "0210000680";   // 客户编码（SAP客户代码）
request["mcs_usebalance"] = 30000m;        // 调整金额USD
request["mcs_proccess"] = "6";             // 流程环节：6 订单执行验收（发货）
request["mcs_adjust"] = "3";               // 调整动作：3 占用
request["mcs_contractid"] = "TEST0626PM1710";
request["mcs_orderid"] = "SID202605270001";

var response = service.Execute(request);

var usedFlag = response["mcs_usedflag"]?.ToString();       // 1 成功 / 0 失败
var usedBalance = response["mcs_usedbalance"];             // 实际调整金额
var sellerBalance = response["mcs_sellerbalance"];         // 调整后余额
var recordId = response["mcs_recordid"]?.ToString();       // 台账编号
var failReason = response["mcs_failreason"]?.ToString();   // 失败原因
```

### 2.2 JavaScript / WebResource 中调用

```javascript
var request = {
    mcs_accountid: "0210000680",
    mcs_usebalance: 30000,
    mcs_proccess: "6",
    mcs_adjust: "3",
    mcs_contractid: "TEST0626PM1710",
    mcs_orderid: "SID202605270001",
    getMetadata: function () {
        return {
            boundParameter: null,
            parameterTypes: {
                mcs_accountid: { typeName: "Edm.String", structuralProperty: 1 },
                mcs_usebalance: { typeName: "Edm.Decimal", structuralProperty: 1 },
                mcs_proccess: { typeName: "Edm.String", structuralProperty: 1 },
                mcs_adjust: { typeName: "Edm.String", structuralProperty: 1 },
                mcs_contractid: { typeName: "Edm.String", structuralProperty: 1 },
                mcs_orderid: { typeName: "Edm.String", structuralProperty: 1 }
            },
            operationType: 0,
            operationName: "mcs_AdjustFcaQuotaBalance"
        };
    }
};

Xrm.WebApi.online.execute(request).then(
    function (result) {
        result.json().then(function (response) {
            console.log("usedflag:", response.mcs_usedflag);
            console.log("sellerbalance:", response.mcs_sellerbalance);
            console.log("recordid:", response.mcs_recordid);
            console.log("failreason:", response.mcs_failreason);
        });
    },
    function (error) {
        console.error(error.message);
    }
);
```

> JS 端调用需确保 Custom API 已发布且当前用户有执行权限。

---

## 3. 请求参数

| 参数名 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `mcs_accountid` | String | 是 | 客户编码（SAP客户代码），按客户主数据 `mcs_sapnumber` 匹配 |
| `mcs_usebalance` | Decimal | 是 | 调整厂端授信金额USD（≥0，调用方负责汇率换算）；**初始化动作时表示新的授信额度** |
| `mcs_proccess` | String | 是 | 流程环节编码，见 §7.1（以台账表 1-11 口径为准） |
| `mcs_adjust` | String | 是 | 额度调整动作：`1`=初始化、`3`=占用、`4`=释放 |
| `mcs_contractid` | String | 条件必填 | 合同编码（`mcs_contract.mcs_name`）；环节 ≥3 时必填 |
| `mcs_orderid` | String | 条件必填 | 订单编码（`mcs_order.mcs_name`）；环节 6-10 时必填 |

### 3.1 动作-环节交叉校验

| 动作 | 允许的流程环节 |
|---|---|
| `1` 初始化 | 1（厂端授信模型计算）、2（厂端授信限额额度申请） |
| `3` 占用 | 6（订单执行验收/发货） |
| `4` 释放 | 5（合同取消）、7（订单变更信用类别）、8（回款解款）、9（订单退货）、10（订单取消） |

---

## 4. 响应参数

| 参数名 | 类型 | 说明 |
|---|---|---|
| `mcs_accountid` / `mcs_usebalance` / `mcs_proccess` / `mcs_adjust` / `mcs_contractid` / `mcs_orderid` | — | 请求参数原样回显 |
| `mcs_usedbalance` | Decimal | 实际调整厂端授信余额USD，失败时 0.00 |
| `mcs_sellerbalance` | Decimal | 调整后厂端授信余额USD（允许负数，表示超额） |
| `mcs_usedflag` | String | `1` 成功 / `0` 失败 |
| `mcs_recordid` | String | 台账编号（`FCR+YYYYMMDD+5位序列号`，成功时填入） |
| `mcs_failreason` | String | 使用失败原因（失败时填入） |

---

## 5. 业务规则

### 5.1 初始化（mcs_adjust=1）

- 额度记录不存在 → 新建：余额 = 额度、占用 = 0
- 额度记录已存在 → 更新：**占用不清零**，余额 = 新额度 − 现有占用；同时更新【是否生效=是】【生效日期=当日】
- 台账：调整金额 = 0

### 5.2 占用（mcs_adjust=3）

- 额度记录必须存在且已生效，否则失败
- **重复性校验**：按「订单编号优先、为空用合同编号」+ 客户编码查台账最新记录，最新动作已是占用 → 判定重复，返回失败：`对应合同已执行占用，无法再次执行动作占用`
- 更新额度表：余额 −= 金额、占用 += 金额；**余额不足不拦截（允许负余额表示超额）**
- 写台账：调整前余额、调整金额、调整后余额

### 5.3 释放（mcs_adjust=4）

- 额度记录必须存在且已生效，否则失败
- 更新额度表：余额 += 金额、占用 −= 金额；**不做防重复校验**
- 写台账

### 5.4 数据一致性

接口内部对"更新额度 + 写台账"做了补偿回滚：任一步骤失败时自动恢复额度记录原值（或删除新建额度），保证额度表与台账不产生半提交状态。

---

## 6. 错误处理

| mcs_usedflag | mcs_failreason | 场景 |
|---|---|---|
| `0` | 客户编码不能为空 | 缺少 `mcs_accountid` |
| `0` | 调整厂端授信金额USD必须大于等于0 | 金额为负 |
| `0` | 流程环节取值非法 / 额度调整动作取值非法 | 取值超出范围 |
| `0` | 初始化动作仅限流程环节 1/2 使用 等 | 动作-环节交叉校验不通过 |
| `0` | 流程环节 X 必须传入合同编码/订单编码 | 条件必填缺失 |
| `0` | 未找到客户编码[X]对应的客户主数据 | 客户不存在 |
| `0` | 未找到合同编码[X]对应的合同 / 未找到订单编码[X]对应的订单 | 合同/订单不存在 |
| `0` | 客户编码[X]的厂端授信额度不存在，无法执行占用/释放 | 无额度记录 |
| `0` | 客户厂端授信额度未生效，无法执行占用/释放 | 额度未生效 |
| `0` | 对应合同已执行占用，无法再次执行动作占用 | 重复占用 |
| `0` | 调整失败: ... | 执行异常（已补偿回滚） |
| `1` | 空字符串 | 成功 |

---

## 7. 附录

### 7.1 流程环节编码（以台账表 `mcs_fca_records.mcs_proccess` 1-11 口径为准）

| 值 | 环节 | 典型动作 |
|---|---|---|
| 1 | 厂端授信模型计算 | 初始化 |
| 2 | 厂端授信限额额度申请 | 初始化 |
| 3 | 提交合同评审 | 提示（不调本接口） |
| 4 | 合同变更信用类别 | 提示（不调本接口） |
| 5 | 合同取消 | 释放 |
| 6 | 订单执行验收（发货） | 占用 |
| 7 | 订单变更信用类别 | 释放 |
| 8 | 回款解款 | 释放 |
| 9 | 订单退货 | 释放 |
| 10 | 订单取消 | 释放 |
| 11 | 其他 | 保留 |

> 注：PRD 接口参数表中的 1-12 口径（含"3厂端授信限额归零调整"）已作废，以本表为准。

### 7.2 涉及解决方案

| 解决方案 | 内容 |
|---|---|
| `McsCustomAPI` | Custom API `mcs_AdjustFcaQuotaBalance`、请求参数、响应属性、实现 Assembly `SanyD365.D365ExtensionApi.Sales` |
| `McsPlugin` | 业务 Plugin Assembly `SanyD365.D365Extension.Sales`（FcaProcActivationPlugin 等口径修正） |
| `entity_XXXX` | `mcs_fca_quota.mcs_usedsellerbalance` 新字段 |

### 7.3 UAT/PROD 发布

通过 n8n Release Tool 发布时，需勾选：`McsCustomAPI`、`McsPlugin`、`McsWebResource`（含 `mcs_fca_quotaapp.js` 更新）、实体包（含新字段）。

---

## 8. 注意事项

1. **额度动态统一使用美元**，非美元金额由调用方按汇率换算后传入。
2. 占用超额时接口不拦截，余额记为负数（表示超额）；调用方如需风险提示，应自行判断返回的 `mcs_sellerbalance`。
3. 占用做防重复、释放不做防重复；同一订单"占用→释放→再占用"是允许的正常流程。
4. 历史数据初始化（功能上线前存量合同占用核算）属于上线前数据迁移任务，见实施方案 §5.6。
