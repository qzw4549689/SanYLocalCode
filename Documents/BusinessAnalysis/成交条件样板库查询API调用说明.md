# 成交条件样板库查询 API 调用说明

> **接口形态**：D365 Custom API（D365 内部接口）  
> **Custom API 唯一名**：`mcs_QueryTradeStPayTerm`  
> **部署环境**：DEV1 / UAT / PROD  
> **最后更新**：2026-06-23

---

## 1. 接口概述

本接口用于根据事业部、子公司、国家、产品线、客户编码查询匹配的**生效**成交条件样板库记录，返回首付比例、账期、付款频次等成交条件。

调用方应为 D365 内部模块（Plugin、JS、Power Automate、其他 Custom API 等）。

---

## 2. 调用方式

### 2.1 C# / Plugin 中调用

```csharp
var request = new OrganizationRequest("mcs_QueryTradeStPayTerm");
request["mcs_buid"] = "BU-1018";
request["mcs_subid"] = "SUB-001";
request["mcs_countrycode"] = "CN";
request["mcs_prdgroupid"] = "PM";
request["mcs_buyercode"] = "AID202507100000";

var response = service.Execute(request);

var status = response["status"]?.ToString();
var message = response["message"]?.ToString();
var records = response["records"]?.ToString(); // JSON 字符串
```

### 2.2 JavaScript / WebResource 中调用

```javascript
var request = {
    mcs_buid: "BU-1018",
    mcs_subid: "SUB-001",
    mcs_countrycode: "CN",
    mcs_prdgroupid: "PM",
    mcs_buyercode: "AID202507100000"
};

Xrm.WebApi.online.execute(request).then(
    function (result) {
        result.json().then(function (response) {
            console.log("status:", response.status);
            console.log("message:", response.message);
            console.log("records:", JSON.parse(response.records));
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
| `mcs_buid` | String | 是 | 事业部编码 |
| `mcs_subid` | String | 是 | 子公司编码 |
| `mcs_countrycode` | String | 是 | 国家代码 |
| `mcs_prdgroupid` | String | 是 | 产品线编码 |
| `mcs_buyercode` | String | 是 | 客户编码（客户主数据 `mcs_accountnumber`） |

---

## 4. 响应参数

| 参数名 | 类型 | 说明 |
|---|---|---|
| `status` | String | `1` 成功，`0` 失败 |
| `message` | String | 错误信息；成功时为空 |
| `records` | String | 匹配记录集 JSON 字符串 |

---

## 5. 返回记录结构

`records` 解析后为数组，单条记录字段如下：

| 字段 | 类型 | 说明 |
|---|---|---|
| `tradeTermId` | String | 标准条件编码 |
| `buId` | String | 事业部编码 |
| `buName` | String | 事业部名称 |
| `subId` | String | 子公司编码 |
| `subName` | String | 子公司名称 |
| `countryCode` | String | 国家代码 |
| `countryName` | String | 国家名称 |
| `typeId` | String | 产品分类编码 |
| `typeName` | String | 产品分类名称 |
| `buyerGrade` | String | 客户分类代码 |
| `downPay` | Decimal | 首付款比例（0-1） |
| `payTerm` | Int32 | 账期（天） |
| `payFreq` | Int32 | 付款频次（天） |

---

## 6. 调用示例与返回

### 请求

```csharp
var request = new OrganizationRequest("mcs_QueryTradeStPayTerm");
request["mcs_buid"] = "BU-1018";
request["mcs_subid"] = "SUB-001";
request["mcs_countrycode"] = "CN";
request["mcs_prdgroupid"] = "PM";
request["mcs_buyercode"] = "AID202507100000";
```

### 响应

```json
{
  "status": "1",
  "message": "",
  "records": [
    {
      "tradeTermId": "TC26062302",
      "buId": "BU-1018",
      "buName": null,
      "subId": "SUB-001",
      "subName": null,
      "countryCode": "CN",
      "countryName": null,
      "typeId": "03",
      "typeName": null,
      "buyerGrade": "C",
      "downPay": 0.3000000000,
      "payTerm": 30,
      "payFreq": 30
    }
  ]
}
```

---

## 7. 业务规则

### 7.1 客户分类计算

接口会根据 `mcs_buyercode` 查询客户主数据，自动计算客户分类代码：

#### 经销商

客户类别为以下之一，或 `mcs_dealerrank` 有值：

| mcs_accountcategory | 含义 |
|---|---|
| 10 | Official Dealer |
| 30 | Dealer End Customer |
| 60 | Dealer Key Account |
| 90 | Prospective Dealer |

经销商分级映射：

| mcs_dealerrank | buyerGrade |
|---|---|
| 1 钻石 | D1 |
| 2 铂金 | D2 |
| 3 白银 | D3 |
| 4 认证 | D4 |
| 5 意向 | D5 |

#### 直销客户

| mcs_accountlevel | buyerGrade |
|---|---|
| 4 Diamond | S |
| 3 Gold | A |
| 2 Silver | B |
| 1 Other / 空 | C |

> 个人客户判断逻辑待业务确认（客户主数据表暂无客户类型字段）。

### 7.2 产品线 → 产品分类映射

根据 `mcs_prdgroupid` 查询 `mcs_trade_ptgrouptype` 实体，获取 `mcs_typeid`。

示例：

| mcs_prdgroupid | 产品线 | mcs_typeid |
|---|---|---|
| AK | 牵引车 | 01 |
| EL | 电动牵引车 | 02 |
| PM | 泵车 | 03 |

### 7.3 查询条件

只查询 `mcs_trade_stpayterm` 中 `mcs_status` = 2（生效）的记录。

#### 泵路事业部

当 `mcs_buid` = `BU-1018` 时（当前占位，待业务确认）：

| 维度 | 匹配逻辑 |
|---|---|
| 子公司 | 必须等于入参 `mcs_subid` |
| 国家 | 不参与查询 |
| 产品分类 | `mcs_typeid` 包含入参产品分类 |
| 客户分类 | `mcs_buyergrade` 包含入参客户分类 |

#### 其他事业部

| 维度 | 匹配逻辑 |
|---|---|
| 子公司 | `mcs_subid` = 入参 OR `mcs_subid` = `NA` |
| 国家 | `mcs_countrycode` 包含入参 OR `mcs_countrycode` = `NA` |
| 产品分类 | `mcs_typeid` 包含入参 OR `mcs_typeid` = `NA` |
| 客户分类 | `mcs_buyergrade` 包含入参 |

> `NA` 表示通配，可匹配任意值。

---

## 8. 错误处理

| status | message | 场景 |
|---|---|---|
| `0` | 事业部编码不能为空 | 缺少 `mcs_buid` |
| `0` | 子公司编码不能为空 | 缺少 `mcs_subid` |
| `0` | 国家代码不能为空 | 缺少 `mcs_countrycode` |
| `0` | 产品线编码不能为空 | 缺少 `mcs_prdgroupid` |
| `0` | 客户编码不能为空 | 缺少 `mcs_buyercode` |
| `0` | 查询失败: ... | Plugin 执行异常 |
| `1` | 空字符串 | 成功，但可能无匹配记录 |

---

## 9. 部署与发布

### 9.1 涉及解决方案

| 解决方案 | 内容 |
|---|---|
| `McsPlugin` | Plugin Assembly `SanyD365.D365Extension.Sales` |
| `McsCustomAPI` | Custom API `mcs_QueryTradeStPayTerm`、请求参数、响应属性 |

### 9.2 UAT/PROD 发布

通过 n8n Release Tool 发布时，需同时勾选：

- `McsPlugin`
- `McsCustomAPI`
- `McsWebResource`（如同时更新 JS）

---

## 10. 待确认事项

1. **泵路事业部真实编码**：当前代码使用 `BU-1018` 作为泵路事业部标识，需业务确认后替换。
2. **个人客户判断**：客户主数据表 `mcs_customermasterdata` 暂无 `mcs_customertype` 字段，个人客户逻辑待补充。
3. **客户等级**：PRD 明确 `mcs_creditgrade` 暂不参与查询。
