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
本地开发 → DEV 注册测试 → 测试人确认通过 → 同步到远程服务器 → 远程编译通过 → Git 提交/PR → 合并到 uat → 更新 DEV Assembly → UAT 发布
```

- **阶段三（DEV 测试通过）是强制卡点**，未经测试人确认，严禁进入阶段四。
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

### 2.3 实体/字段

- 实体前缀统一使用 `mcs_`
- 修改选项集相关代码前，必须先查询实际选项集值
- 禁止直接修改原生系统实体或字段

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
| 用 RibbonDiff.xml 创建按钮 | 生成只读 Legacy Ribbon | 用 C# AppActionDeployer 创建 Modern Command Bar |

---

## 8. 更新规则

- 发现新的通用陷阱或流程变更时，更新本文件
- 具体场景的完整操作步骤仍写入对应 skill（`d365-dev`、`d365-deploy` 等）
