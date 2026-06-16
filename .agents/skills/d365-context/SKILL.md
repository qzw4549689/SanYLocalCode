---
name: d365-context
description: D365 项目上下文恢复助手。在上下文压缩或新对话开始时，自动读取 Memory.md、SystemAccess.md、SKILL.md 及关键代码，帮助 AI 快速恢复项目记忆。
---

# D365 项目上下文恢复

> **使用时机**：新对话开始、上下文被压缩后、AI 表现出对项目背景记忆不清时。

当用户调用 `/skill:d365-context` 时，请按以下步骤恢复项目上下文，不要直接询问用户要做什么。

---

## 第一步：读取核心记忆文件

请使用 ReadFile 工具读取以下文件（按顺序）：

1. `Memory.md` — 项目整体记忆、当前任务状态、关键结论
2. `SystemAccess.md` — 系统访问地址、环境链接、账号信息
3. `SKILL.md` — skill 总览索引，了解有哪些专项 skill 可用

---

## 第二步：根据关键内容目录定位信息

读取完核心文件后，根据用户当前问题的类型，按以下目录快速定位并读取相关内容：

| 问题类型 | 读取目标 |
|---------|---------|
| **BPP 审批流程阻塞、异步处理、审批实例未创建** | `Memory.md` 中 BPP 相关段落 + `Service/SanyD365.Main/Entities/BPP/` 相关 Handler/Service 代码 |
| **部署、发布、n8n、Azure 代码勾选** | `/skill:d365-deploy` |
| **命名规范、状态值、开发流程、远程服务器** | `/skill:d365-dev` |
| **MetadataTool、DeployTool、App Action 按钮** | `/skill:d365-tools` |
| **系统地址、账号、环境链接** | `/skill:d365-system-access` |
| **Plugin 注册、Assembly 同步、元数据查询** | `Code/Tools/MetadataTool/` 相关命令 + `Memory.md` 中 Plugin 相关记录 |
| **当前活跃 Bug / 待验证任务** | `Memory.md` 中 "active_issues" / "当前断裂点" / "待部署验证" 段落 |

---

## 第三步：确认当前任务上下文

读取完成后，请根据 `Memory.md` 中的实际内容，向用户简要确认恢复的关键上下文。**不要编造具体案例**，如果 `Memory.md` 中没有相关信息，直接说明已读取核心文件即可。

通用确认模板：

> 已恢复项目上下文。根据 `Memory.md`：
> - **当前焦点**：`<从 current_focus 段落读取>`
> - **活跃问题**：`<从 active_issues 段落读取>`
> - **环境/账号**：`<从 SystemAccess.md 或 Memory.md environment 段落读取>`
>
> 请告诉我接下来要做什么？

如果 `Memory.md` 内容为空或缺失关键信息，输出：

> 已读取 `Memory.md`、`SystemAccess.md`、`SKILL.md`，但 `Memory.md` 中未找到当前活跃任务记录。请告诉我你接下来想处理什么？

---

## 第四步：执行用户下一步指令

上下文恢复后，根据用户后续指令继续执行任务。如果用户没有明确指令，可询问：

> 上下文已恢复。你想继续处理哪个任务？

---

## 注意事项

- **不要假设**：Memory.md 中的信息可能不是最新的，必要时读取实际代码或询问用户确认。
- **不要只读摘要**：如果涉及具体代码 Bug，必须读取对应源文件，不能只依赖 Memory.md 中的摘要。
- **优先查 skill**：通用问题先查专项 skill，而不是重新推导。

---

*本 skill 用于帮助 AI 快速恢复 D365 项目上下文，减少用户重复输入。*
