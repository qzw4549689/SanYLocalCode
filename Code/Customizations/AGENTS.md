# Code/Customizations 目录 AI 协作规范

> **适用范围**：本文件管辖 `Code/Customizations` 及其所有子目录下的代码操作。
> **作用**：AI 在修改本目录下任何文件前，必须优先遵守本文件中的规则。
> **完整流程参考**：`/.agents/skills/d365-dev/SKILL.md`、`/.agents/skills/d365-deploy/SKILL.md`

---

## 1. 项目结构基本原则（必须记住）

### 1.1 本地项目 ≠ 远程主项目

| 维度 | 本地开发（当前机器） | 远程主项目（tx-windows） |
|---|---|---|
| 路径 | `Code/Customizations/Plugins/<模块>/` | `C:\Projects\D365\D365\` |
| 命名空间 | 独立命名空间，避免冲突 | `SanyD365.D365Extension.Sales.Plugins.<模块>` |
| 输出 DLL | `SanyD365.Plugins.<模块>.dll`（独立 Assembly） | `SanyD365.D365Extension.Sales.dll`（主 Assembly） |
| 用途 | 快速开发 + DEV 环境独立测试 | 正式集成编译 + Git PR + UAT 发布 |
| .NET 版本 | 本地可编译即可 | **必须是 .NET Framework 4.6.2** |

> **核心规则**：本地代码可以独立注册到 DEV 环境测试；同步到远程服务器时，必须改写命名空间、引用、csproj 以适配主项目。

### 1.2 代码同步流程（强制顺序）

```
本地开发 → 单元测试验证功能 → DEV 集成测试 → 测试人确认通过 → 同步到远程服务器 → 远程编译通过 → Git 提交/PR → 合并到 uat → 更新 DEV Assembly → UAT 发布
```

- **阶段三（DEV 集成测试通过）是强制卡点**，未经测试人确认，严禁进入阶段四。
- 功能正确性优先通过**本地单元测试**验证，再进入 DEV 环境做集成测试。
- 同步到远程时只修编译问题（命名空间、引用、语法兼容），**严禁修改业务逻辑**。

---

## 2. 代码开发红线（严禁）

### 2.1 Plugin 开发

- 必须校验 `target.LogicalName`，防止误触发其他实体
- `catch (Exception)` 后必须 `rethrow`，禁止吞异常
- 不修改原生系统组件、系统角色、原生 WebResource

### 2.2 JS WebResource 开发

- 使用字段逻辑名，禁止硬编码 GUID 或 ID
- 复制窗体时必须同步修改 `functionName`、JS 对象名、文件名
- `cell id` 必须使用 `Guid.NewGuid().ToString("B")`
- **🚨 绝对禁止覆盖公共语言包等通用 WebResource（2026-06-27 新增）**：`ms_languagefile_1033/2052` 等被多模块共用的通用文件，AI 严禁直接覆盖其全部内容。新增 key 时必须在原文件基础上追加，避免冲掉其他模块 key。
- **🚨 JS/HTML 修改必须走仓库（2026-08-15 新增）**：本目录 JS/HTML 与远程仓库 `D365/SanyD365.D365WebResource/WebResource/mcs_/Scripts|Htmls/Sales/CreditAssessment/` 一一对应。修改流程：本地改 → 推分支+PR → 用户合并 → tx-windows 拉取 uat → 用仓库版部署 DEV1；禁止直连部署 DEV1。详见 `/skill:d365-dev` 第 8.5.2 节。

### 2.3 实体/字段

- 实体前缀统一使用 `mcs_`
- 修改选项集相关代码前，必须先查询实际选项集值
- 禁止直接修改原生系统实体或字段

### 2.4 元数据创建红线（2026-06-29 新增）

**所有实体、字段、表单、视图、关系、WebResource 等元数据的创建与更新，必须使用 `D365ToolCommon` 或 `MetadataTool` 中已有的公共方法。**

- **严禁**在任意工具、脚本、插件中直接调用 `CreateAttributeRequest`、`CreateEntityRequest`、`UpdateEntityRequest` 等 SDK 原生 API 创建元数据
- **严禁**临时编写新的元数据创建方法
- 如现有公共方法不存在或不能满足需求，**必须向负责人提出申请，获批准后方可修改或扩展公共方法**
- 常用公共方法位置：
  - 字段检查/创建 → `D365ToolCommon.Metadata.MetadataFieldService`
  - 实体/字段/表单/视图综合管理 → `MetadataTool.Services.EntityManager`
  - WebResource 部署 → `D365ToolCommon.WebResource.WebResourceService`
  - 发布实体/WebResource → `D365ToolCommon.Publishing.PublishingService`

---

## 3. 命名与编码规范

### 3.1 实体与编码

| 类型 | 规范 | 示例 |
|---|---|---|
| 实体前缀 | `mcs_` | `mcs_credit_record` |
| 自动编码 | 业务缩写 + YYYYMMDD + 4 位序号 | `SC202506040001`、`SCO202506040001` |
| 解决方案 | `entity_YYYYMMDD_peter` | `entity_20260603_peter` |

### 3.2 状态值映射（PRD 编号 ≠ 技术实现值）

| 业务状态 | 选项集值 |
|---|---|
| 发起信用评估 | 9 |
| 关联客户代码 | 10 |
| 数据集成 | 11 |
| 人工复核 | 12 |
| 信用分计算 | 13 |
| 审核申请 | 14 |
| 审批通过 | 15 |
| 审批未通过 | 16 |

---

## 4. 发布部署红线（严禁 AI 擅自执行）

- **严禁使用 `pac solution import` 导入 Solution**
- **严禁在 UAT/生产环境直接操作元数据**（CreateAttribute/DeleteAttribute/PublishAll 等）
- 发布范围只限于当前操作的实体/WebResource，**全局 PublishAll 必须得到用户明确批准**
- 涉及新增实体/字段时，必须先把实体/元数据发布到 UAT，再发布 Plugin
- UAT 发布必须通过 n8n Release Tool 由用户手动执行

---

## 5. Git 工作流（强制）

- 主集成分支是 `uat`
- 个人分支命名：`uat-日期-姓名缩写-功能简述`
- **不要直接 push `uat` 分支，必须走 PR**
- **所有针对 Azure DevOps D365 项目仓库（`https://dev.azure.com/SanyGlobalCRM/D365/_git/D365`）的 push、PR 分支创建、合并操作，必须在远程服务器 `tx-windows`（`122.51.232.70`，`C:\Projects\D365`）上进行**。本地 Mac 上的 SanYi 目录禁止推送至该项目仓库。
- 远程编译通过后尽快推分支、建 PR，避免代码积压

---

## 6. 已有工具优先原则

开发新功能前，先检查是否已有现成方法，**禁止临时写重复代码**：

1. `Code/Tools/D365ToolCommon/` — **首选**：D365 连接认证、Plugin 注册/查询/注销、WebResource 部署、字段检查/创建、发布
2. `Code/Tools/DeployTool/` — WebResource、App Action、发布
3. `Code/Tools/MetadataTool/` — 实体、字段、表单、视图、Plugin 注册
4. `Code/Customizations/Plugins/*/Plugin/` — 业务逻辑复用
5. `Code/Customizations/WebResources/JS/` — JS 表单逻辑复用

### 6.1 D365ToolCommon 共享库约束（2026-06-13 新增）

在 `Code/Tools/` 下新增功能时，**优先使用 `D365ToolCommon` 中的通用方法**：

- 连接认证 → `D365ToolCommon.Connection.D365ConnectionFactory`
- Plugin 注册/查询/注销 → `D365ToolCommon.Plugin.PluginRegistrationService` / `PluginQueryService` / `PluginStepDeletionService`
- WebResource 部署 → `D365ToolCommon.WebResource.WebResourceService`
- 字段检查/创建 → `D365ToolCommon.Metadata.MetadataFieldService`
- 发布实体/WebResource → `D365ToolCommon.Publishing.PublishingService`

**如果通用方法不满足需求，必须先扩展 `D365ToolCommon` 中的对应类，而不是在 MetadataTool / DeployTool / CofaceConfigImporter 里临时写重复方法。**

---

## 7. 常见陷阱（必须避免）

| 陷阱 | 后果 | 解决方案 |
|---|---|---|
| 本地项目当成远程主项目直接 push | 命名空间/引用冲突，无法编译 | 记住本地是独立项目，远程集成时必须改造 |
| 未等 DEV 测试通过就同步远程 | UAT 携带 Bug，回滚成本高 | 阶段三测试通过是强制卡点 |
| Plugin 吞异常 | 事务被破坏，出现 "ISV code reduced the open transaction count" | catch 后必须 rethrow |
| 用 Solution 导入更新单个 WebResource | 阻塞环境 5-60 分钟 | 用 C# DeployTool |
| 只看 Assembly version 判断同步 | 误以为 UAT/DEV 一致 | 查 `pluginassembly.modifiedon` |
| 用 RibbonDiff.xml 创建按钮（2026-07-27 修订） | 生成只读 Legacy Ribbon；但 appaction 显隐规则不随包 | 默认用 C# AppActionDeployer；需显隐规则随包（SelectionCountRule 等）时改用 RibbonDiffXml（案例：成交条件批量按钮） |

---

## 8. 更新规则

- 发现新的通用陷阱或流程变更时，更新本文件
- 具体场景的完整操作步骤仍写入对应 skill（`d365-dev`、`d365-deploy` 等）
