# 科法斯测试沙盒开通环境邮件和API地址

> 来源: 科法斯测试沙盒开通环境邮件
> 整理时间: 2026-06-04

---

## 0. 账号信息

| 环境 | Username | 密码 |
|------|----------|------|
| 测试沙盒 | tangys12@sany.com.cn | 1qaz!QAZ |
| 生产环境 | tangys12@sany.com.cn | 联系三一IT王中雄或唐涌珊 |

> 2026.4.27 科法斯测试沙盒 Username: tangys12@sany.com.cn 密码: 1qaz!QAZ

---

## 1. 账户配置产品

已为账户开通科法斯API测试环境，开发账户激活链接已发出。

**配置的产品：**

| 产品 | 覆盖指标 | 说明 |
|------|---------|------|
| **URBA 360** | 9个指标 | 覆盖SANY评分卡所需数据 |
| URBA with monitoring | - | 带监控的URBA |
| URBA Full Report | 3个指标 | ICON Full Report (with or without monitoring) |
| Full Report CEE | - | 受限国家专用（如俄罗斯） |

**用户手册：**
- 配置和使用账户以及接口文档入口参见手册: `iCON_API_UserConfiguration_DeveloperPortal.pdf`
- 在线手册: https://coface.github.io/utilities/HowToUse_DataAPI.pdf

---

## 2. 测试数据

**测试环境使用非真实数据**

| 项目 | 值 |
|------|-----|
| 测试公司名称 | TEST FOR SIX HYDRO BUDOWA-6 S.A. |
| Country Code | PL |
| ICON Number | icon#5415240 |

> 如需更多测试数据请联系获取

---

## 3. 接口规范与地址

### 安全验证

- **协议**: OAuth 2.0 Password Grant 身份验证协议
- **方式**: API key + JWT token
- **Token 有效期**: 60分钟

### API URL

#### 认证地址（测试=生产）

| 环境 | URL |
|------|-----|
| 测试环境 | https://api.coface.com/authentication/v1 |
| 生产环境 | https://api.coface.com/authentication/v1 |

#### 数据交互地址

| 环境 | URL |
|------|-----|
| 测试环境 | https://icon-api-test.coface.com/dataapi-v1 |
| 生产环境 | https://api.coface.com/bi/icon-data/v1 |

---

## 4. 主要产品格式与样例

| 产品 | 格式 | 产品类型 | 数据样例 |
|------|------|---------|---------|
| Full Reports | JSON, HTML, PDF, XML | Report | Full Report JSON, Full Report PDF |
| Full Report CEE | JSON, HTML, PDF, XML | Report | - |
| URBA360 full report | JSON, HTML, PDF, XML | Report | Full Report Urba JSON, Full Report Urba PDF |
| **URBA360** | **JSON** | **URBA** | URBA360 JSON |

### 参考链接

- URBA360 JSON 样例: https://coface.github.io/sample/DemoCompany_URBA360.json
- Full Report PDF 样例: https://coface.github.io/sample/data-api-full-report-pdf.pdf
- URBA Full Report PDF 样例: https://coface.github.io/sample/urba-fullreport-pdf.pdf

---

## 5. 重要说明

1. **除以下国家外，商业报告提供两种格式**（JSON + PDF）
   - 例外国家列表: `CLA-CS_countries.xlsx`（39个受限国家）
   - 这些国家需要 JSON 和 PDF **分开下单**

2. **URBA360 目前仅提供 JSON 格式**

3. **俄罗斯使用 Full Report CEE 产品**

---

## 6. API 调用流程（整合）

```
1. 获取 Token
   POST https://api.coface.com/authentication/v1
   { username, password, grant_type: "password" }
   → 返回 access_token (有效期60分钟)

2. 调用数据接口
   Header: Authorization: Bearer {access_token}
   
   URBA360:
   GET https://icon-api-test.coface.com/dataapi-v1/urba360/orders
   GET https://icon-api-test.coface.com/dataapi-v1/urba360/content?id={id}
   
   Full Report:
   POST https://icon-api-test.coface.com/dataapi-v1/publications/orders
   GET https://icon-api-test.coface.com/dataapi-v1/publications?id={id}&format=json
```

---

## 7. 配置信息汇总

| 配置项 | 测试环境 | 生产环境 |
|--------|---------|---------|
| 认证URL | https://api.coface.com/authentication/v1 | https://api.coface.com/authentication/v1 |
| 数据URL | https://icon-api-test.coface.com/dataapi-v1 | https://api.coface.com/bi/icon-data/v1 |
| Username | tangys12@sany.com.cn | tangys12@sany.com.cn |
| Password | 1qaz!QAZ | 联系IT获取 |
| Token有效期 | 60分钟 | 60分钟 |

---

## 8. 开发注意事项

1. **Token 管理**: 每次调用前检查 token 是否过期，过期则重新获取
2. **测试数据**: 使用 icon#5415240 (PL) 进行测试
3. **受限国家**: 39个国家需要分开下单 JSON 和 PDF
4. **数据准备时间**: Full Report 需要 6-7 个工作日准备
5. **货币换算**: 数据默认当地货币，需根据汇率表转换为 USD
