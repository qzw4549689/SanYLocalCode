# BPP 部署前验证报告

> 日期：2026-06-10
> 验证人：Kimi
> 目标环境：DEV1 (https://dev1.crm5.dynamics.com)

---

## 1. 代码静态检查 ✅

| 检查项 | 结果 | 说明 |
|--------|------|------|
| `BPPHandlerServiceForCreditRecord.cs` 存在 | ✅ | 文件已创建 |
| `GetBppFormData` 方法定义 | ✅ | 包含模板 Code 读取、记录查询、表单变量组装 |
| `CallBack` 方法定义 | ✅ | 包含 int→string 转换 (30→Approved, 11→Rejected) |
| `UpdateEntityStatusForStart` 方法定义 | ✅ | 更新 mcs_bppstatus=Submitted, 清空错误信息 |
| `RollbackEntityStatusForFail` 方法定义 | ✅ | 空实现（Plugin 层已处理回滚） |
| `StartupHelper.cs` 注册 | ✅ | `Handler["mcs_credit_record"]` 已注册 |
| `BPP_WorkFlowTemplateCodeEntity.CustomerCreditEvaluation` | ✅ | 属性已添加 |
| `using SanyD365.Main.Entities.HelperEx` | ✅ | 编译所需的扩展方法命名空间已引用 |

---

## 2. D365 字段验证 ✅

### 2.1 mcs_credit_record 实体字段

| 字段名 | 类型 | 状态 | BPP Handler 中用途 |
|--------|------|------|-------------------|
| `mcs_scoreid` | String | ✅ | 信用评估编号（表单变量） |
| `mcs_applicant` | String | ✅ | 申请人（表单变量） |
| `mcs_custname` | String | ✅ | 客户名称（表单变量） |
| `mcs_accountid` | Lookup | ✅ | 关联 Account 查 accountnumber |
| `mcs_countrycode` | String | ✅ | 国家代码（表单变量） |
| `mcs_creditscore` | Decimal | ✅ | 信用分（表单变量） |
| `mcs_creditgrade` | String | ✅ | 信用等级（表单变量） |
| `mcs_cofaceid` | String | ✅ | Coface 客户 ID（表单变量） |
| `mcs_bppstatus` | String | ✅ | BPP 状态（Callback/UpdateEntityStatusForStart） |
| `mcs_workflowid` | String | ✅ | BPP 流程实例 ID（框架自动更新） |
| `mcs_bpperrormsg` | Memo | ✅ | BPP 错误信息 |
| `mcs_bpprejectreason` | Memo | ✅ | BPP 驳回原因（Callback） |
| `mcs_approvedate` | DateTime | ✅ | BPP 审批完成日期（Callback） |

### 2.2 account 实体字段

| 字段名 | 类型 | 状态 | BPP Handler 中用途 |
|--------|------|------|-------------------|
| `accountnumber` | String | ✅ | 客户编码（表单变量 mcs_accountcode） |

---

## 3. 实际数据验证 ⚠️

查询 DEV1 中最近修改的 `mcs_credit_record` 记录：

- **记录ID**: `27d4b0f6-7c87-41f4-87e3-76a21ca72217`
- **评估编号**: `SCO202606090001`
- **业务状态**: `14` (审核申请)
- **BPP状态**: `Submitted`（之前 Plugin 调用后更新）
- **BPP错误信息**: `找不到消息类型为mcs_credit_record的消息处理服务...`（符合预期，Handler 尚未部署）
- **信用分**: `N/A`（空值）⚠️
- **信用等级**: `N/A`（空值）⚠️

### 注意事项

1. **mcs_creditscore / mcs_creditgrade 为空**：不影响 Handler 部署，但测试 BPP 表单时，信用分和等级字段会传空值。建议测试前确保记录有完整的评分数据。
2. **mcs_workflowid 为空**：符合预期，因为之前 BPP 发起失败，没有生成 workflowid。

---

## 4. D365 系统配置验证

| 配置 Name | 状态 | 说明 |
|-----------|------|------|
| `BPP_WorkFlowTemplateCode` | ✅ 用户已更新 | JSON 中已添加 `CustomerCreditEvaluation` |
| `D365BaseUrl` | ✅ 用户已确认 | DEV1 地址存在 |
| `Bpp_ApprovalFlowBaseUrl` | ✅ 已存在 | UAT BPP 门户地址 |

---

## 5. 部署后预期行为

### 5.1 正常流程
1. 用户将 `mcs_status` 从 `13` → `14`
2. `BppIntegrationPlugin` 调用 `mcs_bppstartapi`
3. BPP 框架找到 `BPPHandlerServiceForCreditRecord`
4. `GetBppFormData` 读取记录，组装 10 个表单变量，模板 Code = `781094754802827383`
5. BPP 外部服务发起审批，返回 `flowId`
6. BPP 框架更新 `mcs_workflowid`、`mcs_nextapprover`
7. `UpdateEntityStatusForStart` 更新 `mcs_bppstatus = "Submitted"`
8. 审批完成后 BPP 回调，`CallBack` 更新 `mcs_bppstatus = "Approved"` / `"Rejected"`
9. `BppCallbackPlugin` 监听变化，更新 `mcs_status = 15` / `12`

### 5.2 验证点
- [ ] `mcs_bpperrormsg` 变为空
- [ ] `mcs_workflowid` 有值
- [ ] `mcs_bppstatus` 变为 `"Submitted"`
- [ ] BPP 平台能看到审批单，表单字段正确显示

---

## 6. 结论

✅ **代码层面**：通过静态检查，无编译错误（用户确认 IntelliSense 错误已清除）
✅ **D365 字段层面**：所有需要的字段均已存在，类型正确
⚠️ **数据层面**：测试记录缺少信用分和等级，建议补充后再做端到端测试
✅ **配置层面**：用户已确认 JSON 和 URL 配置就绪

**建议：可以编译并部署。**
