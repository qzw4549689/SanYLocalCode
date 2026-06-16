# Coface API Mandatory Query Parameters SANY20260512

> 来源: Coface API Mandatory Query Parameters SANY20260512.xlsx
> 整理时间: 2026-06-04
> 包含: URBA Sheet + Report Sheet + PublicationId Sheet

---

# 目录

- [第一部分：URBA 接口](#第一部分urba-接口)
- [第二部分：Report 接口](#第二部分report-接口)
- [第三部分：Publication ID 说明](#第三部分publication-id-说明)

---

# 第一部分：URBA 接口

## 接口清单（14个接口）

### 1. 获取国家列表
| 项目 | 内容 |
|------|------|
| No. | 1 |
| Category | Country |
| Description | Retrieve all available countries |
| 描述 | 检索科法斯产品覆盖的国家/地区 |
| Endpoint | `GET /countries` |
| Mandatory Fields | NULL |
| 是否包含 | ❌ 否 |

---

### 2. 获取国家支持的External ID类型
| 项目 | 内容 |
|------|------|
| No. | 2 |
| Category | Company |
| Description | Retrieve all organizations referencing companies by countries |
| 描述 | 检查每个国家支持的合法 external ID |
| Endpoint | `GET /companies/repositories` |
| Mandatory Fields | countryCode |
| 是否包含 | ❌ 否 |

---

### 3. 获取合法权益代码
| 项目 | 内容 |
|------|------|
| No. | 3 |
| Category | LegitimateInterestCodes |
| Description | Legitimate interest codes accepted by Coface |
| 描述 | 获取合法权益代码 |
| Endpoint | `GET /legitimateinterestcodes` |
| Mandatory Fields | countryCode |
| 是否包含 | ❌ 否 |

---

### 4. 搜索公司 ⭐核心接口
| 项目 | 内容 |
|------|------|
| No. | 4 |
| Category | Company |
| Description | Search for a company |
| 描述 | 搜索公司 |
| Endpoint | `GET /companies` |
| Mandatory Fields | **companyId** OR externalId + countryCode OR companyName + countryCode (美国公司搜索再加上city) |
| 是否包含 | ✅ **Y** |
| 备注 | 对第一批线下匹配504个大客户，已获取externalId，采用该方式；对于新客户，需要采用companyName英文名称模糊查询 |

**搜索方式优先级：**
1. `externalId + countryCode` - 504家大客户（已线下匹配）
2. `companyName + countryCode` (+ city for US) - 新客户

---

### 5. 订购URBA产品 ⭐核心接口
| 项目 | 内容 |
|------|------|
| No. | 5 |
| Category | URBA360 |
| Description | Request a new urba orders |
| 描述 | 订购URBA产品 |
| Endpoint | `POST /urba360/orders` |
| Mandatory Fields | externalId + countryCode (legitimateInterest is required for DE company) |
| 是否包含 | ✅ **Y** |

---

### 6. 查询所有URBA订单
| 项目 | 内容 |
|------|------|
| No. | 6 |
| Category | URBA360 |
| Description | Retrieve all urba orders |
| 描述 | 查询已订购的URBA订单 |
| Endpoint | `GET /urba360/orders` |
| Mandatory Fields | externalId + countryCode |
| 是否包含 | ⚠️ 可选 |
| 备注 | 该接口是订单查询接口，主要目的是看sany下过哪些urba订单。Sany不下urba订单，而是下的urba监控订单，此情形用序号10的接口 |

---

### 7. 查询URBA订单状态
| 项目 | 内容 |
|------|------|
| No. | 7 |
| Category | URBA360 |
| Description | Get the status of the orders placed for URBA 360 |
| 描述 | 通过ID查询URBA 360订单的状态 |
| Endpoint | `GET /urba360/orders/{id}` |
| Mandatory Fields | id |
| 是否包含 | ✅ **Y** |

---

### 8. 获取URBA订单内容 ⭐核心接口
| 项目 | 内容 |
|------|------|
| No. | 8 |
| Category | URBA360 |
| Description | Get all information related to an order on URBA360 for a company |
| 描述 | 获取URBA订单内容 |
| Endpoint | `GET /urba360/content` |
| Mandatory Fields | id |
| 是否包含 | ✅ **Y** |
| 备注 | id是order id，但URBA产品不需要发起订单接口，如何获得order id？分情况：1. 对于科法斯帮助客户下的批量订单，前置接口是序号10的接口，可以用deliveredFrom来筛选所有批量订单；2. 如果是项目上线后的后期订单通过API下单的，用序号9接口返回的order id，前提是需要用接口11查出来该订单的状态已经ready了 |

---

### 9. 订购URBA监控 ⭐核心接口
| 项目 | 内容 |
|------|------|
| No. | 9 |
| Category | URBA360 |
| Description | Place a URBA360 monitoring order |
| 描述 | 订购URBA监控 |
| Endpoint | `POST /urba360/monitorings/orders` |
| Mandatory Fields | externalId + countryCode (legitimateInterest is required for DE company) |
| 是否包含 | ✅ **Y** |

---

### 10. 查询所有URBA监控订单 ⭐核心接口
| 项目 | 内容 |
|------|------|
| No. | 10 |
| Category | URBA360 |
| Description | Retrieve all urba monitoring orders |
| 描述 | 查询筛选已订购的URBA监控 |
| Endpoint | `GET /urba360/monitorings/orders` |
| Mandatory Fields | externalId + countryCode |
| 是否包含 | ✅ **Y** |
| 备注 | 补充了这个接口，用来筛选出504批量订单 |

---

### 11. 获取URBA监控状态
| 项目 | 内容 |
|------|------|
| No. | 11 |
| Category | URBA360 |
| Description | Get the status of the monitoring orders placed for URBA 360 |
| 描述 | 获取URBA监控的状态 |
| Endpoint | `GET /urba360/monitorings/orders/{id}` |
| Mandatory Fields | id |
| 是否包含 | ✅ **Y** |

---

### 12. 获取URBA监控更新通知
| 项目 | 内容 |
|------|------|
| No. | 12 |
| Category | URBA360 |
| Description | Retrieve all the notifications occurred during the URBA 360 monitoring |
| 描述 | 获取URBA监控的更新通知 |
| Endpoint | `GET /urba360/notifications` |
| Mandatory Fields | deliveryDateFrom OR deliveryDateTo OR monitoringId OR companyId OR customerReference |
| 是否包含 | ✅ **Y** |

---

### 13. 获取监控通知具体内容
| 项目 | 内容 |
|------|------|
| No. | 13 |
| Category | URBA360 |
| Description | Get the content of a URBA360 notification |
| 描述 | 获取监控通知中的具体内容 |
| Endpoint | `GET /urba360/notifications/{notificationId}/content` |
| Mandatory Fields | notificationId |
| 是否包含 | ✅ **Y** |

---

### 14. 获取URBA订单单个模块内容
| 项目 | 内容 |
|------|------|
| No. | 14 |
| Category | Publications |
| Description | GET the content of a URBA component |
| 描述 | 获取URBA订单中单个模块的内容 |
| Endpoint | `GET /publications` |
| Mandatory Fields | format + id (URBA sub-component id) |
| 是否包含 | 可选 |
| 备注 | 用序号8的接口获取全量数据后，可不用该接口。使用该接口的场景：1. 在订单partially_ready时，先取出已经ready的模块内容；2. 并不需要全部urba信息，只关注其中某个模块 |

---

## URBA 核心接口总结

| 序号 | 接口 | 用途 |
|------|------|------|
| 4 | GET /companies | 搜索公司 |
| 5 | POST /urba360/orders | 订购URBA |
| 7 | GET /urba360/orders/{id} | 查询订单状态 |
| 8 | GET /urba360/content | 获取URBA内容 |
| 9 | POST /urba360/monitorings/orders | 订购URBA监控 |
| 10 | GET /urba360/monitorings/orders | 查询所有监控 |
| 11 | GET /urba360/monitorings/orders/{id} | 查询监控状态 |
| 12 | GET /urba360/notifications | 获取监控通知 |
| 13 | GET /urba360/notifications/{id}/content | 获取通知内容 |

---

## URBA 订单流程

### 504家大客户批量订单（已有externalId）
```
接口10(GET /urba360/monitorings/orders) → 筛选deliveredFrom获取历史批量订单
  → 接口8(GET /urba360/content) 获取内容
```

### 新客户API下单流程
```
接口4(GET /companies) 搜索匹配
  → 接口9(POST /urba360/monitorings/orders) 订购监控
  → 接口11(GET /urba360/monitorings/orders/{id}) 查状态ready
  → 接口8(GET /urba360/content) 获取内容
```

---

# 第二部分：Report 接口

## 接口清单（18个接口）

### 1. 获取国家列表
| 项目 | 内容 |
|------|------|
| No. | 1 |
| Category | Country |
| Endpoint | `GET /countries` |
| Mandatory | NULL |
| 是否包含 | ❌ |

---

### 2. 检查国家支持的External ID
| 项目 | 内容 |
|------|------|
| No. | 2 |
| Category | Company |
| Endpoint | `GET /companies/repositories` |
| Mandatory | countryCode |
| 是否包含 | ❌ |

---

### 3. 获取合法权益代码
| 项目 | 内容 |
|------|------|
| No. | 3 |
| Category | LegitimateInterestCodes |
| Endpoint | `GET /legitimateinterestcodes` |
| Mandatory | countryCode |
| 是否包含 | ❌ |

---

### 4. 搜索公司
| 项目 | 内容 |
|------|------|
| No. | 4 |
| Category | Company |
| Endpoint | `GET /companies` |
| Mandatory | companyId OR externalId + countryCode OR companyName + countryCode |
| 是否包含 | ✅ **Y** |

---

### 5. 请求公司识别与创建
| 项目 | 内容 |
|------|------|
| No. | 5 |
| Category | Company |
| Description | 对于未识别的公司，如果从接口#4找不到公司 |
| Endpoint | `POST /companies/identifications` |
| Mandatory | externalId + address.countryCode OR name + address.postalCode + address.city + address.countryCode |
| 是否包含 | 可选 |

---

### 6. 检索所有公司识别请求
| 项目 | 内容 |
|------|------|
| No. | 6 |
| Category | Company |
| Endpoint | `GET /companies/identifications` |
| Mandatory | NULL |
| 是否包含 | ❌ |

---

### 7. 获取目标国家的评估产品列表
| 项目 | 内容 |
|------|------|
| No. | 7 |
| Category | Assessment |
| Endpoint | `GET /assessments` |
| Mandatory | countryCode |
| 是否包含 | ❌ |

---

### 8. 获取公司的可用报告
| 项目 | 内容 |
|------|------|
| No. | 8 |
| Category | Company |
| Endpoint | `GET /companies/reports` |
| Mandatory | externalId + countryCode |
| 是否包含 | ❌ |

---

### 9. 获取公司的所有可用产品
| 项目 | 内容 |
|------|------|
| No. | 9 |
| Category | Company |
| Endpoint | `GET /companies/products` |
| Mandatory | externalId + countryCode |
| 是否包含 | ❌ |

---

### 10. 下单订购产品 ⭐核心接口
| 项目 | 内容 |
|------|------|
| No. | 10 |
| Category | Publications |
| Description | 下单订购产品 |
| Endpoint | `POST /publications/orders` |
| Mandatory | externalId + countryCode AND one assessments OR one report |
| 是否包含 | ✅ **Y** |

**请求体示例：**
```json
{
  "externalId": "icon#5415240",
  "countryCode": "PL",
  "legitimateInterest": "100",
  "report": "..."
}
```

**响应示例：**
```json
{
  "id": "80b72744-3626-474f-b587-91d8f90d04fa",
  "orderDate": "2026-05-11T04:28:54.354+02:00",
  "expectedDeliveryDate": "in-preparation",
  "status": "in-preparation",
  "publications": [
    {
      "id": "773bcf31-bb14-4667-a83f-00b17daf4152",
      "status": "in-preparation",
      "expectedDeliveryDate": "in-preparation",
      "reportSlug": "customized-report",
      "customReportId": 301
    }
  ]
}
```

---

### 11. 按条件查询已订购的订单 ⭐核心接口
| 项目 | 内容 |
|------|------|
| No. | 11 |
| Category | Publications |
| Endpoint | `GET /publications/orders` |
| Mandatory | id OR companyId OR externalId+countryCode OR productSlug OR productCode OR orderedFrom OR orderedTo OR deliveredFrom OR deliveredTo OR customerReference OR channelDelivery |
| 是否包含 | ✅ **Y** |

---

### 12. 根据ID和格式获取订单生成内容 ⭐核心接口
| 项目 | 内容 |
|------|------|
| No. | 12 |
| Category | Publications |
| Description | 根据ID和格式获取订单生成内容 |
| Endpoint | `GET /publications` |
| Mandatory | id + format (这里的id是publication id，见第三部分) |
| 是否包含 | ✅ **Y** |

**重要说明：**
- `id` 是 **publication id**（不是 order id）
- 支持多种 format（JSON, PDF 等）

---

### 13. 下单监控
| 项目 | 内容 |
|------|------|
| No. | 13 |
| Category | Monitorings |
| Description | 下单监控（监控持续一年） |
| Endpoint | `POST /monitorings/orders` |
| Mandatory | externalId + countryCode AND one assessments OR one report |
| 是否包含 | ❌ |

---

### 14. 按条件查询已订购的监控
| 项目 | 内容 |
|------|------|
| No. | 14 |
| Category | Monitorings |
| Endpoint | `GET /publications/orders` |
| Mandatory | id OR companyId OR externalId+countryCode OR productSlug OR productCode OR startDateFrom OR startDateTo OR endDateFrom OR endDateTo OR orderedFrom OR orderedTo OR deliveredFrom OR deliveredTo OR naceCode OR customerReference OR channelDelivery |
| 是否包含 | ❌ |

---

### 15. 查询监控期间收到的变更通知
| 项目 | 内容 |
|------|------|
| No. | 15 |
| Category | Notification |
| Endpoint | `GET /notifications` |
| Mandatory | deliveryDateFrom OR deliveryDateTo OR productSlug OR productCode OR monitoringId OR companyId OR customerReference OR channelDelivery |
| 是否包含 | ❌ |

---

### 16. 获取监控通知里的变化信息
| 项目 | 内容 |
|------|------|
| No. | 16 |
| Category | Notification |
| Endpoint | `GET /notifications/{notificationId}/content` |
| Mandatory | notificationId + format |
| 是否包含 | ❌ |

---

### 17. 获取监控变化后全量报告名称和格式
| 项目 | 内容 |
|------|------|
| No. | 17 |
| Category | Notification |
| Endpoint | `GET /notifications/{notificationId}/attachments` |
| Mandatory | notificationId |
| 是否包含 | ❌ |
| 备注 | 仅适用于: Score report, AG/CD report, full report, full report urba, full report plus IGF, snapshot report |

---

### 18. 获取监控变化后最新状态的全量报告
| 项目 | 内容 |
|------|------|
| No. | 18 |
| Category | Notification |
| Endpoint | `GET /notifications/{notificationId}/attachments/{fileName}` |
| Mandatory | notificationId + fileName |
| 是否包含 | ❌ |
| 备注 | 仅适用于: Score report, AG/CD report, full report, full report urba, full report plus IGF, snapshot report |

---

## Report 核心接口总结

| 序号 | 接口 | 用途 |
|------|------|------|
| 4 | GET /companies | 搜索公司 |
| 10 | POST /publications/orders | 下单订购Report产品 |
| 11 | GET /publications/orders | 查询订单状态 |
| 12 | GET /publications | 获取Report内容 |

---

## Report 产品下单流程

```
接口4(GET /companies) 搜索匹配公司
  → 接口10(POST /publications/orders) 下单订购
    {
      "externalId": "icon#5415240",
      "countryCode": "PL",
      "legitimateInterest": "100",
      "report": "full-report-urba"
    }
  → 响应获取 orderId 和 publicationId
  → 接口11(GET /publications/orders) 查询状态
  → 接口12(GET /publications?id=xxx&format=json) 获取内容
```

---

# 第三部分：Publication ID 说明

## 核心概念

**Publication ID** 是 Coface Report 产品的核心标识，用于获取报告内容。

### Order ID vs Publication ID

| ID 类型 | 来源 | 用途 |
|---------|------|------|
| **Order ID** | 下单接口响应的 `id` 字段 | 查询订单状态 |
| **Publication ID** | 下单接口响应的 `publications[].id` | **获取报告内容** |

---

## 下单请求示例

```http
POST {{dataAPIBaseUrl}}/publications/orders
Content-Type: application/json

{
  "externalId": "icon#5415240",
  "countryCode": "PL",
  "legitimateInterest": "100",
  "report": "..."
}
```

---

## 下单响应示例

```json
{
  "id": "80b72744-3626-474f-b587-91d8f90d04fa",
  "orderDate": "2026-05-11T04:28:54.354+02:00",
  "expectedDeliveryDate": "in-preparation",
  "status": "in-preparation",
  "publications": [
    {
      "id": "773bcf31-bb14-4667-a83f-00b17daf4152",
      "status": "in-preparation",
      "expectedDeliveryDate": "in-preparation",
      "reportSlug": "customized-report",
      "customReportId": 301
    }
  ]
}
```

---

## 使用 Publication ID 获取内容

```http
GET {{dataAPIBaseUrl}}/publications?id=773bcf31-bb14-4667-a83f-00b17daf4152&format=json
```

**参数说明：**
- `id`: Publication ID（从下单响应获取）
- `format`: 内容格式（`json` 或 `pdf`）

---

## 产品代码 (customReportId)

| customReportId | 产品名称 | 说明 |
|---------------|---------|------|
| **301** | **Full Report URBA** | 完整URBA报告（含财务数据） |
| - | Score Report | 评分报告 |
| - | Full Report | 完整报告 |
| - | Full Report Plus IGF | - |
| - | Snapshot Report | 快照报告 |

**注意：** 301 = Full Report URBA，这与 URBA360 是不同的产品！

---

## 状态流转

```
下单 → status: "in-preparation"
  ↓
数据准备中 → expectedDeliveryDate: "in-preparation"
  ↓ (6-7个工作日)
数据就绪 → status: "ready" / "delivered"
  ↓
获取内容 → GET /publications?id={publicationId}&format=json
```

---

## 关键要点

1. **一个订单可能包含多个 publications**（数组形式）
2. **必须使用 Publication ID 获取内容**，不是 Order ID
3. **format 参数决定返回格式**：`json` 用于数据提取，`pdf` 用于人工阅读
4. **39受限国家**可能需要同时订购 JSON 和 PDF 两种格式
5. **俄罗斯**使用 Full Report CEE（特殊产品）

---

# 附录：通用备注

1. **部分公司可能存在没有companyId或不同国家有重复companyId的情况**，查询公司的可用产品以及下单请使用externalId+countryCode
2. **查询有多个结果返回时会分页显示**，通过range、offset、limit三个参数控制显示内容
3. **序号17和18中的attachment仅适用于这些产品的监控**：Score report、AG/CD report、full report、full report urba、full report plus IGF 以及snapshot report

---

# 附录：12指标数据来源汇总

| 指标 | 产品 | API | 关键路径 |
|------|------|-----|----------|
| 外部评级 | URBA360 | `GET /urba360/content` | `productDetails.score[].debtorRiskValue` |
| 迟付指数 | URBA360 | `GET /urba360/content` | `productDetails.latePaymentIndex[].value` |
| 国别风险 | URBA360 | `GET /urba360/content` | `productDetails.countryRiskAssessment[].countryRiskValue` |
| 行业风险 | URBA360 | `GET /urba360/content` | `productDetails.sectorRiskAssessment[].countryRiskValue` |
| 行业属性 | URBA360 | `GET /urba360/content` | `productDetails.sectorRiskAssessment[].sector.code` |
| 注册资本 | Report | `GET /publications?id={pid}&format=json` | `creditReport.company.capital.indicator.fromAmount` |
| 从业年限 | Report | `GET /publications?id={pid}&format=json` | `creditReport.company.established` |
| 财务指标1-4 | Report | `GET /publications?id={pid}&format=json` | `creditReport.ratios[].fromAmount` |
| 诉讼债权金额 | Report PDF | `GET /publications?id={pid}&format=pdf` | 人工读取 |
