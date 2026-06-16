---
name: d365-summary
description: D365 项目会话总结与记忆更新助手。用于在任务节点、会话结束或上下文压缩前，快速总结当前状态、已完成项、阻塞点和下一步行动，并建议更新 Memory.md。
---

# D365 项目总结与记忆更新

> **使用时机**：
> - 完成一个子任务后，需要沉淀结论
> - 会话结束前，需要把当前状态写回 Memory.md
> - 上下文即将被压缩，需要先做一次状态快照
> - 用户说"总结一下"、"记一下"、"更新 Memory"等

当用户调用 `/skill:d365-summary` 时，请按以下步骤执行。

---

## 第一步：读取当前记忆

使用 ReadFile 读取 `Memory.md`，了解已有的项目背景、活跃任务和历史结论。

---

## 第二步：总结当前会话/任务状态

基于当前对话上下文和 `Memory.md`，整理出以下四类信息：

### 1. 已完成（Done）
- 本次会话完成了哪些具体工作
- 修改了哪些文件 / 部署了哪些组件
- 验证结果如何

### 2. 当前焦点（Current Focus）
- 正在进行的核心任务是什么
- 进展到哪一步
- 当前最关键的代码/配置/环境问题

### 3. 阻塞点与风险（Blockers & Risks）
- 还有什么没解决
- 依赖谁/什么（如：等待测试人确认、等待 n8n 部署、等待权限审批）
- 已知的 Bug 或潜在风险

### 4. 下一步行动（Next Steps）
- 接下来要做什么
- 由谁做
- 优先级排序

---

## 第三步：输出结构化摘要

向用户输出如下格式的总结（Markdown 列表）：

```markdown
## 当前任务摘要

**当前焦点**：
- xxx

**已完成**：
- ✅ xxx
- ✅ xxx

**阻塞点 / 待确认**：
- ⏳ xxx
- ⏳ xxx

**下一步行动**：
1. xxx
2. xxx
3. xxx
```

---

## 第四步：建议更新 Memory.md

输出摘要后，询问用户是否需要把当前状态写回 `Memory.md`：

> 是否需要我把以上内容更新到 `Memory.md` 的 `current_focus` / `active_issues` / `completed_tasks` 段落？

如果用户同意，执行以下操作：

1. **更新 `current_focus`**：用一句话概括当前最核心的任务状态
2. **更新 `completed_tasks`**：把本次完成的工作追加到列表
3. **更新 `active_issues`**：更新未解决问题的状态和上下文
4. **更新时间戳**：在文件末尾或相关段落添加最后更新时间

> ⚠️ **注意**：更新 `Memory.md` 时只做增量/修正，不要删除用户原有的重要历史记录。

---

## 示例输出

```markdown
## 当前任务摘要

**当前焦点**：
- `mcs_credit_record` BPP 审批异步处理阻塞排查，已加日志，待 DEV 部署验证。

**已完成**：
- ✅ 在 `BPPHandlerServiceForCreditRecord.GetBppFormData` 增加全路径诊断日志
- ✅ 在 `BPPService.Start` 增加关键节点日志和 catch 块空值保护
- ✅ `SanyD365.Main` Release 编译通过
- ✅ 创建并推送 `uat-bpp-creditrecord-log` 分支

**阻塞点 / 待确认**：
- ⏳ 需要确认 n8n 部署流程，将 `Messagehandler`（+ `ClientAPI`）发布到 DEV
- ⏳ DEV 部署后提交测试，查看日志确认 Consumer 是否正常消费消息
- ⏳ `uat` 分支被直接 push，无法 ForcePush 回滚，需另寻方式处理

**下一步行动**：
1. 用户确认 n8n 部署参数后，执行 DEV 发布
2. 在 DEV 创建测试记录，触发 BPP 审批
3. 检查日志，确认 `mcs_bppapply` 是否成功创建
```

---

## 使用建议

- **不要等到会话最后才总结**：每完成一个里程碑就调用一次，减少记忆丢失。
- **让 AI 主动提议**：如果检测到对话超过 20 轮或上下文将被压缩，可以主动建议用户调用 `/skill:d365-summary`。
- **结合 `d365-context` 使用**：先用 `/skill:d365-context` 恢复上下文，处理完任务后用 `/skill:d365-summary` 沉淀记忆。

---

*本 skill 用于帮助用户把 D365 项目会话状态结构化沉淀到 Memory.md，减少重复沟通成本。*
