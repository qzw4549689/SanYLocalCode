# 成交条件样板库查询 API 调用说明

> **接口形态**：D365 Custom API（D365 内部接口）  
> **Custom API 唯一名**：`mcs_QueryTradeStPayTerm`  
> **部署环境**：DEV1 / UAT / PROD  
> **最后更新**：2026-07-28

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
request["mcs_subid"] = "A000025";
request["mcs_countrycode"] = "001";
request["mcs_prdgroupid"] = "4";
request["mcs_buyercode"] = "ACN202605280000";

var response = service.Execute(request);

var status = response["status"]?.ToString();
var message = response["message"]?.ToString();
var records = response["records"]?.ToString(); // JSON 字符串，解析后为记录数组
```

### 2.2 JavaScript / WebResource 中调用

```javascript
var request = {
    mcs_buid: "BU-1018",
    mcs_subid: "A000025",
    mcs_countrycode: "001",
    mcs_prdgroupid: "4",
    mcs_buyercode: "ACN202605280000"
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

| 参数名 | 类型 | 必填 | 说明 | 对应表名（架构名称） |
|---|---|---|---|---|
| `mcs_buid` | String | 是 | 事业部编码 | `mcs_trade_stpayterm.mcs_buid` |
| `mcs_subid` | String | 是 | 子公司编码 | `mcs_trade_stpayterm.mcs_subid` |
| `mcs_countrycode` | String | 是 | 国家代码 | `mcs_country.mcs_countrycode` |
| `mcs_prdgroupid` | String | 是 | 产品线编码；**支持逗号分隔传多个**（如 `CP0202,CP0501`），多个产品线映射的产品分类取并集，与记录产品分类任一相交即命中 | `mcs_trade_ptgrouptype.mcs_groupid` |
| `mcs_buyercode` | String | 是 | 客户编码 | `mcs_customermasterdata.mcs_accountnumber` |

---

## 4. 响应参数

| 参数名 | 类型 | 说明 |
|---|---|---|
| `status` | String | `1` 成功，`0` 失败 |
| `message` | String | 错误信息；成功时为空 |
| `records` | String | 匹配记录集 JSON 字符串，解析后即为记录数组；无匹配或失败时为 `"[]"`（错误信息看顶层 `message`） |

---

## 5. 返回记录结构

`records` 解析后为记录数组，单条记录字段如下：

| 字段 | 类型 | 说明 | 对应表名（架构名称） |
|---|---|---|---|
| `tradeTermId` | String | 标准条件编码 | `mcs_trade_stpayterm.mcs_trade_stpaytermname` |
| `buId` | String | 事业部编码 | `mcs_trade_stpayterm.mcs_buid` |
| `buName` | String | 事业部名称 | `mcs_trade_stpayterm.mcs_buname` |
| `subId` | String | 子公司编码 | `mcs_trade_stpayterm.mcs_subid` |
| `subName` | String | 子公司名称 | `mcs_trade_stpayterm.mcs_subname` |
| `countryCode` | String | 国家代码（多个以逗号分隔） | `mcs_trade_stpayterm.mcs_countries`（GUID 集合，关联 `mcs_country.mcs_countrycode`） |
| `countryName` | String | 国家名称（多个以逗号分隔） | `mcs_country.mcs_name` |
| `typeId` | String | 产品分类编码（多个以逗号分隔） | `mcs_trade_stpayterm.mcs_trade_type`（GUID 集合，关联 `mcs_trade_pttype.mcs_typeid`） |
| `typeName` | String | 产品分类名称（多个以逗号分隔） | `mcs_trade_pttype.mcs_trade_pttypename` |
| `buyerGrade` | String | 客户分类代码（多个以 / 分隔） | `mcs_trade_stpayterm.mcs_buyergrade` |
| `downPay` | Decimal | 首付款比例（0-1） | `mcs_trade_stpayterm.mcs_downpay` |
| `payTerm` | Int32 | 账期（天） | `mcs_trade_stpayterm.mcs_payterm` |
| `payFreq` | Int32 | 付款频次（天） | `mcs_trade_stpayterm.mcs_payfreq` |

---

## 6. 调用示例与返回

> 以下入参为实测可调通的组合（2026-07-28 验证），其他环境请按实际数据调整。
> `mcs_prdgroupid` 支持逗号分隔传多个产品线编码，如 `"CP0202,CP0501"`。

### 6.1 单产品线查询示例（DEV1 实测）

### 请求

```csharp
var request = new OrganizationRequest("mcs_QueryTradeStPayTerm");
request["mcs_buid"] = "BU-1018";
request["mcs_subid"] = "A000025";
request["mcs_countrycode"] = "001";
request["mcs_prdgroupid"] = "4";
request["mcs_buyercode"] = "ACN202605280000";
```

### 响应

> 输出参数：`status="1"`、`message=""`、`records` = 如下 JSON **字符串**（解析后即为记录数组）。

```json
[
  {
    "tradeTermId": "TC26062502",
    "buId": "BU-1018",
    "buName": "CONCRETE MACHINERY BU/泵路海外营销公司",
    "subId": "A000025",
    "subName": "China-SH",
    "countryCode": "1,001,KT",
    "countryName": "南非,中国,Kertdiva",
    "typeId": "01,02",
    "typeName": "燃油牵引车,电动牵引车",
    "buyerGrade": "A/S",
    "downPay": 0.4000000000,
    "payTerm": 30,
    "payFreq": 30
  }
]
```

### 6.2 多产品线查询示例（UAT 实测，2026-07-28）

`mcs_prdgroupid` 逗号分隔传多个产品线编码，任一映射分类命中即返回：

### 请求

```csharp
var request = new OrganizationRequest("mcs_QueryTradeStPayTerm");
request["mcs_buid"] = "BU-0026";
request["mcs_subid"] = "A011058";
request["mcs_countrycode"] = "AI";
request["mcs_prdgroupid"] = "CP0202,CP0501"; // CP0202→分类20（不命中），CP0501→分类01（命中），并集后命中
request["mcs_buyercode"] = "AID202309210002";
```

### 响应（节选）

> `records` 输出参数内容节选（解析后即为记录数组）。

```json
[
  {
    "tradeTermId": "TC2607270026",
    "buId": "BU-0026",
    "buName": "Latin America BU/拉美大区",
    "subId": "A011058",
    "subName": "Brazil Region/巴西国区",
    "countryCode": "AI",
    "countryName": "Anguilla",
    "typeId": "01,02,03",
    "typeName": "燃油牵引车,电动牵引车,泵车",
    "buyerGrade": "A/B/S",
    "downPay": 0.1500000000,
    "payTerm": 120,
    "payFreq": 30
  }
]
```

> 说明：单独传 `CP0202` 时无匹配记录（返回空数组）；改为 `CP0202,CP0501` 后因 CP0501 映射的分类 01 与记录相交而命中。

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
支持逗号分隔传多个产品线编码（如 `2,4`），取并集后与记录的产品分类做交集匹配，任一有交集即命中。

示例（UAT 实际映射）：

| mcs_prdgroupid | 产品线 | mcs_typeid |
|---|---|---|
| CP0501 | 压路机 | 01 |
| CP7320 | 测试大类C | 02 |
| CP0224 | 土石 | 03 |
| CP0202 | 中挖 | 20 |
| CP0410 | 充填泵 | 10000 |

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
| `1` | 空字符串 | 成功，但可能无匹配记录（`records` 为 `"[]"`） |

> 无论成功/失败，`records` 输出参数均为记录数组 JSON 字符串（无匹配或失败时为 `"[]"`）。

---

## 9. 部署与发布

### 9.1 涉及解决方案

| 解决方案 | 内容 |
|---|---|
| `McsCustomAPI` | Custom API `mcs_QueryTradeStPayTerm`、请求参数、响应属性，**以及实现 Assembly `SanyD365.D365ExtensionApi.Sales`** |

> 注意：`McsPlugin` 中的 `SanyD365.D365Extension.Sales` 仅有已废弃的 `QueryTradeStPayTermPlugin_Legacy` 副本，本接口不涉及。

### 9.2 UAT/PROD 发布

通过 n8n Release Tool 发布时，勾选：

- `McsCustomAPI`（同时携带 Custom API 元数据和实现 Assembly）
- `McsWebResource`（如同时更新 JS）

---

## 10. 待确认事项

1. **泵路事业部真实编码**：当前代码使用 `BU-1018` 作为泵路事业部标识，需业务确认后替换。
2. **个人客户判断**：客户主数据表 `mcs_customermasterdata` 暂无 `mcs_customertype` 字段，个人客户逻辑待补充。
3. **客户等级**：PRD 明确 `mcs_creditgrade` 暂不参与查询。

---

## 11. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-07-28 | `mcs_prdgroupid` 支持逗号分隔传多个产品线编码（PR 6246）；示例参数更新为实测可调通组合；补充参数对应表名（架构名称） |
| 2026-07-30 | `records` 输出改为裸记录数组（PR 6377），与失败路径 `"[]"` 一致；修正 §9 部署说明（实现 Assembly 随 `McsCustomAPI` 发布） |
