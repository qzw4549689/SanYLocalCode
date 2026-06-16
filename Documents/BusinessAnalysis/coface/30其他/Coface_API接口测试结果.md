# Coface API 接口测试结果

> 测试时间: 2026-06-04
> 测试账号: tangys12@sany.com.cn / 1qaz!QAZ
> API Key: 0vneRg8vLjzPQlIfSkzO8kIDg04kfaKafTzg5sX1
> 测试数据: TEST FOR SIX HYDRO BUDOWA-6 S.A., PL, icon#5415240

---

## 一、测试结果汇总

### 1. 网络连通性 ✅

| 目标 | 状态 | 响应时间 |
|------|------|---------|
| api.coface.com (认证服务) | ✅ 连通 | ~0.7s |
| icon-api-test.coface.com (测试数据) | ✅ 连通 | ~0.9s |
| coface.github.io (公开样例) | ✅ 连通 | ~1s |

**结论**: 网络环境正常，所有 Coface 服务均可访问。

---

### 2. 认证测试 ✅

| 接口 | HTTP状态 | 响应 | 结论 |
|------|---------|------|------|
| `POST /authentication/v1/token` | 200 | 返回 idToken + accessToken + refreshToken | ✅ 成功 |

**认证方式**:
```bash
curl -X POST "https://api.coface.com/authentication/v1/token" \
  -H "Content-Type: application/json" \
  -H "x-api-key: {API_KEY}" \
  -d '{
    "username": "tangys12@sany.com.cn",
    "password": "1qaz!QAZ",
    "grant_type": "password"
  }'
```

**重要**: 必须同时携带 `Authorization: Bearer {idToken}` + `x-api-key: {API_KEY}` 才能访问数据接口。

---

### 3. 数据接口测试 ✅

| 接口 | 状态 | 说明 |
|------|------|------|
| `GET /countries` | ✅ 200 | 返回246个国家 |
| `GET /companies?companyName=XXX&countryCode=PL` | ✅ 200 | 公司搜索成功 |
| `GET /urba360/orders` | ✅ 200 | URBA订单列表 |
| `GET /urba360/orders/{id}` | ✅ 200 | 订单详情（含publications） |
| `GET /urba360/content?id={orderId}` | ✅ 200 | **URBA完整内容** |
| `GET /publications?id={pubId}&format=json` | ✅ 200 | **单个模块内容** |
| `GET /urba360/monitorings/orders` | ✅ 200 | URBA监控订单列表 |
| `GET /publications/orders` | ✅ 200 | Report订单列表 |
| `GET /publications?id={pubId}&format=json` | ✅ 200 | Full Report内容 |

---

## 二、12指标数据提取验证

### 数据来源确认

| 指标 | 来源产品 | API | 数据路径 | 测试结果 |
|------|---------|-----|---------|---------|
| 外部评级 | URBA360 | `GET /urba360/content` | `productDetails.score[].debtorRiskValue` | ✅ 3 |
| 迟付指数 | URBA360 | `GET /urba360/content` | `productDetails.latePaymentIndex[].value` | ✅ 2 |
| 国别风险 | URBA360 | `GET /urba360/content` | `productDetails.countryRiskAssessment[].countryRiskValue` | ✅ A3 |
| 行业风险 | URBA360 | `GET /urba360/content` | `productDetails.sectorRiskAssessment[].countryRiskValue` | ✅ 2 |
| 行业属性 | URBA360 | `GET /urba360/content` | `companyGeneralInformation.naceCodes[]` | ✅ 5个NACE |
| 注册资本 | Full Report | `GET /publications?id={pid}&format=json` | `icon.creditReport.shareCapital.nominalCapitalAmount` | ✅ 100000 PLN |
| 从业年限 | Full Report | `GET /publications?id={pid}&format=json` | `icon.creditReport.company.established` | ✅ 19470000 |
| 资产负债率 | URBA360 | `GET /urba360/content` | `productDetails.financials.ratios[]` (Debt ratio) | ✅ 24.2098 |
| 流动比率 | URBA360 | `GET /urba360/content` | `productDetails.financials.ratios[]` (Current ratio) | ✅ 7.735 |
| 净利润率 | URBA360 | `GET /urba360/content` | `productDetails.financials.ratios[]` (ROS) | ✅ 3.6219 |
| 净资产 | URBA360 | `GET /urba360/content` | `productDetails.financials.balanceSheet` (Equity) | ✅ 9607232.7953 EUR |
| 诉讼记录 | Full Report | `GET /publications?id={pid}&format=json` | `icon.creditReport.additionalInsolvencies` | ✅ 7条记录 |

---

## 三、关键发现

### 1. 认证方式
- 使用 AWS Cognito 认证流程
- `idToken` 用于数据接口（不是 `accessToken`）
- 每次请求必须同时带 `Authorization: Bearer {idToken}` 和 `x-api-key: {API_KEY}`
- Token 有效期 3600 秒（1小时）

### 2. URBA360 内容获取
- **正确接口**: `GET /urba360/content?id={orderId}`（不是 `/urba360/orders/{id}/publications/{pubId}`）
- 返回完整 URBA360 数据，包含所有子模块
- 也可通过 `GET /publications?id={pubId}&format=json` 获取单个模块

### 3. Full Report 内容获取
- **正确接口**: `GET /publications?id={publicationId}&format=json`
- `publicationId` 从 `GET /publications/orders` 响应中获取
- 不是 `orderId`，注意区分

### 4. 数据特点
- URBA360 和 Full Report 有数据重叠（如外部评级、NACE等）
- 财务数据在 URBA360 的 `productDetails.financials` 中
- 诉讼记录只在 Full Report 中提供
- 货币单位需注意转换（EUR → USD）
- dimension 字段表示金额单位（0=元,1=千,2=百万,3=十亿,4=百分比）

### 5. 测试环境数据
- 测试公司 `TEST FOR SIX HYDRO BUDOWA-6 S.A.` 有完整的 URBA360 和 Full Report 数据
- URBA360 订单有效期到 2109-08-22（长期监控）
- Full Report 订单状态为 ready（已就绪）

---

## 四、接口调用流程（开发参考）

```
1. 获取 Token
   POST /authentication/v1/token
   Headers: x-api-key, Content-Type
   Body: {username, password, grant_type: "password"}
   → 返回 idToken

2. 搜索公司
   GET /companies?companyName={name}&countryCode={code}
   Headers: Authorization: Bearer {idToken}, x-api-key
   → 返回 companyId, externalIds

3. 获取 URBA360 内容（已有订单）
   GET /urba360/monitorings/orders?externalId={id}&countryCode={code}
   → 返回 monitoring order id
   
   GET /urba360/content?id={orderId}
   → 返回完整 URBA360 数据

4. 获取 Full Report 内容（已有订单）
   GET /publications/orders?externalId={id}&countryCode={code}
   → 返回订单列表（含 publicationId）
   
   GET /publications?id={publicationId}&format=json
   → 返回 Full Report 数据
```

---

## 五、问题记录

| 问题 | 状态 | 说明 |
|------|------|------|
| 403 Forbidden | ✅ 已解决 | 需要同时带 idToken + x-api-key |
| 401 Unauthorized | ✅ 已解决 | 使用 idToken 而非 accessToken |
| 端点路径错误 | ✅ 已解决 | 通过文档确认正确路径 |
| 数据缺失（注册资本） | ✅ 已确认 | URBA360中无注册资本，需从Full Report获取 |
| 数据缺失（诉讼记录） | ✅ 已确认 | URBA360中无诉讼记录，需从Full Report获取 |

---

## 六、下一步开发建议

1. **Token 管理**: 实现 Token 缓存和自动刷新（3600秒有效期）
2. **错误处理**: 处理 401/403 时自动重新获取 Token
3. **数据映射**: 建立 Coface 字段到 D365 实体的映射表
4. **货币转换**: 实现 EUR → USD 的汇率转换
5. **缺失值处理**: 定量指标缺失=-1，定性指标缺失='O'
6. **批量处理**: 504家大客户的数据批量导入
7. **监控更新**: 实现 URBA360 监控通知的定期轮询
