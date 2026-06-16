# D365 开发通用技能总览

> 适用于所有 Dynamics 365 (Dataverse) 定制开发项目的通用技巧、工具和最佳实践。

本目录下技能已按内容类型拆分为六个专项 skill：

| Skill | 调用方式 | 内容定位 |
|-------|---------|---------|
| **d365-context** | `/skill:d365-context` | 上下文恢复：新对话或上下文压缩后，自动读取 Memory.md、SystemAccess.md 及关键目录 |
| **d365-summary** | `/skill:d365-summary` | 会话总结：任务节点或会话结束时，快速生成摘要并建议更新 Memory.md |
| **d365-deploy** | `/skill:d365-deploy` | 发布部署：部署方式优先级、项目结构、n8n Release Tool、远程编译、Git 工作流 |
| **d365-dev** | `/skill:d365-dev` | 开发规范与流程：命名规范、状态值映射、已知陷阱、代码复用、前端控件、自定义进度条、远程服务器、完整开发工作流 |
| **d365-tools** | `/skill:d365-tools` | 工具使用：MetadataTool CLI 速查、DeployTool 已封装方法、Modern Command Bar（App Action）按钮部署 |
| **d365-system-access** | `/skill:d365-system-access` | 系统访问地址：Git、飞书、DEV/UAT 环境、n8n 发布工具及账号 |

---

## 快速索引

### 我刚进新对话 / 上下文被压缩了
→ 调用 `/skill:d365-context`

AI 会自动读取 `Memory.md`、根目录 `SKILL.md`，并根据关键内容目录定位相关信息，最后向你确认当前任务状态。系统访问地址请查看 `/skill:d365-system-access`。

### 我要总结当前任务 / 更新 Memory.md
→ 调用 `/skill:d365-summary`

AI 会基于当前会话和 `Memory.md`，整理出 **当前焦点 / 已完成 / 阻塞点 / 下一步行动**，并询问是否写回 `Memory.md`。

### 我要发布代码
→ 查看 `/skill:d365-deploy`

涵盖：
- 部署方式优先级与禁止事项
- `D365.sln` vs `Service.sln` 项目区分
- n8n Release Tool 勾选规则
- `tx-windows` 远程服务器编译命令
- Git 工作流（基于 `uat` 分支，必须走 PR）

### 我要写代码 / 查规范
→ 查看 `/skill:d365-dev`

涵盖：
- 命名规范与编码规则
- `mcs_status` 状态值映射（PRD 编号 ≠ 技术实现值）
- 已知陷阱与血泪教训
- 代码复用原则
- 可编辑子网格（Editable Grid）阶段控制
- 自定义进度条方案
- 远程开发服务器连接与 VS Code Remote-SSH
- 本地 → DEV 测试 → 远程集成 → Git PR 完整工作流

### 我要使用 MetadataTool / DeployTool / 部署 App Action 按钮
→ 查看 `/skill:d365-tools`

涵盖：
- MetadataTool CLI 完整命令速查
- DeployTool 已封装方法
- Modern Command Bar 按钮部署（App Action），替代 RibbonDiff.xml

### 我要查系统地址 / 账号 / 环境链接
→ 查看 `/skill:d365-system-access`

涵盖：
- Git 仓库与 Azure DevOps 链接
- 飞书共享文件夹与项目计划表
- DEV1 / UAT 环境前台与后台地址
- n8n 发布工具地址、账号及 hosts 配置

---

## 推荐组合

| 场景 | 使用顺序 |
|------|---------|
| 新会话接手任务 | `/skill:d365-context` → 处理任务 → `/skill:d365-summary` |
| 长时间会话中途 | `/skill:d365-summary`（沉淀中间状态） |
| 上下文被压缩前 | `/skill:d365-summary` → 继续工作 |

---

*本文件仅作为总览索引。具体技能内容请查看对应 skill 文件。*
