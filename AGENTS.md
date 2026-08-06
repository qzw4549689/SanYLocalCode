# 项目级 AI 协作规范

> **适用范围**：本文件管辖整个 `SanYi` 项目目录及其所有子目录。
> **作用**：每次新会话开始时，AI 必须优先读取并遵守本文件中的用户偏好与协作规则。

---

## 1. 语言与表达偏好

- **所有回复必须使用中文**。
- **思考过程 / 推理链必须使用中文展示**，不要只给结论。
- 代码注释、Git commit message、文档正文保持中文；专有名词、类名、方法名、API 路径等保持英文不变。

## 2. 协作原则

- 不要假设，有疑问时先确认。
- 做最小改动，优先复用现有工具和代码。
- 涉及 Git 操作、UAT/生产环境发布、Solution 导入等关键动作，必须得到用户明确授权。

## 3. 新会话必查资料

新会话开始时，按 `/skill:d365-context` 指引恢复上下文：

1. `Memory.md` — 项目当前状态与关键结论
2. `/skill:d365-system-access` — 环境地址与账号
3. `/skill:d365-dev` — 开发规范与流程
4. `/skill:d365-deploy` — 发布部署指南
5. `/skill:d365-tools` — 工具使用速查
6. `Code/Customizations/AGENTS.md` — 代码目录专项规范

## 4. 指令边界与代码修改红线

- **只做用户指令内的事情**，不擅自执行与当前指令无关的操作。
- **先回答/完成用户当前的指令**，再请示下一步；尤其在涉及代码修改、配置变更、数据修复、部署发布时，必须先获得用户明确授权，不能边排查边顺手改代码。
- 如果用户只要求“查看/查询/验证/分析”，AI 只能做只读操作；如需修改代码或数据，必须明确询问：“是否需要我修改？”并说明改动范围、文件、预期影响。
- **本地 SanYi 仓库禁止推送至 Azure DevOps D365 项目仓库**：所有针对 `https://dev.azure.com/SanyGlobalCRM/D365/_git/D365` 的推送、PR 分支创建、合并操作，必须在远程服务器 `tx-windows`（`122.51.232.70`，`C:\Projects\D365`）上进行。本地 Mac 上的 SanYi 目录仅用于个人笔记、独立模块开发和 GitHub 备份。
- **本地独立 Assembly 测试结束后必须注销**：在 DEV/UAT 使用本地独立 Assembly（如 `SanyD365.Plugins.TradeStPayTerm`）做 Plugin 功能验证后，必须在测试结束的第一时间执行 `unregister-assembly <Assembly名>` 注销 Step、Type 和 Assembly，避免 Solution 打包时携带对不存在 Assembly 的依赖，导致他人发布/导入 Solution 失败（错误 `8004801D`）。未同步到远程主项目前，不得将独立 Assembly 的 Step 留在环境中。
- **WebResource / 实体发布必须使用通用服务**：所有代码中的发布操作必须复用 `D365ToolCommon.WebResource.WebResourceService.PublishWebResources` 和 `D365ToolCommon.Publishing.PublishingService.PublishEntities`，禁止直接构造 `PublishXmlRequest`。详见 `/skill:d365-tools` 第 3 章。
- **🚨 绝对禁止覆盖公共/通用文件（2026-06-27 新增）**：AI 严禁直接覆盖任何文件内容，尤其是多人共用的通用文件（如 `ms_languagefile_1033/2052`、`1033.json`、`2052.json` 等语言包）。只允许在已有内容后追加。AI 不得擅自修改所有人公用的通用文件；如需修改，必须先获得用户逐字明确授权，并说明改动范围。
- **🚨 元数据创建必须使用公共方法（2026-06-29 新增）**：所有实体、字段、表单、视图、关系、WebResource 等元数据的创建与更新，必须使用 `D365ToolCommon` 或 `MetadataTool` 中已有的公共方法。严禁在任意工具、脚本、插件中直接调用 `CreateAttributeRequest`、`CreateEntityRequest`、`UpdateEntityRequest` 等 SDK 原生 API 创建元数据；严禁临时编写新的元数据创建方法。如公共方法不存在或不能满足需求，必须向用户提出申请，获批准后方可修改或扩展公共方法。
- **🚨 新增组件必须及时加入主清单 Solution（2026-07-20 新增，2026-08-01 强化）**：`AllComponent_Peter_NoUAT`（显示名「AllComponent_Peter_整理_禁止导入UAT」）是发版分布核对的唯一基准，禁止导入 UAT。开发在 DEV1 新增任何 D365 组件（实体/字段/表单/BPF/WebResource/Plugin Assembly/Step/Custom API/App Action 等）后，**主清单必须加，无一例外、无需再问**（工具：`MetadataTool add-solution-component`，幂等；刚创建的组件立即加报 does not exist 时，删除重建并在创建时直接指定目标包）；本地临时独立 Assembly、测试组件、他人组件不得加入；组件从环境删除时须同步从主清单移除。**发版到 UAT 的包（entity_XXX 等）归属每次都不一样，AI 不清楚必须问用户，禁止自行决定**（历史教训：曾擅自把按钮加进默认包 entity_20260603_peter）。详细规则见 `/skill:d365-dev` 第 8.3.1 节，发版核对见 `/skill:d365-deploy` 第 5.1 节。
- **🚨 新增组件必须主动通知用户（2026-07-24 新增）**：每次开发（代码/元数据/配置）新增任何 D365 组件（实体/字段/表单/视图/关系/BPF/WebResource/Plugin Assembly/Step/Custom API/App Action/工作流/文档模板等）后，**AI 必须在完成汇报中显式列出新增组件清单（组件类型+名称+所属 Solution 建议），并明确提醒用户加入对应的包**，由用户负责加到对应发版包/主清单，以便后续发版时不遗漏。漏报 = 发版漏组件，视为严重失误。
- **🚨 开发过程必须同步评估发布清单影响（2026-07-28 新增，强制执行）**：每次开发（代码/元数据/配置变更）时，AI 必须同步评估「是否影响发布/上线清单」，并随开发同步更新，不得事后补：① 新增组件 → 提醒加主清单 `AllComponent_Peter_NoUAT`；② 新增/变更配置数据 → 更新 `Documents/Planning/D365配置数据清单.md` 与《上线核对清单》第 4 章；③ 发现新的「不能随 Solution 发布」事项 → 登记《上线核对清单》（`Documents/Planning/上线核对清单.md`）第 2.4 章；④ 涉及字段删除/类型变更 → 立即提示生产导入冲突风险（历史事故 `80041A06`），并优先建议新建字段替代。
- **🚨 导包改实体必须先问用哪个包（2026-08-01 新增，强制执行）**：凡涉及导出/导入 Solution 包来修改实体（如 deploy-ribbon 合并 RibbonDiffXml、pac solution import、import-solution 等），**AI 必须先询问用户使用哪个载体包，获明确指定后方可执行，严禁自行选择/随意使用任何现有包**（历史教训：擅自用发版包 `entity_20260722` 当载体，该包混入大量其他团队实体，导出重导影响面不可控）。同理，禁止擅自在任何 Solution 中添加/移除组件。
- **🚨 命令栏按钮跨环境发布默认用 RibbonDiffXml，简单按钮可经用户确认用 App Action（2026-08-01 新增）**：需要显隐规则随包或跨环境零手动步骤的命令栏按钮，一律用 RibbonDiffXml 内联在实体自定义中随实体包走（App Action 现代按钮依赖 App 级命令组件库，每环境独立副本不可跨环境打包）；无特殊显隐控制的简单按钮，经用户确认可用 App Action（2026-08-01 用户指示【Coface 下单】即用 App Action）。App Action 仅限 DEV 临时验证场景不在此限。按钮的载体包选择同样遵守上一条红线。
- **🚨 开发完成必须登记任务看板「发布清单」（2026-08-03 新增，2026-08-05 起改为看板登记，强制执行）**：每次开发/修 Bug 在 DEV 验证通过后、完成汇报前，必须把本次全部待发布内容逐项登记到任务看板发布清单（`POST http://122.51.232.70:8100/api/release-items/item`，含既有组件的 JS/Plugin 代码变更，不止新增组件；同一文件多个 Bug 合并为一项）；Step 类组件登记时由 AI 直接加入 McsPlugin。原《待发布内容清单.md》已于 2026-08-05 按用户指示废弃删除（历史见 git），看板为唯一登记处。详细节点与登记口径见 `/skill:d365-dev` 第 8.3.3 节。漏登 = 发版漏组件，历史教训：2026-08-03 #1507/#1508 漏登被用户批评。

---

## 5. Bug 修复登记机制（禅道关联，2026-07-24 新增，强制执行）

- **先登记后动手**：凡修复 Bug，开工前必须先在 `Documents/Tests/BugReports/禅道Bug修复记录.md` 登记：禅道 Bug 编号、标题、模块、日期、状态。AI 接到 Bug 修复指令时必须先读取该表确认已登记，未登记必须先补登记再改代码。
- **修复记录必须关联禅道编号**：根因、改动文件、部署/验证状态记录在同一行，随修复进展持续更新；DEV 验证通过置「已修复待验证」，用户验证/UAT 发布后置「已关闭」。
- **无禅道编号的 Bug**（口头反馈、自查发现等）同样必须登记，编号列填「无」并备注来源。
- **commit message 必须关联禅道编号**，格式示例：`fix(tradestpayterm): 事业部带出及审批共享修复 (#1151)`。

---

*本文件记录项目级用户偏好，变更频率低，发现新的全局偏好时及时更新。*
