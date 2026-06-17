# Coface 测试数据汇总

> 用途：记录 Coface 测试环境可用的搜索/报告数据，方便前端、Plugin、接口调试时快速复用。
>
> 来源：科法斯测试沙盒开通邮件、2026-06-14 DEV1 弹窗搜索验证、API 接口测试结果。

---

## 测试环境账号

| 项目 | 值 |
|------|-----|
| Username | `tangys12@sany.com.cn` |
| Password | `1qaz!QAZ` |
| API Key | `0vneRg8vLjzPQlIfSkzO8kIDg04kfaKafTzg5sX1` |
| 认证 URL | `https://api.coface.com/authentication/v1` |
| 测试数据 URL | `https://icon-api-test.coface.com/dataapi-v1` |

---

## 测试数据 1：官方完整报告数据（PL）

用于验证 URBA360 / Full Report 完整数据流、BPP 回调回写等场景。

| 字段 | 值 |
|------|-----|
| **公司名称** | **TEST FOR SIX HYDRO BUDOWA-6 S.A.** |
| **Country Code** | **PL** |
| **ICON Number** | **icon#5415240** |
| 特点 | 有完整的 URBA360 和 Full Report 数据；URBA360 订单长期有效；Full Report 状态为 ready |

**适用验证：**
- CofaceDataSyncPlugin 数据同步
- URBA360 / Full Report 字段解析
- 评分卡标签生成

---

## 测试数据 2：弹窗搜索快速验证数据（DE）

用于快速验证【搜索 Coface 企业】弹窗能否正常返回候选企业列表。

| 字段 | 值 |
|------|-----|
| **英文名称** | **Sany** |
| **国家编码** | **DE** |
| 特点 | 返回约 20 条候选企业；适合前端 UI 和 Custom Action 联调 |

**适用验证：**
- `mcs_CofaceSearchCompany` Custom Action 调用
- `CofaceSearchCompanyPlugin` Step 是否正常触发
- 前端弹窗搜索、选择、绑定 Coface ID 流程

---

## 使用方式

### 弹窗搜索

在 `mcs_credit_record` 表单点击【搜索 Coface 企业】，输入：

```
英文名称: Sany
国家编码: DE
```

预期返回 20 条左右候选企业，选择后可绑定 `mcs_cofaceid`。

### 完整数据同步

创建/更新一条 `mcs_credit_record`，客户关联到 Country Code = PL 的 account，并确保 `mcs_cofaceid` = `icon#5415240`，触发 `CofaceDataSyncPlugin` 后可拉取完整报告数据。

---

## 相关文档

| 文档 | 路径 |
|---|---|
| 测试沙盒开通邮件和 API 地址 | `Documents/BusinessAnalysis/coface/30其他/科法斯测试沙盒开通环境邮件和API地址.md` |
| API 接口测试结果 | `Documents/BusinessAnalysis/coface/30其他/Coface_API接口测试结果.md` |
| 企业搜索弹窗 HTML | `Code/Customizations/WebResources/HTML/mcs_coface_company_search.html` |
| 企业搜索 Plugin | `Code/Customizations/Plugins/CofaceIntegration/Plugin/CofaceSearchCompanyPlugin.cs` |
| 数据同步 Plugin | `Code/Customizations/Plugins/CofaceIntegration/Plugin/CofaceDataSyncPlugin.cs` |
