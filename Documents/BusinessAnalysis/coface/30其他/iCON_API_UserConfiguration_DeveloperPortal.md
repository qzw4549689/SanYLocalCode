# iCON API User Configuration - Developer Portal

> 来源: 科法斯提供开发文档 iCON_API_UserConfiguration_DeveloperPortal
> 整理时间: 2026-06-04

---

## 1. Verification Email

Look out for an email from **no-reply@verificationemail.com**. It will guide you through connecting to the API Portal.

邮件内容包含：
- Developer Portal 邀请链接: https://developers.coface.com
- Username
- Temporary password

---

## 2. Access the API Portal

访问: https://developers.coface.com/

---

## 3. Credentials

使用邮件中提供的凭据登录。

---

## 4. Password Change

首次登录时，系统会提示修改密码。

---

## 5. AWS Gateway Login

密码更新后，将自动登录到 Coface APIs 的 AWS 网关。

初始着陆点是 **用户仪表板主页 (user dashboard homepage)**。

### 仪表板功能

在仪表板上可以：
- **Retrieve your API Key** - 获取 API Key
- **Access the list of APIs available to you** - 查看可用的 API 列表
- **Explore API products generally available on APIs** - 浏览 API 产品

### API Keys 说明

> **Important Note**: Currently, within your API Keys, you will find a single section labeled "Production." It is important to note that for the iCON API, the same API Key from Production can also be utilized in Sandbox, even if not explicitly specified here.

**关键信息：**
- API Keys 中只显示 "Production" 环境
- 但 **同一个 API Key 可以同时用于 Production 和 Sandbox 环境**
- 无需为 Sandbox 单独申请 API Key

---

## 6. API 认证流程

### 获取 JWT Token

1. 在 DOCS 页面找到 "Authentication" 部分
2. 参考详细说明获取 token

**认证方式：**
- API Key + JWT Token
- Token 用于认证请求

### 使用 iCON Data API

1. 在 DOCS 页面找到 "iCON Data API" 部分（第4节）
2. 探索技术文档

**iCON Data API 包含的模块：**
- Assessment (评估)
- Country (国家)
- Company (公司)
- Monitoring (监控)
- Notification (通知)
- Order (订单)

---

## 关键配置信息

| 项目 | 值 |
|------|-----|
| Developer Portal | https://developers.coface.com |
| API Key | 在 Dashboard 中获取（Production/Sandbox 共用） |
| 认证方式 | API Key + JWT Token |
| 文档入口 | DOCS → Authentication / iCON Data API |

---

## 开发步骤总结

```
1. 接收验证邮件
   → 获取 Developer Portal 链接和临时密码

2. 登录 Developer Portal
   → https://developers.coface.com

3. 修改密码
   → 首次登录强制修改

4. 获取 API Key
   → Dashboard → Your API Keys → Production
   → 同一个 Key 用于 Production 和 Sandbox

5. 获取 JWT Token
   → DOCS → Authentication
   → 使用 API Key + 用户名密码获取 Token

6. 调用 iCON Data API
   → DOCS → iCON Data API
   → 使用 Token 调用具体接口
```

---

## 注意事项

1. **同一个 API Key 可用于 Production 和 Sandbox**
2. **Token 有有效期**，需要定期刷新
3. 所有 API 请求必须通过 **HTTPS**
4. 详细接口文档在 DOCS 页面中查看
