# Coface API Solutions Flow - SANY

> 来源: Coface API Solutions Flow - SANY.pdf（流程图）
> 整理时间: 2026-06-04
> 页数: 4页

---

## 业务目标

从科法斯产品 **URBA360** 及 **报告** 中获取三一评分卡所需的关键字段。

---

## 第1页：科法斯产品调用流程概览

### 三大流程模块

```
┌─────────────────┐    ┌──────────────────────┐    ┌─────────────────┐
│  1 公司搜索      │ → │  2 带监测的URBA360    │ → │  3 商业报告      │
│  (Company Search)│    │  (URBA360 Monitoring)│    │  (Report)       │
└─────────────────┘    └──────────────────────┘    └─────────────────┘
```

### 完整流程

```
开始
  ↓
[1 公司搜索]
  ├── 搜索公司 (GET /companies)
  │     └── 识别被调查公司？
  │           ├── 是 → [2 带监测的URBA360]
  │           │         ├── 使用ICON Number订购带监测的URBA360
  │           │         ├── 检查订单状态
  │           │         └── 获取URBA360内容
  │           │
  │           └── 否 → 发起新公司调查并下单Full Report/Full Report CEE
  │                     └── 检查是否识别新公司并生成ICON Number
  │                           ├── 是 → 获取报告内容并记录ICON Number
  │                           │         └── 是否在搜索公司阶段（创建新公司）订购报告？
  │                           │               ├── 是 → 结束
  │                           │               └── 否 → [3 商业报告]
  │                           │
  │                           └── 否 → 返回Negative Report → 结束
  │
[3 商业报告]
  ├── 被调查公司是否在受限国家？
  │     ├── 是 → 使用ICON Number下单Full Report/Full Report CEE (RU)
  │     │         ├── 检查订单状态
  │     │         └── 获取报告内容 → 结束
  │     │
  │     └── 否 → 使用ICON Number下单URBA Full Report
  │               ├── 检查订单状态
  │               └── 获取报告内容 → 结束
```

---

## 第2页：1 公司索引搜索

### 业务场景：主体校验

```
开始
  ↓
三种搜索方式（并行）：
  ├─ 方式1: Country Code + Company Name
  │     GET {{dataAPIBaseUrl}}/companies?companyName={}&countryCode={}
  │
  ├─ 方式2: Country Code + Legal Identifiers
  │     GET {{dataAPIBaseUrl}}/companies?countryCode={}&externalId={}
  │
  └─ 方式3: Country Code + Coface Identifiers (ICON Number, Easy Number, GIID等)
  │     GET {{dataAPIBaseUrl}}/companies?countryCode={}&externalId={}
  │
  ↓
返回搜索结果？
  ├── 否 → 优化搜索条件 → 重新搜索
  │
  └── 是 → 识别出所需公司结果？
              ├── 否 → 需要下单该公司？
              │         ├── 否 → 搜索结束
              │         └── 是 → 发起该公司识别调查
              │                   POST {{dataAPIBaseUrl}}/companies/identifications
              │                   └── 是否识别该公司？
              │                         ├── 是 → 返回识别状态identified和icon号
              │                         │         └── 用ICON Number下单该公司带监测的URBA360
              │                         │               POST {{dataAPIBaseUrl}}/urba360/orders
              │                         │               └── 下单结束
              │                         │
              │                         └── 否 → 返回识别状态initiated和companyIdentificationId
              │                                   └── 用companyIdentificationId下单该公司
              │                                         POST {{dataAPIBaseUrl}}/publications/orders
              │                                         └── 下单结束
              │
              └── 是 → 检索结果中只有giid？
                        ├── 是 → 使用Country Code + giid进一步检索公司
                        │         └── 结果中包含ICON Number
                        │               └── 用ICON Number下单该公司带监测的URBA360
                        │                     POST {{dataAPIBaseUrl}}/urba360/orders
                        │                     └── 下单结束
                        │
                        └── 否 → （已有ICON Number）
                                  └── 用ICON Number下单该公司带监测的URBA360
                                        POST {{dataAPIBaseUrl}}/urba360/orders
                                        └── 下单结束
```

---

## 第3页：2 带监测的URBA360

### 业务场景

获取三一评分卡所需字段：
- `score` (外部评级)
- `latePaymentIndex` (迟付指数)
- `balanceSheet` (资产负债表)
- `countryRiskAssessment` (国别风险)
- `sectorRiskAssessment` (行业风险)
- `naceCodes` (行业属性)

### 流程

```
开始
  ↓
请求者从公司索引搜索中选择相应的公司
  ↓
提交带监测的URBA360订单
  POST {{dataAPIBaseUrl}}/urba360/orders
  ↓
系统返回订单状态 "in-preparation" (准备中)
  ↓
输入Order ID检查报告状态
  GET {{dataAPIBaseUrl}}/urba360/monitorings/orders/{id}
  ↓
监测报告就绪？
  ├── 否 → 循环检查状态
  │
  └── 是 → 系统返回订单状态 "Ready"(就绪)以及公司正被监测
            ↓
            输入Monitoring ID提取URBA360内容
              GET {{dataAPIBaseUrl}}/urba360/content?id={global ID}
            ↓
            系统返回URBA360内容
            ↓
            输入日期或订单检查更新
              GET {{dataAPIBaseUrl}}/urba360/notifications
            ↓
            监测到更新？
              ├── 否 → 结束
              │
              └── 是 → 输入Notification ID获取更新
                        GET {{dataAPIBaseUrl}}/urba360/notifications/{notificationId}/content
                        ↓
                        系统返回更新 → 结束
```

**输入参数：**
- ICON Number (externalId)
- 语言 (language)
- 参考信息 (customerReference)

---

## 第4页：3 商业报告 (URBA Full Report/Full Report/Full Report CEE)

### 业务场景

获取三一评分卡所需字段：
- `shareCapital` (注册资本)
- `registration.date` (成立日期)
- `additionalInsolvencyList` (诉讼债权)

### 流程

```
开始
  ↓
请求者从公司索引搜索中选择相应的公司
  ↓
检查即时报告可用？
  GET {{dataAPIBaseUrl}}/companies/reports?countryCode={}&externalId=icon#{}
  ↓
即时报告可用？
  ├── 是 → 提交请求即时报告
  │         └── 系统返回订单状态 "Ready"(就绪)
  │               ↓
  │               输入Publication Order ID及format提取报告
  │                 GET /publications
  │               ↓
  │               系统返回商业报告 → 结束
  │
  └── 否 → 请求商业报告 - 全新调查*
            POST /publications/orders
            ↓
            提交请求调查报告
            ↓
            系统返回订单状态 "in-preparation" (准备中)
            ↓
            输入Order ID检查报告状态
              GET /publications/orders
            ↓
            商业报告就绪？
              ├── 否 → 循环检查状态
              │
              └── 是 → 系统返回订单状态 "Ready"(就绪)
                        ↓
                        输入Publication Order ID及format提取报告
                          GET /publications
                        ↓
                        系统返回商业报告 → 结束
```

**输入参数：**
- 商业报告类型 (reportSlug)
- 语言 (language)
- 参考信息 (customerReference)
- 报告格式 (format)
- 交付速度 (investigationSpeedLevel)
- 即时交付/全新调查 (processingTimeInstruction)
- 多数国家支持双格式在一个订单

---

## 流程对比总结

| 流程 | 产品 | 下单接口 | 状态检查 | 内容获取 | 数据时效 |
|------|------|---------|---------|---------|---------|
| **公司搜索** | - | GET /companies | - | - | 实时 |
| **URBA360** | URBA360 | POST /urba360/orders | GET /urba360/monitorings/orders/{id} | GET /urba360/content | 实时监控 |
| **商业报告** | Full Report | POST /publications/orders | GET /publications/orders | GET /publications | 6-7工作日 |

## 12指标对应流程

| 指标 | 所属流程 | API |
|------|---------|-----|
| 外部评级 | URBA360 | GET /urba360/content |
| 迟付指数 | URBA360 | GET /urba360/content |
| 国别风险 | URBA360 | GET /urba360/content |
| 行业风险 | URBA360 | GET /urba360/content |
| 行业属性 | URBA360 | GET /urba360/content |
| 注册资本 | 商业报告 | GET /publications |
| 成立日期 | 商业报告 | GET /publications |
| 财务指标(4个) | 商业报告 | GET /publications |
| 诉讼债权金额 | 商业报告PDF | GET /publications?format=pdf |

## 关键决策点

1. **公司是否已识别？** → 否 → 发起识别调查
2. **是否需要下单？** → 是 → 选择URBA360或Report
3. **是否在受限国家？** → 是 → 使用Full Report CEE (俄罗斯)
4. **报告是否就绪？** → 循环检查直到Ready
5. **是否有更新？** → 是 → 获取Notification内容
