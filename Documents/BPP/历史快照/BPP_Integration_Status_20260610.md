# BPP 集成状态纪要

> 日期：2026-06-10
> 目标：DEV1 / UAT（2合1测试环境）
> 当前状态：**Handler 代码已开发完成，未编译/未发布**，等待用户确认

---

## 1. 已完成的代码修改

### 1.1 我们项目内的 Plugin（已注册到 DEV1）

| 组件 | 实体 | 消息 | 阶段 | 筛选字段 | 状态 |
|------|------|------|------|----------|------|
| `BppIntegrationPlugin` | `mcs_credit_record` | Update | Post-operation | `mcs_status` | ✅ 已注册 |
| `BppCallbackPlugin` | `mcs_credit_record` | Update | Post-operation | `mcs_bppstatus` | ✅ 已注册 |
| 实体发布 | `mcs_credit_record` | - | - | - | ✅ 已发布 |

### 1.2 同事项目 `SanyD365.Main` 的 BPP Handler（刚修改，未发布）

| # | 文件 | 操作 | 状态 |
|---|------|------|------|
| 1 | `SanyD365.Main/DTO/BPP/BPP_WorkFlowTemplateCodeEntity.cs` | 新增 `CustomerCreditEvaluation` 属性 | ✅ 已修改 |
| 2 | `SanyD365.Main/Entities/BPP/BPPHandlerServices/BPPHandlerServiceForCreditRecord.cs` | 新建 Handler，实现 `IBPPHandlerService` | ✅ 已创建 |
| 3 | `SanyD365.Main/StartupHelper.cs` | 注册 `Handler["mcs_credit_record"] = ...` | ✅ 已修改 |

---

## 2. 当前错误与根因（已定位）

### 错误信息
```
找不到消息类型为mcs_credit_record的消息处理服务，
发生位置为SanyD365.Main.Entities.BPP.BPPHandlerServiceMain.IBPPHandlerService
```

### 根因
`BPPHandlerServiceMain` 内部维护一个静态字典 `Handler<string, IBPPHandlerService>`，按 `EntityName` 查找对应 Handler。`mcs_credit_record` 尚未注册该 Handler。

### BPP 发起流程（已确认）
1. `mcs_bppstartapi` 被调用
2. `AppBPPFlowService.SubmitBPP` 发送 SMessage 到消息队列
3. `SMessageListenerForBPPStartWorkflow` 消费
4. `BPPService.Start` 执行：
   - `PreStart()`
   - `GetBppFormData()` ← 这里需要 Handler
   - 调用 BPP 外部接口 `endpoint.Init()`
   - 保存 `mcs_bppapply`
   - 更新 `mcs_workflowid`、`mcs_nextapprover`
   - `UpdateEntityStatusForStart()`
5. 审批完成后 BPP 回调 D365，走 `CallBack()`

---

## 3. `BPPHandlerServiceForCreditRecord` 实现要点

### 3.1 依赖注入
- `ID365SystemConfigurationRepositoryCacheProxy _d365ConfigRepository` —— 读取系统配置
- `ICrmServiceGenerateService _crmServiceGenerateService` —— 获取 CRM Service 更新记录
- **未新建 Repository**，直接在 Handler 内联实现 `GetBppFormData`

### 3.2 `GetBppFormData`
- 读取 `BPP_WorkFlowTemplateCode` 配置，取 `CustomerCreditEvaluation` 模板 Code
- 查询 `mcs_credit_record` 记录
- 根据 `mcs_accountid` 关联查询 `account.accountnumber`
- 根据 `D365BaseUrl` 配置构造 D365 记录链接
- 组装 10 个表单变量：

| 表单变量名称 | 字段 Code | 来源字段 | 说明 |
|-------------|-----------|---------|------|
| 信用评估编号 | mcs_scoreid | `mcs_scoreid` | 字符串 |
| 申请人 | mcs_applicant | `mcs_applicant` | 字符串 |
| 客户名称 | mcs_custname | `mcs_custname` | 字符串 |
| 客户编码 | mcs_accountcode | `account.accountnumber` | 通过 lookup 关联查询 |
| 国家代码 | mcs_countrycode | `mcs_countrycode` | 字符串 |
| 信用分 | mcs_creditscore | `mcs_creditscore` | 数字（decimal） |
| 信用等级 | mcs_creditgrade | `mcs_creditgrade` | 字符串 |
| Coface 客户 ID | mcs_cofaceid | `mcs_cofaceid` | 字符串 |
| 审批人员 | mcs_approver | `string.Empty` | **TODO：待确认飞书账号来源** |
| D365 记录链接 | mcs_credit_record_url | 动态构造 | 格式：`{D365BaseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=mcs_credit_record&id={EntityId}` |

### 3.3 `UpdateEntityStatusForStart`
- 更新 `mcs_bppstatus` = `"Submitted"`
- 清空 `mcs_bpperrormsg`

### 3.4 `CallBack`
- `BPPCallBackDTO.Status` 是 int（11=驳回，30=完成）
- `mcs_bppstatus` 在 DEV1 是 **String**，需要做转换：
  - `30` → `"Approved"`
  - `11` → `"Rejected"`
- 有 `Reason` 时更新 `mcs_bpprejectreason`
- Status 为 30 或 11 时更新 `mcs_approvedate` = `DateTime.Now`

### 3.5 两层回调分工
1. **Handler `CallBack`**：更新 BPP 特定字段（`mcs_bppstatus`、`mcs_bpprejectreason`、`mcs_approvedate`）
2. **Plugin `BppCallbackPlugin`**：监听 `mcs_bppstatus` 变化，更新业务状态 `mcs_status`：
   - `Approved` / `30` → `mcs_status = 15`，更新 Account 信用信息
   - `Rejected` / `11` → `mcs_status = 12`
   - `Withdrawn` / `Abandoned` → `mcs_status = 12`

---

## 4. 发布前还需要做的事

### 4.1 代码层面
- [ ] 编译 `SanyD365.Main` 工程，确保无编译错误
- [ ] 部署编译后的 DLL 到 DEV1 环境

### 4.2 D365 配置层面
- [ ] 在 DEV1 的 `mcs_d365systemconfiguration` 实体中，找到 Name = `BPP_WorkFlowTemplateCode` 的记录
- [ ] 在 Content JSON 中新增：
  ```json
  "CustomerCreditEvaluation": "781094754802827383"
  ```
- [ ] 确认 `D365BaseUrl` 配置存在，Content 为当前环境 URL（如 `https://dev1.crm5.dynamics.com`）

### 4.3 待确认问题
- [ ] **审批人员 `mcs_approver` 数据来源**：当前代码写死为空字符串，需要确认从哪个字段取飞书账号
- [ ] BPP 回调是否真的能到达 DEV1（BPP 服务的回调地址配置是否正确）

---

## 5. 测试计划

1. 重新触发 `mcs_credit_record` 状态 13 → 14
2. 观察 `mcs_bpperrormsg` 是否清空、`mcs_bppstatus` 是否变为 `"Submitted"`、`mcs_workflowid` 是否有值
3. 在 BPP 平台完成/驳回审批
4. 观察 `mcs_bppstatus` 是否变为 `"Approved"` / `"Rejected"`
5. 观察 `BppCallbackPlugin` 是否正确更新 `mcs_status` 和 Account 信用字段
