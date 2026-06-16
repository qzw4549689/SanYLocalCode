# 科法斯接口清单 API Mandatory Query Parameters 2026

> 文件名: 科法斯接口清单API Mandatory Query Parameters 2026
> 来源: 飞书云盘 10客户资信科法斯集成
> 最近修改: 2026年4月28日 12:59
> 整理时间: 2026-06-04

---

## 说明

此文件是 **Coface API Mandatory Query Parameters SANY20260512.xlsx** 的前身/原始版本。

两个版本对比：

| 对比项 | 本文件 (2026原始版) | SANY20260512版 |
|--------|-------------------|---------------|
| 文件名 | 科法斯接口清单API Mandatory Query Parameters 2026 | Coface API Mandatory Query Parameters SANY20260512 |
| 修改时间 | 2026年4月28日 | 2026年5月12日 |
| Sheet数 | 2个 (Report, URBA) | 3个 (URBA, Report, publicationid) |
| 内容差异 | 基础接口清单 | 增加了publicationid说明、更详细的备注 |

**建议使用 SANY20260512 版本**，内容更完整。

---

## Sheet1: Report (18个接口)

| No. | Category | Description | 接口描述 | Endpoint | Mandatory Fields |
|-----|----------|-------------|---------|----------|-----------------|
| 1 | Country | Retrieve all available countries | 检索科法斯产品覆盖的国家/地区 | GET /countries | NULL |
| 2 | Company | Check which external id is legal per country | 检查每个国家支持的合法external ID | GET /companies/repositories | countryCode |
| 3 | LegitimateInterestCodes | Legitimate interest codes accepted by Coface | 获取合法权益代码 | GET /legitimateinterestcodes | countryCode |
| 4 | Company | Search for a company | 搜索公司 | GET /companies | companyId OR externalId+countryCode OR companyName+countryCode |
| 5 | Company | Request for a company identification/creation | 请求公司识别与创建 | POST /companies/identifications | externalId+address.countryCode OR name+address.postalCode+address.city+address.countryCode |
| 6 | Company | Retrieve all the requests for identification | 检索所有的公司识别请求 | GET /companies/identifications | NULL |
| 7 | Assessment | Returns potential assessments for country | 获取目标国家的评估产品列表 | GET /assessments | countryCode |
| 8 | Company | Get which are the available report for a company | 获取公司的可用报告 | GET /companies/reports | externalId+countryCode |
| 9 | Company | Get all available products for a company | 获取公司的所有可用产品 | GET /companies/products | externalId+countryCode |
| 10 | Publications | Request a new publication | 下单订购产品 | POST /publications/orders | externalId+countryCode AND one assessments OR one report |
| 11 | Publications | Get all the publication orders | 按条件查询已订购的订单 | GET /publications/orders | id OR companyId OR externalId+countryCode OR productSlug OR productCode OR orderedFrom OR orderedTo OR deliveredFrom OR deliveredTo OR customerReference OR channelDelivery |
| 12 | Publications | Retrieve an ordered publication based on an ID and format | 根据ID和格式获取订单生成内容 | GET /publications | id + format |
| 13 | Monitorings | Request a new monitoring | 下单监控 | POST /monitorings/orders | externalId+countryCode AND one assessments OR one report |
| 14 | Monitorings | Retrieve all ordered monitoring | 按条件查询已订购的监控 | GET /publications/orders | 多种条件组合 |
| 15 | Notification | Retrieve all the notifications | 查询监控期间收到的变更通知 | GET /notifications | deliveryDateFrom OR deliveryDateTo OR productSlug OR productCode OR monitoringId OR companyId OR customerReference OR channelDelivery |
| 16 | Notification | Retrieve the content of a notification | 获取监控通知里的变化信息 | GET /notifications/{notificationId}/content | notificationId + format |
| 17 | Notification | Get the attachments of a monitoring notification | 获取监控变化后全量报告名称和格式 | GET /notifications/{notificationId}/attachments | notificationId |
| 18 | Notification | Retrieve the file attachment of a monitoring notification | 获取监控变化后最新状态的全量报告 | GET /notifications/{notificationId}/attachments/{fileName} | notificationId + fileName |

---

## Sheet2: URBA (14个接口)

| No. | Category | Description | 接口描述 | Endpoint | Mandatory Fields |
|-----|----------|-------------|---------|----------|-----------------|
| 1 | Country | Retrieve all available countries | 检索科法斯产品覆盖的国家/地区 | GET /countries | NULL / countryCode |
| 2 | Company | Retrieve all organizations referencing companies by countries | 检查每个国家支持的合法external ID | GET /companies/repositories | countryCode |
| 3 | LegitimateInterestCodes | Legitimate interest codes accepted by Coface | 获取合法权益代码 | GET /legitimateinterestcodes | countryCode |
| 4 | Company | Search for a company | 搜索公司 | GET /companies | companyId OR externalId+countryCode OR companyName+countryCode |
| 5 | URBA360 | Request a new urba orders | 订购URBA产品 | POST /urba360/orders | externalId+countryCode (legitimateInterest is required for DE company) |
| 6 | URBA360 | Retrieve all urba orders | 查询已订购的URBA订单 | GET /urba360/orders | externalId+countryCode OR countryCode OR companyId OR orderFrom OR orderTo OR deliveredFrom OR deliveredTo OR customerReference OR channelDelivery |
| 7 | URBA360 | Get the status of the orders placed for URBA 360 | 通过ID查询URBA 360订单的状态 | GET /urba360/orders/{id} | id |
| 8 | URBA360 | Get all information related to an order on URBA360 | 获取URBA订单内容 | GET /urba360/content | id |
| 9 | URBA360 | Place a URBA360 monitoring order | 订购URBA监控 | POST /urba360/monitorings/orders | externalId+countryCode (legitimateInterest is required for DE company) |
| 10 | URBA360 | Retrieve all urba monitoring orders | 查询筛选已订购的URBA监控 | GET /urba360/monitorings/orders | externalId+countryCode OR countryCode OR companyId OR orderFrom OR orderTo OR deliveredFrom OR deliveredTo OR customerReference OR channelDelivery |
| 11 | URBA360 | Get the status of the monitoring orders placed for URBA 360 | 获取URBA监控的状态 | GET /urba360/monitorings/orders/{id} | id |
| 12 | URBA360 | Retrieve all the notifications occurred during the URBA 360 monitoring | 获取URBA监控的更新通知 | GET /urba360/notifications | deliveryDateFrom OR deliveryDateTo OR monitoringId OR companyId OR customerReference |
| 13 | URBA360 | Get the content of a URBA360 notification | 获取监控通知中的具体内容 | GET /urba360/notifications/{notificationId}/content | notificationId |
| 14 | Publications | GET the content of a URBA component | 获取URBA订单中单个模块的内容 | GET /publications | format + id (URBA sub-component id) |

---

## 版本差异说明

### SANY20260512 版本新增内容

1. **新增 Sheet3 (publicationid)**：说明 Publication ID 的获取和使用
2. **更详细的 Cathy 备注**：关于接口使用场景和注意事项
3. **API Portal 链接**：每个接口都附带了开发者平台链接
4. **问题列**：记录了各接口的疑问和解决方案

### 建议使用

**开发时以 SANY20260512 版本为准**，本文件仅作为历史参考。
