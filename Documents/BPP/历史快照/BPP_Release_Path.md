# BPP 客户信用评估 - 发布路径

> 日期：2026-06-10
> 发布工具：D365 版本发布（UAT）工具
> 工具地址：https://n8n.sany.com.cn/form/0923e9fb-86fa-48d7-8d94-3d3e360ab7dd

---

## 实际流程（已确认）

```
特性分支 (uat_20260606_qzw)
    │
    ▼
推送到远程 → PR 审查
    │
    ▼
合并到 uat 分支
    │
    ▼
D365 版本发布（UAT）工具
    • 选择：Extension API
    • 提交发布申请
    │
    ▼
等待发布完成（自动/审批流程）
    │
    ▼
UAT 环境测试验证
    • 13→14 触发 BPP
    • mcs_bppstatus = Submitted
    • 审批通过/驳回
    • 回调更新状态
```

---

## 用户负责事项

| 步骤 | 操作 | 说明 |
|------|------|------|
| 1 | 代码修改在特性分支 | 已完成 ✅ |
| 2 | `git add` + `git commit` + `git push` | 待执行 |
| 3 | 在 Azure DevOps 创建 PR → `uat` 分支 | 待执行 |
| 4 | 等待 Reviewer 审批合并 | 待执行 |
| 5 | 合并后，打开发布工具提交申请 | https://n8n.sany.com.cn/form/0923e9fb-86fa-48d7-8d94-3d3e360ab7dd |
| 6 | 选择 **Extension API** | 工具选项 |
| 7 | 等待发布完成 | 自动部署到 UAT |
| 8 | UAT 环境测试 | 端到端验证 |

---

## 不再需要的步骤（已废弃）

| 原步骤 | 状态 | 原因 |
|--------|------|------|
| DEV1 PRT 注册 | ❌ 不需要 | 发布工具自动处理 |
| 手动管理 Solution | ❌ 不需要 | 发布工具自动处理 |
| DEV1 测试验证 | ❌ 不需要 | 直接在 UAT 测试 |

---

## UAT 发布前必须确认的配置

在提交发布申请前，确保 UAT 的 D365 系统配置已就绪：

| 配置 Name | 要求 | 负责人 |
|-----------|------|--------|
| `BPP_WorkFlowTemplateCode` | JSON 中必须有 `"CustomerCreditEvaluation": "781094754802827383"` | **你**（需提前配置） |
| `D365BaseUrl` | 值为 UAT 的 D365 地址 | 已存在 ✅ |
| `Bpp_ApprovalFlowBaseUrl` | UAT BPP 门户地址 | 已存在 ✅ |

> ⚠️ **关键提醒**：`BPP_WorkFlowTemplateCode` JSON 中新增字段需要手动在 UAT 配置，**不会随代码自动发布**。

---

## UAT 测试 checklist

发布完成后，在 UAT 验证：

- [ ] `mcs_credit_record` 状态 13 → 14
- [ ] `mcs_bppstatus` 变为 `Submitted`
- [ ] `mcs_workflowid` 有值
- [ ] `mcs_bpperrormsg` 为空
- [ ] BPP 平台能看到审批单，表单变量正确显示
- [ ] 审批通过后 `mcs_bppstatus` = `Approved`，`mcs_status` = 15
- [ ] 审批驳回后 `mcs_bppstatus` = `Rejected`，`mcs_status` = 12
- [ ] Account 信用字段正确更新（信用分、等级、有效状态）

---

## 附录：历史详细流程图（早期设计版，已废弃 DEV1 中间步骤）

> 以下流程图为早期设计，包含 DEV1 PRT 注册等中间步骤，现已废弃，仅作历史参考。

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          特性分支开发                                     │
│  uat_20260606_qzw                                                       │
│  ├─ 修改 BPPFormData.cs（+CustomerCreditEvaluation）                     │
│  ├─ 新建 BPPHandlerServiceForCreditRecord.cs                            │
│  └─ 修改 StartupHelper.cs（+mcs_credit_record 注册）                     │
└─────────────────────────────────┬───────────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    PR → 代码审查 → 合并到 uat 分支                        │
│  (Azure DevOps Pull Request)                                            │
│  • Reviewer 审批                                                         │
│  • 解决冲突（如有）                                                       │
│  • 合并后 uat 分支 = 唯一可信源                                           │
└─────────────────────────────────┬───────────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        拉取 uat 最新代码                                  │
│     git checkout uat                                                    │
│     git pull origin uat                                                 │
└─────────────────────────────────┬───────────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        编译 Release                                       │
│     dotnet build -c Release                                             │
└─────────────────────────────────┬───────────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        阶段 1：DEV1 测试（已废弃）                         │
│                                                                         │
│  ① PRT 注册到 DEV1（Sandbox）                                            │
│     • 更新 SanyD365.Main.dll                                            │
│     • 保留现有 Step                                                      │
│                                                                         │
│  ② DEV1 D365 配置                                                        │
│     • BPP_WorkFlowTemplateCode JSON + CustomerCreditEvaluation          │
│     • D365BaseUrl = DEV1地址                                             │
│                                                                         │
│  ③ 发布 mcs_credit_record 实体自定义项                                    │
│                                                                         │
│  ④ 测试验证                                                              │
│     • 状态 13→14                                                         │
│     • BPP 发起成功                                                       │
│     • mcs_bppstatus = Submitted                                          │
│     ⚠️ 回调可能到 UAT（取决于 BPP 回调地址配置）                           │
└─────────────────────────────────┬───────────────────────────────────────┘
                                  │
                    ┌─────────────┴─────────────┐
                    │                           │
                    ▼                           ▼
            ┌──────────────┐            ┌──────────────┐
            │   不通过 ❌   │            │   通过 ✅    │
            └──────┬───────┘            └──────┬───────┘
                   │                           │
                   ▼                           ▼
            回到特性分支修复              （同一编译产物或重新编译）
            重新 PR → 重新来一遍                 │
                                                 ▼
                                    ┌─────────────────────────────┐
                                    │      阶段 2：UAT 部署        │
                                    │                             │
                                    │  ① 导入解决方案到 UAT       │
                                    │     • 包含 SanyD365.Main.dll│
                                    │                             │
                                    │  ② UAT D365 配置            │
                                    │     • BPP_WorkFlowTemplateCode│
                                    │       JSON + CustomerCreditEvaluation
                                    │     • D365BaseUrl = UAT地址  │
                                    │                             │
                                    │  ③ 发布 mcs_credit_record   │
                                    │                             │
                                    │  ④ UAT 完整测试             │
                                    │     • 审批通过/驳回         │
                                    │     • 回调更新 mcs_bppstatus│
                                    │     • Plugin 更新 mcs_status│
                                    └─────────────────────────────┘
```
