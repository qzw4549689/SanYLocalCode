# BPP 审批流程 - 测试环境对接方案

> 来源：BPP对接手册.docx  
> 整理日期：2026-06-08  
> 适用环境：测试环境 (UAT)

---

## 一、环境信息

| 环境 | 地址 | 说明 |
|------|------|------|
| BPP前端(用户界面) | https://sanybpp-uat.sany.com.cn/ | 审批人操作界面 |
| BPP后台管理 | https://sanybpp-admin-uat.sany.com.cn/workplace | 流程模板配置，需开权限 |
| BPP审批地址(外网打不开) | https://sanybpp-portal-uat.sany.com.cn/approval-form?instanceId={mcs_workflowid}&orgId=3 | 前端拼接，instanceId=mcs_workflowid |

> **注意**：测试环境审批地址外网打不开，需内网或VPN访问

---

## 二、D365实体字段配置

对接BPP的业务实体（mcs_credit_record）需添加以下字段：

| 字段名 | 显示名 | 类型 | 说明 |
|--------|--------|------|------|
| mcs_workflowid | BPP流程实例ID | String | 发起成功后BPP返回，用于拼接审批地址 |
| mcs_nextapprover | 当前审批人 | String | 飞书账号，如：unrj5、zhaow328 |
| mcs_bpperrormsg | BPP发起错误信息 | String | 发起失败时写入错误信息 |
| mcs_bppstatus | BPP流程状态 | String | BPP状态码 |
| mcs_bpprejectreason | BPP驳回原因 | String | 审批驳回时回填 |

> 当前 mcs_credit_record 实体已包含以上字段（开发计划中2.2.2已定义）

---

## 三、流程模板配置（测试环境）

### 3.1 流程分类
- 流程模板存放于 **"新海外客户关系管理系统"** 分类下
- 新建流程时复制基础流程模板，不要从零创建

### 3.2 关键配置
- 流程图必须包含 **"回调节点"**（最后一个节点，不可改动）
- 模板Code存放于**系统配置**中，测试/生产环境Code不一致
- 上生产时必须配置**生产模板Code**

### 3.3 变量配置
| 类型 | 用途 | 注意事项 |
|------|------|---------|
| 流程变量 | 流程图分支判断 | 不要和表单变量code重复 |
| 表单变量 | 审批页展示信息 | 关联流程表单产生 |

### 3.4 数据格式
- BPP仅支持 **字符串** 和 **数字** 两种格式
- **无布尔类型**，流程变量传值推荐使用字符串
- 行项目code取值一定要选中全部表格

---

## 四、代码对接步骤

### 4.1 实现 IBPPHandlerService 接口

新建类实现以下接口（参考：BPPHandlerServiceForNationlPrice）：

| 接口方法 | 功能 | 说明 |
|---------|------|------|
| `GetBppFormData` | 封装发起参数 | 流程标题、模板Code、表单变量(json)、流程变量(map) |
| `GetFileData` | 附件信息 | 传入文件流，使用 `IUploadFileInfoHandleService#GetBytes` |
| `CallBack` | 回调统一处理 | 处理：撤回、驳回、废弃、审批通过 → 变更实体状态 |
| `UpdateEntityStatusForStart` | 发起成功回调 | 更改实体业务状态 |
| `RollbackEntityStatusForFail` | 发起失败回滚 | 回滚单据状态 |

### 4.2 类注册

在 `StartupHelper` 中注册：
```csharp
BPPHandlerServiceMain.Handler["mcs_credit_record"] = DIContainerContainer.Get<BPPHandlerServiceForCreditRecord>();
```

---

## 五、API接口清单

### 5.1 流程发起

| 项目 | 内容 |
|------|------|
| API | `mcs_bppstartapi` |
| 机制 | 发送消息 → `SMessageListenerForBPPStartWorkflow` 监听消费 |
| 成功回调 | 调用 `UpdateEntityStatusForStart` |
| 重提判断 | 根据 `mcs_workflowid` 是否有值：无值=发起，有值=重提 |

**参数**：
- `EntityId`：实体ID
- `EntityName`：实体名称（mcs_credit_record）
- `UserId`：发起人飞书账号（从 mcs_personnel.mcs_feishuemail 取@前部分）

### 5.2 流程撤回

| 项目 | 内容 |
|------|------|
| API | `mcs_bppwithdrawapi` |
| 机制 | 校验可否撤回 → 发送消息 → `SMessageListenerForBPPWithDrawWorkflow` 消费 |
| 回调 | 调用 `CallBack` 修改业务状态 |

**参数**：`EntityId`, `EntityName`

### 5.3 流程废弃

| 项目 | 内容 |
|------|------|
| API | `mcs_bppabandonapi` |
| 限制 | 已完成不能废弃 |
| 机制 | 直接调BPP接口废弃，同步调用 |
| 回调 | 默认按撤回逻辑处理，调用 `CallBack` |

### 5.4 流程状态校验

| 项目 | 内容 |
|------|------|
| API | `mcs_bppcheckapi` |
| 返回值 | `true` / `false` |

**参数**：`EntityId`, `EntityName`

### 5.5 审批完成回调

| 项目 | 内容 |
|------|------|
| 接口 | `BppController#CommonCallBack` |
| 数据 | BPP回传数据放在 `Data` 中 |

### 5.6 节点历史回调

| 项目 | 内容 |
|------|------|
| 接口 | `BppController#History` |
| 功能 | 返回历史节点+下一节点审批信息 |
| 存储 | 记录到 SQLServer `BppHistory` 表 |
| 更新 | 下一节点审批人写入 `mcs_nextapprover` |
| 驳回 | 调用 `CallBack` 修改状态 |

### 5.7 节点数据回写

| 项目 | 内容 |
|------|------|
| 接口 | `BppController#DataWriteBack` |
| 配置 | BPP需配置节点回写接口 |
| 处理 | 各自BPP处理类实现，将数据回写到单据 |

---

## 六、前端JS对接

### 6.1 通用JS文件
- 文件：`BppFlow.js`
- 功能：查看BPP审批信息 + 流程废弃按钮的展示规则
- 用法：直接引用

### 6.2 各自需添加的按钮
- **提交审批**按钮：需在业务界面（mcs_credit_record表单）单独添加
- 参考通用JS中的实现逻辑

---

## 七、信用评估系统对接方案

### 7.1 触发时机

```
状态13(信用分计算) → 用户点击【下一步】→ 状态14(审核申请)
                                      ↓
                              BppIntegrationPlugin 触发
                              调用 mcs_bppstartapi 发起BPP审批
```

### 7.2 发起参数封装（GetBppFormData）

| 参数 | 值 |
|------|-----|
| 流程标题 | "客户信用评估审批 - {客户名称}" |
| 模板Code | 测试环境模板Code（从BPP后台获取） |
| 表单变量 | 客户名称、信用分、等级、Coface ID等 |
| 流程变量 | 信用分范围（用于分支判断） |
| 发起人 | mcs_applicant 对应的飞书账号 |

### 7.3 回调处理（CallBack）

| BPP操作 | 实体状态变更 |
|---------|-------------|
| 审批通过 | mcs_status → 15(审批通过), mcs_active → true, 更新客户主数据信用分/等级 |
| 驳回 | mcs_status → 12(人工复核), mcs_bpprejectreason → 驳回原因 |
| 撤回 | mcs_status → 12(人工复核), 清空BPP字段 |
| 废弃 | mcs_status → 12(人工复核), 清空BPP字段 |

### 7.4 当前已实现 vs 待实现

| 功能 | 当前状态 | 说明 |
|------|---------|------|
| BppIntegrationPlugin框架 | ✅ 已存在 | 占位实现，未调用真实BPP接口 |
| 流程发起 | ⏸️ 待开发 | 需实现IBPPHandlerService + 注册 |
| 回调处理 | ⏸️ 待开发 | CallBack中处理通过/驳回/撤回/废弃 |
| 前端提交审批按钮 | ⏸️ 待开发 | 需在mcs_credit_record表单添加 |
| BPP通用JS引用 | ⏸️ 待开发 | 引入BppFlow.js |
| 模板Code配置 | ⏸️ 待配置 | 需从BPP后台获取测试模板Code |

---

## 八、调试方法

1. **接口调用日志**：在接口管理中查看输入/输出
2. **修改审批人**：发起后在BPP后台将当前处理人改为自己，然后做审批
3. **节点跳转**：后台可直接跳转节点，方便调试

---

## 九、常见问题

| 问题 | 排查方法 |
|------|---------|
| "模板中不存在开启的模板" | 检查模板状态是否为"已开启" |
| 节点找不到审批人 | 查看报错节点配置，检查变量映射传入值是否满足条件 |
| 接口配置变更后无效 | 到引用模板中更新接口版本，重新保存接口字段映射 |

---

## 十、对接前准备清单

- [ ] 申请BPP测试环境后台管理权限
- [ ] 获取/复制测试流程模板，记录模板Code
- [ ] 确认 mcs_credit_record 实体BPP字段已创建
- [ ] 实现 BPPHandlerServiceForCreditRecord 类（IBPPHandlerService）
- [ ] 在 StartupHelper 中注册
- [ ] 配置模板Code到系统配置
- [ ] 前端添加"提交审批"按钮，引入 BppFlow.js
- [ ] 测试：发起 → 审批通过/驳回 → 状态变更验证
