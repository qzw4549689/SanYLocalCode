# BPP 代码推送/部署到 UAT 注意事项

> 日期：2026-06-10
> 目标环境：UAT（用户验收测试环境）
> 说明：UAT D365 与 DEV1 D365 是两个独立环境，但共用同一个 UAT BPP 服务

---

## 一、代码层面

### 1.1 确认推送范围
需要确保以下 3 个文件的修改完整推送到 UAT 代码分支：

| # | 文件 | 变更类型 |
|---|------|----------|
| 1 | `SanyD365.Main/DTO/BPP/BPPFormData.cs` | 新增 `CustomerCreditEvaluation` 属性 |
| 2 | `SanyD365.Main/Entities/BPP/BPPHandlerServices/BPPHandlerServiceForCreditRecord.cs` | 新建 Handler |
| 3 | `SanyD365.Main/StartupHelper.cs` | 注册 `Handler["mcs_credit_record"]` |

> ⚠️ 如果 UAT 分支与 DEV 分支有差异，注意 **merge conflict**，特别是 `StartupHelper.cs`（多人可能都在加 Handler 注册）。

### 1.2 编译
- UAT 部署的 DLL 必须是 **Release 配置**
- 确保 `SanyD365.Main` 在 UAT 分支上编译通过（0 error, 0 warning）

---

## 二、D365 系统配置（UAT 环境）

### 2.1 BPP_WorkFlowTemplateCode
UAT 的 `mcs_d365systemconfiguration` 表中，Name = `BPP_WorkFlowTemplateCode` 的 JSON Content 必须包含：

```json
"CustomerCreditEvaluation": "781094754802827383"
```

> 注意：UAT 的模板 Code 值可能与 DEV1 相同（因为 BPP 服务是同一个 UAT），但需要向 BPP 团队确认。

### 2.2 D365BaseUrl
UAT 环境的 D365BaseUrl 必须指向 **UAT 的 D365 地址**，不是 DEV1。

| 环境 | D365BaseUrl 示例 |
|------|------------------|
| DEV1 | `https://dev1.crm5.dynamics.com` |
| UAT | `https://sanyglobal-test.crm.dynamics.com`（以实际 UAT URL 为准）|

### 2.3 Bpp_ApprovalFlowBaseUrl
UAT 的 BPP 门户地址通常和 DEV1 相同（都是 UAT BPP 服务），但需确认：
```
https://sanybpp-portal-uat.sany.com.cn/approval-form?orgId=3&instanceId=
```

---

## 三、Plugin 注册（UAT 环境）

### 3.1 需要注册的 Plugin

| Plugin | 实体 | 消息 | 阶段 | 筛选属性 |
|--------|------|------|------|----------|
| `BppIntegrationPlugin` | `mcs_credit_record` | Update | Post-operation | `mcs_status` |
| `BppCallbackPlugin` | `mcs_credit_record` | Update | Post-operation | `mcs_bppstatus` |

### 3.2 注册方式
- 如果通过 **Solution 导入**：确保 Solution 中包含 Plugin Assembly 和 Step
- 如果通过 **Plugin Registration Tool (PRT)**：手动注册 Assembly + Step，注意 Stage 和 Filtering Attributes

### 3.3 版本管理
- UAT 注册时，如果 Assembly 已存在，选择 **Update** 而非新建
- 确保 Sandbox 模式勾选

---

## 四、实体与字段（UAT 环境）

### 4.1 字段一致性检查
确认 UAT 的 `mcs_credit_record` 实体字段与 DEV1 一致，特别是：

| 字段 | 类型 | 必须存在 |
|------|------|----------|
| `mcs_bppstatus` | String | ✅ |
| `mcs_workflowid` | String | ✅ |
| `mcs_bpperrormsg` | Memo | ✅ |
| `mcs_bpprejectreason` | Memo | ✅ |
| `mcs_approvedate` | DateTime | ✅ |
| `mcs_nextapprover` | String | ✅ |
| `mcs_creditscore` | Decimal | ✅ |
| `mcs_creditgrade` | String | ✅ |

### 4.2 实体发布
注册 Plugin 后，必须在 UAT **发布 `mcs_credit_record` 实体**的自定义项。

---

## 五、数据准备（UAT 环境）

### 5.1 测试数据要求
UAT 测试前，确保有至少一条完整的 `mcs_credit_record` 记录：

| 字段 | 要求 |
|------|------|
| `mcs_status` | `13`（初筛通过） |
| `mcs_creditscore` | 有值（如 60） |
| `mcs_creditgrade` | 有值（如 A1） |
| `mcs_accountid` | 关联有效的 Account |
| `mcs_applicant` | 有值 |
| `mcs_countrycode` | 有值（如 PL） |
| `mcs_cofaceid` | 有值（可选） |

### 5.2 关联数据
- Account 记录必须有 `accountnumber`（客户编码）
- Account 名称完整（用于 BPP 表单显示）

---

## 六、BPP 回调地址（关键）

### 6.1 确认回调配置
BPP 服务（UAT）审批完成后，会回调 D365 更新 `mcs_bppstatus`。

**必须确认**：BPP UAT 服务的回调地址配置指向的是 **UAT 的 D365**，不是 DEV1。

如果 BPP 回调地址配置错误：
- UAT 发起的审批，回调可能更新到 DEV1
- 或 UAT 的 `mcs_bppstatus` 永远不变

### 6.2 验证方法
UAT 部署后，发起一条审批，观察：
- UAT 的 `mcs_bppstatus` 是否从 `Submitted` → `Approved`/`Rejected`
- 如果不变，检查 BPP 回调地址配置

---

## 七、权限检查

### 7.1 执行账号
确保 UAT 中执行 BPP 流程的账号（调用 `mcs_bppstartapi` 的用户）有权限：
- 读取 `mcs_credit_record`
- 读取 `account`
- 读取 `mcs_d365systemconfiguration`（系统配置）
- 创建/更新 `mcs_bppapply`

### 7.2 Plugin 执行上下文
Plugin 在 Sandbox 模式下运行，确保 UAT 的隔离沙箱策略不会阻止外部 HTTP 调用（BPP 服务调用）。

---

## 八、部署顺序建议

```
1. 推送代码到 UAT 分支
2. 编译 SanyD365.Main (Release)
3. 部署 SanyD365.Main DLL 到 UAT
4. 注册/更新 Plugin Assembly (BppIntegration + BppCallback)
5. 配置 D365 系统配置 (BPP_WorkFlowTemplateCode, D365BaseUrl)
6. 发布 mcs_credit_record 实体
7. 准备测试数据（完整评分记录）
8. 发起审批测试
9. 验证 BPP 回调
```

---

## 九、Rollback 预案

如果 UAT 部署后出现问题：

| 问题 | 回滚方式 |
|------|----------|
| Handler 报错 | 从 UAT 分支 revert 3 个文件，重新编译部署 |
| Plugin Step 冲突 | 在 PRT 中注销 Step 或回滚 Assembly 版本 |
| 配置错误 | 恢复 UAT 的 `BPP_WorkFlowTemplateCode` JSON 备份 |
| BPP 回调异常 | 联系 BPP 团队暂停回调，避免脏数据 |

---

## 十、与 DEV1 的关键差异清单

| 项目 | DEV1 | UAT |
|------|------|-----|
| D365 URL | `https://dev1.crm5.dynamics.com` | 以实际为准 |
| D365BaseUrl 配置 | DEV1 地址 | **UAT 地址** |
| BPP 模板 Code | 781094754802827383 | 需确认是否相同 |
| BPP 回调地址 | 指向 DEV1 D365 | **必须指向 UAT D365** |
| 测试数据 | 可随意创建/修改 | 注意保护已有 UAT 数据 |
| 其他用户 | 只有你 | 可能有其他测试人员 |
