# 融资管理 BPP 审批开发方案

> **状态**：最终确认版  
> **日期**：2026-07-16  
> **适用范围**：`mcs_fsm_data`（融资管理）的 BPP 立项审批与融资方案审批  
> **开发原则**：参考客户信用评估、厂端授信额度申请的 BPP 实现，保持最小改动、字段复用。

---

## 一、业务背景

`mcs_fsm_data`（融资管理）存在两种顺序执行的 BPP 审批：

| 审批类型 | 业务阶段 | 触发条件 | 通过后状态 |
|---------|----------|----------|-----------|
| 立项审批 | `mcs_fsm_status = 2`（融资立项） | `mcs_can_initiated = 1` | → 3（融资解决方案） |
| 融资方案审批 | `mcs_fsm_status = 3`（融资解决方案） | `mcs_can_project = 1` | → 4（融资落实），`mcs_is_valid = true` |

两种审批**顺序执行、互斥进行**，不能并行。上一个审批未结束前，下一个审批无法开始。

---

## 二、审批流程

```
状态1 融资需求
  ↓ 融资需求经理创建/编辑保存
状态2 融资立项（mcs_can_initiated = 1）
  ↓ 点击【提交立项审批】
状态2 + mcs_approve_type=1 + mcs_bppstatus=2（立项审批中）
  ↓ BPP 审批通过
状态3 融资解决方案（mcs_can_project = 1）
  ↓ 融资经理填写方案，点击【提交融资方案审批】
状态3 + mcs_approve_type=2 + mcs_bppstatus=2（方案审批中）
  ↓ BPP 审批通过
状态4 融资落实（mcs_is_valid = true）
```

---

## 三、元数据变更

### 3.1 新增字段

只在 `mcs_fsm_data` 上新增 1 个字段：

| 字段 | 类型 | 选项值 | 说明 |
|------|------|--------|------|
| `mcs_approve_type` | Picklist | 1 立项审批 / 2 融资方案审批 | 标识当前提交的是哪种审批 |

### 3.2 复用现有字段

`mcs_fsm_data` 已有 BPP 相关字段全部复用：

| 字段 | 用途 |
|------|------|
| `mcs_bppstatus` | BPP 审批状态（1 申请 / 2 审批中 / 3 通过 / 4 驳回） |
| `mcs_bppstatuscode` | BPP 原始状态（Submitted/Pending/Approved/Rejected/Withdrawn） |
| `mcs_bppid` | BPP 工作流 ID |
| `mcs_bppapprover` | 当前审批人 |
| `mcs_bpperrormsg` | BPP 错误信息 |
| `mcs_bpprejectreason` | BPP 驳回原因 |
| `mcs_approvedate` | 审批完成日期 |
| `mcs_fsm_data_url` | BPP 审批链接，用户直接点击跳转 |

**不新增**：`mcs_fsm_approve` 实体、`mcs_bppcomment`、`mcs_submit_time`、`mcs_submit_person` 等字段，保持与其他 BPP 审批实体一致。

---

## 四、D365 Plugin 端

### 4.1 新增文件

| 文件 | 触发 | 功能 |
|------|------|------|
| `Code/Customizations/Plugins/FinancingManagement/Bpp/FsmDataBppIntegrationPlugin.cs` | `mcs_fsm_data` Update PostOperation，Filter=`mcs_bppstatus` | 提交 BPP 审批 |
| `Code/Customizations/Plugins/FinancingManagement/Bpp/FsmDataBppCallbackPlugin.cs` | `mcs_fsm_data` Update PostOperation，Filter=`mcs_bppstatuscode` | 处理 BPP 回调 |

### 4.2 提交审批逻辑

触发条件：
- `mcs_bppstatus` 从非 2 变为 2
- 当前 `mcs_fsm_status` 为 2（立项审批）或 3（方案审批）
- `mcs_can_initiated = 1` 或 `mcs_can_project = 1`
- 当前没有审批在进行中（`mcs_bppstatus ≠ 2`，`mcs_bppstatuscode` 不为 Submitted/Pending）

Plugin 调用：
```csharp
mcs_bppstartapi(EntityId, EntityName, UserId)
```

### 4.3 回调处理逻辑

| 当前 `mcs_fsm_status` | `mcs_approve_type` | BPP 结果 | 业务动作 |
|----------------------|-------------------|----------|----------|
| 2 | 1 | Approved | → 3，`mcs_can_initiated = 0` |
| 2 | 1 | Rejected | `mcs_can_initiated = 1`，状态不变 |
| 3 | 2 | Approved | → 4，`mcs_is_valid = true`，`mcs_can_project = 0` |
| 3 | 2 | Rejected | `mcs_can_project = 1`，状态不变 |
| 任意 | 任意 | Withdrawn / Abandoned | `mcs_bppstatus = 1`，清空 `mcs_bppid`、`mcs_bppapprover` |

---

## 五、Service 项目端（MessageHandler）

### 5.1 新增/修改文件

| 文件 | 操作 | 说明 |
|------|------|------|
| `Service/SanyD365.Main/Entities/BPP/BPPHandlerServices/BPPHandlerServiceForFsmData.cs` | 新增 | 实现 `IBPPHandlerService`，参考 `BPPHandlerServiceForFcaQuotaApp.cs` |
| `Service/SanyD365.Main/StartupHelper.cs` | 修改 | 新增注册：`BPPHandlerServiceMain.Handler["mcs_fsm_data"] = DIContainerContainer.Get<BPPHandlerServiceForFsmData>();` |
| `Service/SanyD365.Main/DTO/BPP/BPP_WorkFlowTemplateCodeEntity.cs` | 修改 | 新增两个属性：`FsmDataInitiation`（立项审批）、`FsmDataProject`（融资方案审批） |

### 5.2 BPPHandlerServiceForFsmData 核心逻辑

#### 5.2.1 GetBppFormData

1. 查询 `mcs_fsm_data`，读取 `mcs_approve_type`
2. 根据 `mcs_approve_type` 选择 `TemplateCode`
3. 组装表单变量（5 个）：

| 表单字段 | 字段 Code | 数据来源 |
|---------|----------|----------|
| 融资管理编号 | `mcs_fsm_managment_no` | `mcs_fsm_data.mcs_fsm_managment_no` |
| D365 记录链接 | `mcs_fsm_data_url` | 拼接生成 |
| 客户名称 | `mcs_customer_name` | `mcs_fsm_data.mcs_customer_name` |
| 客户编码（SAP） | `mcs_customer_id` | `mcs_fsm_data.mcs_customer_id` |
| 申请人/融资经理 | `mcs_fsm_manager` | `mcs_fsm_data.mcs_fsm_manager` |

> BPP 表单只展示摘要信息，审批人点击 `mcs_fsm_data_url` 跳转 D365 查看完整记录详情。

#### 5.2.2 UpdateEntityStatusForStart

- 回写 `mcs_bppstatuscode = "Submitted"`
- 回写 `mcs_bpperrormsg = string.Empty`
- 回写 `mcs_fsm_data_url`
- 回写 `mcs_nextapprover`

#### 5.2.3 CallBack

- 回写 `mcs_bppstatuscode`
- 回写 `mcs_bpprejectreason`
- 回写 `mcs_approvedate`
- 回写 `mcs_nextapprover`

#### 5.2.4 PreStart

- 驳回后重新提交时，清理旧 `mcs_bppapply` 记录
- 清空 `mcs_bppid`、`mcs_bppstatuscode`、`mcs_fsm_data_url`

---

## 六、JS WebResource + App Action

### 6.1 新增文件

`Code/Customizations/WebResources/JS/mcs_fsm_data.js`

### 6.2 JS 函数

| 函数 | 行为 |
|------|------|
| `submitInitiationApproval()` | 校验 → `mcs_approve_type = 1` → `mcs_bppstatus = 2` |
| `submitProjectApproval()` | 校验 → `mcs_approve_type = 2` → `mcs_bppstatus = 2` |

### 6.3 前端校验

```javascript
// 提交立项审批
if (mcs_fsm_status !== 2) throw "只有融资立项状态才能提交立项审批";
if (mcs_can_initiated !== 1) throw "当前不允许提交立项审批";
if (mcs_bppstatus === 2) throw "已有审批在进行中";

// 提交融资方案审批
if (mcs_fsm_status !== 3) throw "只有融资解决方案状态才能提交方案审批";
if (mcs_can_project !== 1) throw "当前不允许提交方案审批";
if (mcs_bppstatus === 2) throw "已有审批在进行中";
```

### 6.4 App Action 按钮

| 按钮 | 位置 | 显隐规则 |
|------|------|----------|
| 提交立项审批 | 表单命令栏 | `mcs_fsm_status = 2` 且 `mcs_can_initiated = 1` |
| 提交融资方案审批 | 表单命令栏 | `mcs_fsm_status = 3` 且 `mcs_can_project = 1` |

**不新增**【查看审批】按钮，`mcs_fsm_data_url` 字段直接作为链接展示，用户点击跳转。

---

## 七、n8n 发布

| 修改内容 | n8n 勾选 |
|---------|----------|
| D365 Plugin（FsmDataBppIntegrationPlugin / FsmDataBppCallbackPlugin） | `McsPlugin` |
| Service 项目 SanyD365.Main（BPPHandlerServiceForFsmData） | `Messagehandler`（建议同时勾选 `ClientAPI` 保持版本一致） |

---

## 八、BPP 模板配置

已生成两个 Excel 模板文件，交给 BPP 团队配置：

| 文件 | 对应审批 | 表单字段 |
|------|---------|----------|
| `Documents/BPP/融资立项审批_BPP模板配置.xlsx` | 融资立项审批 | 融资管理编号、D365 记录链接、客户名称、客户编码、申请人 |
| `Documents/BPP/融资方案审批_BPP模板配置.xlsx` | 融资方案审批 | 同上 5 个字段 |

节点配置暂按“风控审核（待业务确认）”一个节点处理，操作类型：通过、退回。

### 待 BPP 团队返回

| 项 | 说明 |
|----|------|
| 立项审批 BPP TemplateCode | 配置到 `BPP_WorkFlowTemplateCodeEntity.FsmDataInitiation` 和 `BPP_WorkFlowTemplateCode` JSON |
| 融资方案审批 BPP TemplateCode | 配置到 `BPP_WorkFlowTemplateCodeEntity.FsmDataProject` 和 `BPP_WorkFlowTemplateCode` JSON |
| 最终审批节点/审批人规则 | 替换 Excel 中的占位节点配置 |

---

## 九、开发顺序

1. **Service 端**：`BPPHandlerServiceForFsmData.cs` + `StartupHelper.cs` 注册 + `BPP_WorkFlowTemplateCodeEntity.cs` 属性
2. **D365 元数据**：`mcs_fsm_data` 新增 `mcs_approve_type`
3. **D365 Plugin**：`FsmDataBppIntegrationPlugin.cs` + `FsmDataBppCallbackPlugin.cs`
4. **JS + App Action**：`mcs_fsm_data.js` + 2 个提交按钮
5. **DEV1 测试** → 远程编译 → PR 合并 → n8n 发布

---

## 十、状态判断参考

用户可通过以下字段组合判断当前审批状态：

| 字段组合 | 含义 |
|----------|------|
| 状态=2，`mcs_approve_type=1`，`mcs_bppstatus=2` | 立项审批中 |
| 状态=3，`mcs_approve_type=2`，`mcs_bppstatus=2` | 融资方案审批中 |
| 状态=4，`mcs_is_valid=true` | 全部审批完成，已生效 |

---

*本文档基于 `融资管理PRD.docx`、`融资管理数据表定义_v1.md` 及现有 BPP 实现（客户信用评估、厂端授信额度申请）整理。*
