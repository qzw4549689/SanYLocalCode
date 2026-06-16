# D365 ↔ BPP 审批交互全流程详解

> 基于当前D365环境探测结果和BppIntegrationPlugin实现整理
> 最后更新：2026-06-10
>
> ⚠️ 重要修正（2026-06-10）：当前三一BPP框架不存在 `IBPPHandlerService` / `BPPHandlerServiceMain` 机制，
> 业务模块只需调用 `mcs_bppstartapi`，回调通过监听 `mcs_bppstatus` 字段变更处理。

---

## 一、整体交互概览

```
┌─────────────┐      ┌──────────────────┐      ┌─────────────┐
│   D365前端   │ ──▶  │ D365 BPP框架      │ ──▶  │  BPP平台     │
│  (用户操作)  │      │ (自定义API+Plugin) │      │ (审批引擎)   │
└─────────────┘      └──────────────────┘      └─────────────┘
       ▲                                              │
       │                                              │
       │         ┌──────────────────┐                │
       └──────── │ BPP回调/消息消费  │ ◀──────────────┘
                 │ (SMessageListener) │
                 └──────────────────┘
```

---

## 二、阶段详解

### 阶段1：触发审批（D365前端 → D365 BPP框架）

```
用户点击【提交审批】
    │
    ▼
mcs_credit_record.mcs_status = 13 → 14
    │
    ▼
【BppIntegrationPlugin 触发】
  触发条件: Update / PostOperation / mcs_status=14
  深度检查: Depth <= 2（防递归）
  重复检查: 已有workflowid且状态Submitted/Pending则跳过
    │
    ▼
【调用 mcs_bppstartapi】
  输入参数:
    EntityId  = 评估记录Guid
    EntityName= "mcs_credit_record"
    UserId    = 当前操作用户Guid（context.InitiatingUserId）
    │
    ▼
【D365 BPP框架处理（同步）】
  实现方: SanyD365.D365ExtensionApi.Apis.BppStartApis
  返回: {"Result":true,"Description":"Operation successful!",...}
    │
    ▼
【D365 BPP框架处理（异步）】
  SMessageListener消费消息
    │
    ▼
  查找IBPPHandlerService实现
    ├─ 找到 → 调用GetBppFormData()获取表单数据 → HTTP调用BPP平台
    └─ 未找到 → 报错：找不到消息类型为mcs_credit_record的消息处理服务
```

**⚠️ 当前状态：**
- ✅ mcs_bppstartapi调用成功（返回`Operation successful`）
- ❌ 异步SMessageListener找不到mcs_credit_record的IBPPHandlerService

---

### 阶段2：BPP平台审批（BPP平台内部）

```
BPP平台接收审批请求
    │
    ▼
【创建审批实例】
  - 生成BPP工作流ID（instanceId）
  - 绑定业务数据（客户信息、信用分等）
  - 确定审批路由（根据金额/等级等规则）
    │
    ▼
【审批流转】
  状态: Submitted → InReview → ... → Approved/Rejected
    │
    ▼
【各级审批人操作】
  审批人登录BPP后台查看待办 → 审批通过/驳回 → 填写审批意见
```

**BPP平台地址：**
- 审批后台：`https://sanybpp-admin-uat.sany.com.cn/workplace`
- 审批页面：`https://sanybpp-portal-uat.sany.com.cn/approval-form?instanceId={workflowid}&orgId=3`

---

### 阶段3：审批结果回传（BPP平台 → D365）

BPP平台通过回调接口通知D365审批结果：

| 回调类型 | 接口 | 功能 | D365处理 |
|---------|------|------|---------|
| 审批完成 | `BppController#CommonCallBack` | 审批通过/驳回/撤回/废弃 | 更新mcs_status、mcs_bppstatus |
| 节点历史 | `BppController#History` | 返回历史节点和下一节点审批人 | 更新mcs_nextapprover |
| 数据回写 | `BppController#DataWriteBack` | 审批中填写的数据回写 | 更新业务实体字段 |

**状态流转规则（已修正）：**
- 审批通过 → 状态15（审批通过），mcs_active=true
- 审批驳回 → 状态**12（人工复核）**，可重新提交
- 审批撤回 → 状态**12（人工复核）**
- 审批废弃 → 状态**12（人工复核）**

> ⚠️ 之前设计驳回到16是错误的，根据BPP对接手册，驳回后应回到12（人工复核）重新修改后提交

---

### 阶段4：审批通过后处理（D365内部）

```
状态变更为15（审批通过）
    │
    ▼
【更新客户主数据】
  实体: Account
  字段:
    mcs_creditscore  = 评估记录的信用分
    mcs_creditgrade  = 根据信用分计算等级(A0-A4)
    mcs_creditvalid  = true (有效)
    │
    ▼
【评估记录变为有效】
  mcs_active = true
```

---

## 三、D365已注册的BPP相关组件

### 自定义API（6个）

| API名称 | 功能 | 输入参数 | 输出参数 |
|---------|------|---------|---------|
| `mcs_bppstartapi` | 发起BPP审批 | EntityId, EntityName, UserId | Result |
| `mcs_bppstartapiv2` | 发起BPP审批(v2) | EntityId, EntityName, UserId | Result |
| `mcs_bppcheckapi` | 查询BPP审批状态 | EntityId, EntityName | Result |
| `mcs_bppwithdrawapi` | 撤回BPP审批 | EntityId, EntityName | Result |
| `mcs_bppabandonapi` | 放弃BPP审批 | EntityId, EntityName | Result |
| `mcs_ConsultationOrderRevokeApprovalBPPApi` | 撤销审批(咨询订单) | - | - |

### BPP字段（mcs_credit_record实体）

| 字段名 | 类型 | 用途 | 填充时机 |
|--------|------|------|---------|
| `mcs_workflowid` | String(100) | BPP流程实例ID | 审批发起成功后 |
| `mcs_nextapprover` | String(100) | 当前审批人（飞书账号） | 审批过程中更新 |
| `mcs_bppstatus` | String(30) | BPP审批状态 | 实时更新 |
| `mcs_bpperrormsg` | Memo(1000) | BPP错误信息 | 发起失败时 |
| `mcs_bpprejectreason` | Memo(1000) | 驳回原因 | 审批拒绝时 |
| `mcs_approvedate` | DateTime | 审批完成日期 | 审批完成时 |

### Plugin Assembly

| Assembly | 类型 | 说明 |
|----------|------|------|
| `SanyD365.Plugins.BppIntegration` | BppIntegrationPlugin | 本项目开发，状态14触发，调用mcs_bppstartapi |
| `SanyD365.D365ExtensionApi.Sales` | BppStartApis等 | 三一内部通用BPP框架，实现自定义API |

---

## 四、domainaccount问题解决记录

### 问题现象
调用mcs_bppstartapi时报错：
> `The domainaccount of the account (...) cannot be empty!`

### 根因分析
BPP框架通过`QueryPersonnelDomainAccountById_Fetch`查询用户的domainaccount，查询逻辑：
1. 用D365 UserId查询`mcs_personnel`记录
2. 从personnel记录取`mcs_domainaccount`字段
3. 如果找不到personnel记录或domainaccount为空，则报错

### 解决步骤
1. **查询mcs_useraccount**：确认当前用户是否有`mcs_useraccount`记录
   - 当前用户已有：`fdaaffb6-ab3c-4bb7-9717-4b03c6389bad`
   - `mcs_systemuserid`指向当前D365用户

2. **创建mcs_personnel记录**：
   ```
   mcs_name = "# 邱正卫"
   mcs_domainaccount = "gw_qiuzw"
   mcs_feishuemail = "gw_qiuzw@sany.com.cn"
   mcs_email = "gw_qiuzw@dealersany.com.cn"
   mcs_systemuserid → systemuser(当前用户)
   mcs_systemuseraccount → mcs_useraccount(fdaaffb6...)
   ```

3. **验证**：mcs_bppstartapi调用成功，返回`Operation successful`

### 关键教训
- D365用户要对接BPP，**必须**有对应的`mcs_personnel`记录
- `mcs_personnel.mcs_systemuserid`必须指向D365 systemuser
- `mcs_personnel.mcs_systemuseraccount`必须指向`mcs_useraccount`（不是systemuser）
- `mcs_personnel.mcs_domainaccount`必须填写飞书账号（@前部分）

---

## 五、IBPPHandlerService注册问题（当前阻塞）

### 问题现象
SMessageListener异步处理时报错：
> `找不到消息类型为mcs_credit_record的消息处理服务，发生位置为SanyD365.Main.Entities.BPP.BPPHandlerServiceMain.IBPPHandlerService`

### 根因分析
根据BPP对接手册，需要：
1. 实现`IBPPHandlerService`接口
2. 在`StartupHelper`中注册：
   ```csharp
   BPPHandlerServiceMain.Handler["mcs_credit_record"] = DIContainerContainer.Get<BPPHandlerServiceForCreditRecord>();
   ```

### 已实现的BPPHandlerServiceForCreditRecord
包含方法：
- `GetBppFormData(Guid entityId)`：封装BPP流程发起参数（模板Code、表单变量、流程变量）
- `CallBack(string action, Guid entityId, Dictionary<string, object> data)`：处理审批结果回调
- `UpdateEntityStatusForStart(Guid entityId, string workflowId, string nextApprover)`：发起成功回调
- `RollbackEntityStatusForFail(Guid entityId, string error)`：发起失败回滚

### 阻塞原因
- `BPPHandlerServiceMain`不在任何D365 Plugin Assembly中（可能在核心框架Assembly里）
- 无法通过Plugin代码可靠注册Handler
- D365 sandbox中反射注册到错误的BPPHandlerServiceMain实例

### 需要IT团队协助
1. 确认`BPPHandlerServiceMain`的实际位置和注册方式
2. 其他模块（如CompanyInspection、PaymentLoss）的Handler是在哪里注册的？
3. 是否需要在特定Assembly中添加注册代码？

---

## 六、表单变量传递说明

### 审核内容传递方式
BPP审批页面展示的信息通过**表单变量**传递：

```csharp
// GetBppFormData返回的表单变量（key需与BPP模板字段Code对齐）
var formData = new Dictionary<string, object>
{
    ["custName"] = "客户名称",
    ["creditScore"] = "60",
    ["creditGrade"] = "A2",
    ["cofaceId"] = "Coface报告ID",
    ["countryCode"] = "PL",
    ["applicant"] = "申请人"
};
```

### 当前状态
- 表单变量key是**暂定占位符**，等BPP团队搭建好模板后需要对齐
- 模板Code也是占位符：`TEST_CREDIT_EVALUATION`

---

## 七、时序图

```
用户            D365前端          BppIntegrationPlugin    D365 BPP框架      BPP平台
 │                  │                    │                  │                │
 │─点击【提交审批】─▶│                    │                  │                │
 │                  │─状态13→14保存────▶│                  │                │
 │                  │                    │─调用mcs_bppstartapi─────────────▶│
 │                  │                    │                  │                │
 │                  │                    │◀─返回成功───────│                │
 │                  │◀─事务提交──────────│                  │                │
 │◀─显示"已提交"────│                    │                  │                │
 │                  │                    │                  │                │
 │                  │                    │      【异步处理】 │                │
 │                  │                    │                  │─查找Handler────│
 │                  │                    │                  │ ❌未找到       │
 │                  │                    │                  │                │
 │                  │         【BPP平台内部审批流转】        │                │
 │                  │                    │                  │                │
 │                  │                    │                  │◀─审批完成─────│
 │                  │                    │                  │                │
 │                  │◀─回调更新状态────│◀─CommonCallBack──│                │
 │                  │                    │                  │                │
 │◀─显示审批结果────│                    │                  │                │
```

---

## 八、待确认事项

| 编号 | 问题 | 影响 | 状态 |
|------|------|------|------|
| BPP-001 | ✅ domainaccount用户映射 | 已解决 | ✅ 通过创建personnel记录 |
| BPP-002 | ~~IBPPHandlerService注册方式~~ | ~~阻塞异步处理~~ | ❌ 不存在该机制，已从代码中移除 |
| BPP-003 | BPP模板Code | GetBppFormData需要 | ⬜ 等BPP团队搭建模板 |
| BPP-004 | 表单变量字段Code对齐 | 审批页面展示内容 | ⬜ 等BPP团队给Code清单 |
| BPP-005 | BPP回调Controller是否已部署 | 审批结果回传 | ⬜ 待确认 |
| BPP-006 | 审批通过后客户主数据更新 | Account表更新 | ✅ 已开发（BppCallbackPlugin中实现），待测试 |

---

## 九、关键结论

1. **D365侧已就绪**：
   - ✅ BppIntegrationPlugin已重构，能正确调用mcs_bppstartapi
   - ✅ BppCallbackPlugin已新增，监听mcs_bppstatus变更处理回调
   - ❌ ~~BPPHandlerServiceForCreditRecord已删除~~（当前BPP框架不存在该机制）
   - ✅ domainaccount问题已解决
   - ✅ mcs_workflowid/mcs_nextapprover字段已创建并添加到表单
   - ✅ 审批通过后Account表更新逻辑已实现

2. **当前阻塞**：
   - ⏸️ 待测试环境注册Plugin Step并验证完整流程
   - ⬜ BPP模板未搭建，表单变量和模板Code待定

3. **后续重点**：
   - 测试环境注册BppCallbackPlugin Step → 验证审批发起+回调
   - BPP团队搭建模板并提供模板Code+表单变量Code → 对齐D365代码
   - 确认BPP平台实际回传的状态值格式（字符串 vs 数字）
