# D365 客户信用评估系统 — 项目记忆

> **项目：** 三一重工 D365 客户信用评估系统
> **技术栈：** Dynamics 365 (Dataverse) + C# Plugin + JavaScript WebResource
> **最后更新：** 2026-08-01（Coface 系统内下单本地开发完成：编译全过，DEV1 部署与沙盒联调待下周一用户协调 Coface 后进行）

---

## 目录

- [1. 项目一句话描述](#1-项目一句话描述)
- [2. 项目结构速览](#2-项目结构速览)
  - [2.0 AI 协作红线](#20-ai-协作红线)
  - [2.1 本地测试项目 → 远程主项目同步资产](#21-本地测试项目-→-远程主项目同步资产)
  - [2.2 最近一次发布记录（Coface 配置化 + BPP 回调修复）](#22-最近一次发布记录coface-配置化-+-bpp-回调修复)
  - [2.3 当前进行中的工作（Coface 两个 Bug 修复）](#23-当前进行中的工作coface-两个-bug-修复)
- [3. 核心资产清单](#3-核心资产清单)
  - [3.1 实体（7 个自定义实体）](#31-实体7-个自定义实体)
  - [3.2 Plugin（9 个 C# 类，分 6 个模块）](#32-plugin9-个-c#-类，分-6-个模块)
  - [3.3 JS WebResource（6 个表单脚本）](#33-js-webresource6-个表单脚本)
  - [3.4 状态流转（8 个阶段）](#34-状态流转8-个阶段)
  - [3.5 客户信用画像（Phase 7，新增）](#35-客户信用画像phase-7，新增)
- [4. 四大核心文件（AI 协作入口）](#4-四大核心文件ai-协作入口)
  - [4.1 功能编号体系（F1.x ~ F6.x）](#41-功能编号体系f1x-~-f6x)
  - [4.2 测试用例编号体系](#42-测试用例编号体系)
- [5. 当前进度（2026-06-15）](#5-当前进度2026-06-15)
  - [当前焦点与阻塞](#当前焦点与阻塞)
  - [工具项目共享库规划](#工具项目共享库规划)
  - [代码同步跟踪（2026-06-11 新增）](#代码同步跟踪2026-06-11-新增)
- [6. 外部系统集成](#6-外部系统集成)
  - [BPP审批对接详情（2026-06-08）](#bpp审批对接详情2026-06-08)
- [7. 待确认 / 待处理事项](#7-待确认--待处理事项)
  - [7.2 Coface 汇率改用 D365 标准汇率评估（2026-06-21 记录）](#72-coface-汇率改用-d365-标准汇率评估2026-06-21-记录)
  - [7.1 客户主数据表与 Account 表架构变更（2026-06-16 记录）](#71-客户主数据表与-account-表架构变更2026-06-16-记录)
- [8. AI 协作指南](#8-ai-协作指南)
  - [8.1 接手项目时](#81-接手项目时)
  - [8.2 开发新功能时](#82-开发新功能时)
  - [8.3 修复 Bug 时](#83-修复-bug-时)
  - [8.4 代码规范检查清单](#84-代码规范检查清单)
  - [8.5 新增 MetadataTool 命令（2026-06-11）](#85-新增-metadatatool-命令2026-06-11)

## 1. 项目一句话描述

为三一重工海外业务打造的 **D365 客户信用评估系统**，覆盖从客户建档、评分卡配置、Coface 数据集成、信用分计算到 BPP 审批的全流程。核心是在 D365 上构建 7 个自定义实体 + 7 个 C# Plugin + 6 个 JS 表单脚本，通过自定义进度条驱动状态流转。

---

## 2. 项目结构速览

```
├── Code/                          # 所有代码资产
│   ├── Customizations/            # D365 自定义开发资产
│   │   ├── Plugins/               # 9 个业务模块 C# Plugin
│   │   │   ├── Account/           # Account 字段校验
│   │   │   ├── ScoringCard/       # 评分卡编码生成 (SC+日期+序号)
│   │   │   ├── CreditRecord/      # 评估记录编码生成 (SCO+日期+序号)
│   │   │   ├── CreditScore/       # 信用分计算
│   │   │   ├── CofaceIntegration/ # Coface API 数据同步
│   │   │   ├── BppIntegration/    # BPP 审批集成
│   │   │   ├── CreditItems/       # 评分项目校验
│   │   │   ├── CreditItemValue/   # 枚举值校验
│   │   │   └── CustomerTag/       # 标签初始化
│   │   ├── WebResources/JS/       # 6 个实体表单脚本
│   │   └── Entities/              # 实体定义 JSON
│   ├── Tools/
│   │   ├── MetadataTool/          # C# CLI 工具 (实体/字段/表单/视图/Plugin/JS 部署)
│   │   ├── DeployTool/            # C# CLI 工具 (Plugin/WebResource/BPP 诊断/环境修复)
│   │   ├── CofaceConfigImporter/  # C# CLI 工具 (Coface 配置数据导入)
│   │   └── SolutionViewer/        # Node.js 解决方案可视化浏览器
│   └── SanyD365Project/           # 主项目 (Playwright E2E 测试 + 配置)
│
└── Documents/                     # 所有文档资料
    ├── PRD/                       # 产品需求文档
    ├── BusinessAnalysis/coface/   # Coface 接口分析 (10+ 文档)
    ├── DataDictionary/            # 数据字典 v14
    ├── DevelopmentStandards/      # D365 定制规范 (丁波)
    ├── Planning/                  # ⭐ 开发计划.md (功能-测试-Bug 关联总表)
    ├── Tests/                     # ⭐ 测试用例总集.md (169 条用例)
    │   └── BugReports/            # ⭐ 测试Bug反馈记录.md (8 个 Bug)
    ├── DailyReports/              # ⭐ 工作日报汇总.md (5 天记录)
    └── Methodology/               # D365+AI 实施方法论
```

### 2.0.1 功能代码索引（2026-07-06 新增）

> **新增文件**：`Code/INDEX.md`
>
> **作用**：按「模块 → 功能点 → 代码路径」组织的项目功能代码索引，作为 AI 后续修改代码时的快速检索入口，也是项目移交时的代码地图。
>
> **维护约定**：
> - 每次新增、删除、迁移功能点或重命名代码文件时，必须同步更新 `Code/INDEX.md`。
> - AI 在修改 `Code/` 下任何源代码前，应先查阅 `Code/INDEX.md`，确认相关功能点与代码路径。
> - 索引范围不包括构建产物（`bin/`、`obj/`）、Solution 解包产物（`solutions/unpacked*/`）、第三方依赖（`node_modules/`、`venv/`）等。
> - 索引维护属于代码修改的一部分，应纳入开发完成检查清单。

### 2.0 AI 协作红线

> 详细规则见 `/skill:d365-deploy` 第 0 章。
>
> - **AI 代码提交铁律**：除非用户明确说出"提交"或"推送"二字，否则 AI 不得执行任何 Git 操作。
> - **McsPlugin 解决方案红线**：`McsPlugin` 里只能放 Plugin 和 Step，不能放实体/字段/WebResource 等其他组件。
> - **Git 推送约定**：默认推送到项目 Git（Azure DevOps `uat`），只有用户明确说 push 到个人 Git 时才推送到 `origin`。
> - **PublishAll 执行红线**：凡是需要 `PublishAll`（全局发布）的操作，由用户执行；AI 只运行指定实体的发布（如 `dotnet run publish mcs_customer_tag`），不运行无实体参数的 `dotnet run publish` 或等效全局发布命令。
> - **🚨 Plugin Assembly 更新红线（2026-06-25 新增）**：更新 DEV Assembly 前，必须先将代码推送到项目 Git 并合并到 `uat`，再用合并后的 `uat` 代码重新编译 DLL。严禁使用未入仓代码编译的 DLL 更新 DEV Assembly！
> - **🚨 绝对禁止覆盖公共/通用文件（2026-06-27 新增）**：AI 严禁直接覆盖任何文件内容，尤其是多人共用的通用文件（如 `ms_languagefile_1033/2052`、`1033.json`、`2052.json` 等语言包、AGENTS.md、开发规范文档等）。只允许在已有内容后追加。AI 不得擅自修改所有人公用的通用文件；如需修改，必须先获得用户逐字明确授权，并说明改动范围。

当前本地 `origin` 指向个人 GitHub（`https://github.com/qzw4549689/SanYLocalCode.git`），这是历史备份用途。

---

### 2.1 本地测试项目 → 远程主项目同步资产

当本地 `Code/Customizations/Plugins/` 下的独立项目（如 `CofaceIntegration`、`BppIntegration`）验证通过后，需要归并到远程主项目 `SanyD365.D365Extension.Sales` 进行发布：

| 资产 | 路径 | 用途 |
|---|---|---|
| 同步指南 | `Documents/DevelopmentStandards/D365-本地测试项目到远程主项目同步指南.md` | 命名空间映射、目录映射、C# 7.3 降级点、编译验证步骤 |
| 自动化脚本 | `Code/Tools/sync-plugin-to-remote.py` | 读取本地文件 → 替换命名空间 → scp 到远程 → 更新 csproj → 触发编译 |

**核心原则**：本地为源，远程为镜像。除远程 `PluginBase` 框架脚手架外，业务代码必须与本地一致。

**关键命令**：
```bash
# 预览同步计划
python3 Code/Tools/sync-plugin-to-remote.py --dry-run

# 输出到本地检查转换结果
python3 Code/Tools/sync-plugin-to-remote.py --output-to-local /tmp/sync-test

# 正式同步 + 编译 + 拉回 DLL
python3 Code/Tools/sync-plugin-to-remote.py --pull-dll Code/Customizations/Plugins/CofaceIntegration/SanyD365.D365Extension.Sales.dll
```

**编译验证**：单独编译目标项目即可，不要全量编译 `D365.sln`（测试项目缺 `Secret.json` 会失败）：
```powershell
cd C:\Projects\D365\D365\SanyD365.D365Extension.Sales
msbuild SanyD365.D365Extension.Sales.csproj /p:Configuration=Release /p:Platform=AnyCPU
```

### 2.2 最近一次发布记录（Coface 配置化 + BPP 回调修复）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-16 |
| PR | #3198 已合并到 `uat`（commit `f90b1b1dd3`） |
| 修改 Plugin | `CofaceIntegrationDataSyncPlugin`、`CreditRecordBppCallbackPlugin` |
| 远程编译 | `SanyD365.D365Extension.Sales.csproj` 单独编译成功 |
| DEV Assembly | 已更新 `SanyD365.D365Extension.Sales`，modified=`2026/6/16 00:18:53` |
| 发布实体 | 已发布 `mcs_credit_record` |
| 待办 | DEV 验证 → 用户手动 n8n 发布 `McsPlugin` 到 UAT |

### 2.3 当前进行中的工作（Coface 两个 Bug 修复）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-16 |
| 目标 Bug | 1. Full Report 内容接口缺少 `format` 参数导致 400<br>2. URBA360/Full Report 财务比率字段返回字符串，`GetDecimal()` 抛异常 |
| 状态 | ✅ 已合并到 `uat`，等待用户手动发布 UAT |
| 修改文件 | `CofaceApiService.cs`（加 `format=json`）<br>`Urba360Parser.cs`、`FullReportParser.cs`（`GetDecimal()` → `GetDecimalSafe()`）<br>新增 `JsonElementExtensions.cs` |
| Git 分支 | `uat-260616-peter-coface-bugfix` → `uat`（已合并） |
| Commit | `1e9a12901e` — `fix(credit): Coface API format参数缺失及财务比率字符串解析兼容` |
| 本地编译 | ✅ 通过（`0 个错误，0 个警告`） |
| 远程编译 | ✅ 通过（项目原有警告，无新增错误） |
| DEV Assembly | ✅ 已更新，`modifiedon` = `2026-06-16 02:21:16`，无类型差异错误 |
| DEV 测试 | ✅ `SCO202606160004` 测试通过：<br>• Full Report 接口 `Status=OK`，不再 400<br>• 资产负债率 `DebtRatio=12.74`<br>• 流动比率 `CurrentRatio=7.74` |
| 清理 | ✅ 已注销 DEV 临时 Assembly `SanyD365.Plugins.CofaceIntegration` |
| 注意 | 当前实现与已合并的 PR !3201 实现不同，用户决定用当前实现并必要时 reverse PR !3201 |
| 下一步 | 用户手动通过 n8n Release Tool 发布 UAT（解决方案勾选 `McsPlugin`，Azure代码不勾选） |

### 2.4 当前进行中的工作（Coface Report 产品选择逻辑修正）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-18 |
| 目标 | 按截图修正 Coface Report 产品选择逻辑，39 国列表与截图对齐 |
| 状态 | ✅ 已推送到新分支，等待用户合并 PR |
| 修改文件 | `CofaceCountryConfig.cs`（重构配置模型）<br>`CofaceApiService.cs`（`GetReportOrders` 增加 `productSlug`/`productCode` 过滤）<br>`CofaceDataSyncPlugin.cs`（按国家类型选择 Report 产品并过滤 publication）<br>`CofaceConfigDeployer.cs`（39 国列表与截图对齐，增加 productCode） |
| Git 分支 | `uat-260618-peter-coface-report-product` |
| Commit | `a06e8c0606` — `fix(coface): Report产品按国家类型选择slug/productCode，39国列表与截图对齐` |
| 远程编译 | ✅ 通过（项目原有警告，无新增错误） |
| UAT 配置 | ✅ 已用 DeployTool 更新 `ms_systemconfiguration.CofaceCountryConfig`（新 JSON 结构） |
| DEV 配置 | ✅ 已用 DeployTool 更新 `ms_systemconfiguration.CofaceCountryConfig` |
| DEV Assembly | ✅ 已用 MetadataTool `update-assembly` 更新，`modifiedon` = `2026-06-18 00:28:19` |
| DEV 验证 | ✅ PL 测试记录 `SCO202606160004` 重新触发状态 11，CofaceDataSync 返回 SUCCESS，7 个标签正常生成，Report 订单匹配到新 publication |
| 产品选择逻辑 | 非受限国家：`customized-report` + `301`<br>受限国家（39 国，除 RU）：`full-report`<br>RU：`customized-report` + `21000` |
| URBA360 | 保持不变，所有国家仍调用 `/urba360/monitorings/orders` |
| 下一步 | 用户通过 n8n Release Tool 发布 `McsPlugin` 到 UAT |

---

### 2.5 当前进行中的工作（评分卡多行配置改造）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-18 |
| 目标 | 支持同一评分项目下多行评分卡配置；缺失值取平均分；创建/更新评分卡时校验重复/重叠 |
| 状态 | ✅ 多行配置 PR 已合并；Create 校验 Bug 已修复并合并；DEV1 评分卡配置已全量导入 |
| 修改文件 | `ScoreCalculator.cs`（按 `ItemCode` 分组匹配、缺失值先匹配再取平均、允许负分）<br>`CreditScoringCardValidationPlugin.cs`（新增 Create/Update PreOperation 校验；修复 Create 时 Retrieve 未创建记录的 Bug）<br>`SanyD365.D365Extension.Sales.csproj`（加入新 Plugin） |
| Git 分支 | `uat-20260618-peter-scoringcard-multiline`（已合并到 `uat`）<br>`uat-20260618-peter-fix-scoringcard-validation`（已合并到 `uat`） |
| Commit | `9363153dcb` — `feat(scoringcard): 支持多行评分卡配置、缺失值平均分、创建时重叠校验`<br>`7daf0d0da6` — `fix: 评分卡校验Plugin在Create时不再Retrieve未创建的记录` |
| 远程编译 | ✅ 通过（0 错误，项目原有警告） |
| DEV1 Assembly | ✅ 已更新 `SanyD365.D365Extension.Sales`，`modifiedon` = `2026-06-18 06:49:00` 左右 |
| DEV1 Plugin Steps | ✅ 已注册：<br>• Create PreOperation of `mcs_credit_scoringcard`<br>• Update PreOperation of `mcs_credit_scoringcard` |
| 已发布实体 | ✅ `mcs_credit_scoringcard` |
| DEV1 评分卡配置 | ✅ 已清空并全量导入 448 条（来自 `Documents/BusinessAnalysis/评分卡因子.xlsx` 7 套评分卡），覆盖 7 种客户类型；`mcs_weight` 字段范围已调整为 `[-100, 100]`；`ExternalRating` 已改为定量 |
| 数据格式评估 | 已结合 `Documents/BusinessAnalysis/评分卡因子.xlsx` 评估：当前 `[min, max)` 模型可覆盖大部分区间分档；定性多值需拆行；显式“无/未提供资料/无评分”配置为普通分档行（权重 0）；缺失/未命中时取该项目平均分；诉讼记录暂按数量配置；预计损失率由用户算好后直接填入 |
| 下一步 | 在 DEV1 选取测试信用评估记录（如 `SCO202606180002`）重新计算，验证 7 套评分卡得分与配置是否一致；确认 `mcs_credit_scoringcard.js` WebResource 发布 |

---

### 2.6 已废弃的 PR `!3530`（从业年限改动）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-18 |
| 目标 | 按业务文档，将"从业年限"计算逻辑改为基于 Coface `registration.date` 计算距今年数 |
| 状态 | ✅ **已完成**。新 PR `!3536` 已合并到 `uat`，DEV Assembly 已更新。原 PR `!3530` 已废弃并删除。 |
| Git 分支 | `uat-20260618-peter-registrationdate-years` → `uat`（PR `!3536` 已合并） |
| Commit | `13a9c2beaf` / `5b4dd25773` — `fix(coface): 从业年限按文档从registration.date计算年数` |
| 冲突文件 | 无（PR `!3536` 仅修改 1 个文件） |
| 远程编译 | ✅ 通过（项目原有警告，无新增错误） |
| DEV Assembly | ✅ 已更新 `SanyD365.D365Extension.Sales`，ID: `9d6ff315-8c03-4d51-b641-ebeccf9e98b0`，DLL 大小 8043 KB |
| DEV 验证 | ✅ `SCO202606160004`（PL 测试记录）重新触发状态 11，`CofaceDataSync` 返回 SUCCESS。生成 14 个标签（含多行配置分档），其中：<br>• **从业年限 (RegistrationDate) = 24**<br>• 注册资本 (RegisteredCapital) = 100000<br>• 诉讼债权金额 (LegalEvents) = 1<br>• 净利润率 (NetProfit) = 3.62<br>• 行业属性 (Sectors) = 建工 |
| 教训 | **本地 SanYi 仓库禁止推送至 Azure DevOps D365 项目仓库**。所有 D365 项目代码的 PR 必须在远程服务器 `tx-windows`（`C:\Projects\D365`）上操作。 |
| UAT 评分卡导入 | 🔄 已执行 `import-scoring-cards` 到 UAT：成功 374 条，跳过 74 条。失败原因：<br>1. UAT `mcs_weight` 字段范围仍为 `[0, 99]`（DEV 已改为 `[-100, 100]`），导致权重为 -3/-1 的记录创建失败<br>2. UAT `mcs_credititem_value` 缺少 CountryRisk/SectorRisk 枚举值（A1/A2/A3/A4/B/C/D/E、4），导致对应记录跳过 |
| 下一步 | 1. 用户通过 n8n Release Tool 发布 `McsPlugin` 到 UAT<br>2. 修复 UAT `mcs_weight` 字段范围和 `mcs_credititem_value` 枚举值后，重新导入评分卡 |

---

### 2.7 当前进行中的工作（成交条件样板库）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-21 / 2026-06-23 |
| 目标 | 新增成交条件样板库模块：产品分类、产品分类关系、样板库主表、批量审批、公共查询接口 |
| 状态 | ✅ 阶段 2/3 已发布到 DEV1 并验证通过；修复分支已合并到 `uat`；DEV1 Assembly 已与 `uat` 对齐<br>✅ 阶段 4（公共查询接口）Custom API 已完成本地独立开发、DEV1 验证、临时 Assembly 清理，并已推送远程分支等待合并 |
| 涉及实体 | `mcs_trade_pttype`（成交条件产品分类）<br>`mcs_trade_ptgrouptype`（成交条件产品分类关系）<br>`mcs_trade_stpayterm`（成交条件样板库） |
| 修改文件 | **阶段 2/3**：`Code/Tools/MetadataTool/Definitions/mcs_trade_pttype.json`<br>`Code/Tools/MetadataTool/Definitions/mcs_trade_ptgrouptype.json`<br>`Code/Tools/MetadataTool/Definitions/mcs_trade_stpayterm.json`<br>`Code/Customizations/WebResources/JS/mcs_trade_stpayterm.js`（新增 `TradeStPayTermGrid` 批量申请/审批/拒绝）<br>`Code/Customizations/Plugins/TradeStPayTerm/AutoNumber/TradeStPayTermAutoNumberPlugin.cs`<br>`Code/Customizations/Plugins/TradeStPayTerm/Validation/TradeStPayTermValidationPlugin.cs`<br>`Code/Tools/MetadataTool/Program.cs`（增加 `test-tradestpayterm` 测试命令）<br>`Code/Tools/MetadataTool/Services/EntityManager.cs`<br>`Code/Tools/DeployTool/AppActionDeployer.cs`（新增批量申请/审批/拒绝 App Action + `location` 参数）<br>`Code/Tools/DeployTool/Program.cs`（新增 `tradestpayterm`/`tradestpayterm-wr` 命令）<br>`Code/Tools/DeployTool/DeployPlugin.cs`（新增 `DeployTradeStPayTermPlugin`）<br>`Code/Tools/sync-plugin-to-remote.py`（增加 TradeStPayTerm 文件/命名空间映射）<br><br>**阶段 4**：`Code/Customizations/Plugins/TradeStPayTerm.Api/QueryTradeStPayTermPlugin.cs`<br>`Code/Customizations/Plugins/TradeStPayTerm.Api/TradeStPayTermQueryService.cs`<br>`Code/Tools/MetadataTool/Services/CustomApiDeployer.cs`（Custom API 注册/更新/删除）<br>`Code/Tools/MetadataTool/Services/EntityManager.cs`（`RegisterPluginAssemblyOnly`）<br>`Code/Tools/MetadataTool/Program.cs`（`deploy-tradestpayterm-api` / `delete-tradestpayterm-api` / `test-tradestpayterm-api` / `check-solution-customapi` / `query-optionset` 等命令） |
| 阶段 4 实现 | **接口形态**：Custom API（D365 内部接口，符合 PRD 表格 16/17）<br>**唯一名**：`mcs_QueryTradeStPayTerm`<br>**本地开发方式**：先本地独立项目 `SanyD365.Plugins.TradeStPayTerm.Api` 开发验证，再归并到远程 `SanyD365.D365Extension.Sales`<br>**临时 Assembly**：`SanyD365.Plugins.TradeStPayTerm.Api`（已清理）<br>**最终归属**：`McsCustomAPI` 解决方案 + `SanyD365.D365Extension.Sales` Assembly<br>**工具扩展**：MetadataTool 增加 Custom API 部署与测试命令<br>**输入参数**：`mcs_buid` / `mcs_subid` / `mcs_countrycode` / `mcs_prdgroupid` / `mcs_buyercode`<br>**输出参数**：`status` / `message` / `records`(JSON)<br>**客户分类映射**：经销商分级 `mcs_dealerrank` 1-5 → D1-D5；直销客户 `mcs_accountlevel` 4/3/2/1 → S/A/B/C；个人客户判断待业务确认 |
| 解决方案 | `entity_20260603_peter`（实体/WebResource/App Action）、`McsPlugin`（Plugin/Step）、`McsCustomAPI`（阶段 4） |
| DEV1 状态 | ✅ 阶段 1/2 实体/字段/表单/视图/JS 已发布<br>✅ `SanyD365.D365Extension.Sales` Assembly 已更新，`TradeStPayTermAutoNumberPlugin` + `TradeStPayTermValidationPlugin` Steps 已注册<br>✅ 4 个 App Action 按钮已部署（克隆新增 + 批量申请/审批/拒绝）<br>✅ `mcs_trade_stpayterm.js` 已发布<br>✅ DEV1 自动化验证通过（自动编号、校验、状态流转、重复校验）<br>✅ 阶段 4 Custom API `mcs_QueryTradeStPayTerm` 已归并到主 Assembly 并在 DEV1 注册，调用成功并返回匹配记录<br>✅ Custom API / 请求参数 / 响应属性已加入 `McsCustomAPI` 解决方案<br>✅ DEV1 临时 Assembly `SanyD365.Plugins.TradeStPayTerm.Api` 和临时 Custom API 已清理 |
| 修复记录 | 验证时发现 `mcs_creditgrade` 改为 Picklist 后，`ValidationPlugin` 仍按 string 读取导致转换异常。已修复：按业务规则将 `mcs_creditgrade` 从重复校验中移除。<br>修复分支：`uat-260623-peter-tradestpayterm-fix`（commit `936420eabe`，已合并到 `uat`）<br><br>批量按钮隐藏 bug 修复：未勾选时显示、勾选记录后消失的根因是 `appaction` JS 参数误配为 `PrimaryControl`，且 Visibility 需用 Power Fx。修复方案：<br>1. `mcs_trade_stpayterm.js` 的 `apply/approve/reject` 改为接收 `SelectedControlSelectedItemIds`（选中记录 ID 数组或逗号分隔字符串）<br>2. Command Designer 中 4 个批量按钮的 Visibility 设为 `CountRows(Self.Selected.AllItems) > 0`<br>3. Command Designer 中参数改为 `SelectedControlSelectedItemIds`<br>已在 DEV1 `Sany CRM Sales` App 验证通过 |
| 阶段 3 新增内容 | JS：`TradeStPayTermGrid.apply`/`approve`/`reject` + `batchUpdateStatus`，调用 `Xrm.WebApi.updateRecord` 更新 `mcs_status`<br>App Action：`mcs_trade_stpayterm_apply`（批量申请，未生效→待审批）、`mcs_trade_stpayterm_approve`（批量审批，待审批→生效）、`mcs_trade_stpayterm_reject`（批量拒绝，待审批→未生效），均位于列表命令栏 |
| 按钮权限控制（测试） | JS：`mcs_trade_stpayterm.js` 已增加 `hasCloneButtonPermission` / `hideCloneButtonIfNoPermission`，但当前硬编码为 `System Administrator` / `Regional Sales Representative`，与最新角色权限设计不符；需后续按新设计改为 `成交条件制定人` / `成交条件审批人` 角色判断 |
| 文档 | `Documents/Planning/成交条件样板库_阶段1实施方案.md`<br>`Documents/Planning/成交条件样板库_阶段2-5实施方案.md`<br>`Documents/BusinessAnalysis/成交条件产品分类_示例数据.xlsx`<br>`Documents/DevelopmentStandards/三一D365开发规范文档.md` |
| Git 分支 | `uat-260621-peter-tradestpayterm`（已合并到 `uat`）<br>`uat-260623-peter-tradestpayterm-fix`（已合并到 `uat`）<br>`uat-260623-peter-tradestpayterm-api`（已合并到 `uat`） |
| 关键确认 | 审批机制：D365 内批量按钮；客户等级：暂不参与查询/重复校验；重复校验：交集即重复；编码规则：`TC`+`YYMMDD`+2 位序号；产品分类 Lookup 引用 `mcs_trade_pttype` 而非 `mcs_productline`；默认 EUR 逻辑暂不修改；阶段 4 采用 Custom API（D365→D365 内部接口） |
| 待确认 | 1. 泵路事业部真实编码（当前代码用 `BU-1018` 占位）<br>2. 个人客户判断逻辑（客户主数据表无 `mcs_customertype` 字段） |
| 权限设计更新（2026-06-24） | 业务提供最新角色权限矩阵：<br>• **成交条件制定人**（大/国区相关管理人员、事业部海外风控人员/事业部海外营销管理部管理人员）：数据范围为本部门及下级部门，可发起成交条件配置和申请审批<br>• **成交条件审批人**（总部相关交易风险管理部门/大/国区执委会/事业部董事会）：数据范围为全集团，可审核成交条件配置数据<br><br>文档已更新：`Documents/Planning/成交条件样板库_阶段2-5实施方案.md` 3.3 节 |
| 权限实现差距 | 当前代码尚未按新设计落地，差距记录在上述文档中：<br>• 前端按钮权限仍为旧角色硬编码<br>• 后端 `TradeStPayTermValidationPlugin` 未按角色校验状态流转<br>• `TradeStPayTermQueryService` 未按用户部门/角色做数据范围过滤<br>• 新建记录时未显式设置 owner 以支持业务部门级权限 |
| 下一步 | 1. ✅ 用户已合并 PR `uat-260623-peter-tradestpayterm-api` 到 `uat`<br>2. ✅ 已在 DEV1 部署归并后的 `SanyD365.D365Extension.Sales` Assembly<br>3. ✅ 已用 MetadataTool 注册 Custom API 到 `McsCustomAPI` 解决方案并验证通过<br>4. ✅ 批量按钮隐藏 bug 已在 DEV1 修复并验证<br>5. ✅ UAT 已重新发布 `McsCustomAPI`（单独勾选），`mcs_QueryTradeStPayTerm` Custom API 调用验证通过<br>6. ⏸️ 注意 `Sany CRM Sales` App 的 Command Component Library 同步到 UAT（如需）<br>7. ⏸️ 按 2026-06-24 新角色权限设计修复 TradeStPayTerm 前端/后端权限控制 |

---

### 2.8 当前进行中的工作（Coface Report PDF 平台上传改造）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-21 |
| 目标 | 解决 Coface Report PDF 直接写入 `mcs_customer_file.mcs_filebyte`（Memo 字段 MaxLength=4000）导致超长报错的问题 |
| 状态 | ✅ 本地代码改造完成，编译通过；待 DEV1 验证 |
| 修改文件 | `Code/Customizations/Plugins/CofaceIntegration/Plugin/CofaceDataSyncPlugin.cs` |
| 核心改动 | `SaveCofaceReportAttachment` 改为：<br>1. 不再将 PDF Base64 写入 `mcs_filebyte`<br>2. 调用 `mcs_InitUploadFile`（EntityName=`mcs_credit_record`, Type=`002`）获取 uploadId + Blob URL<br>3. HTTP PUT 上传 PDF 字节到 Blob URL（`x-ms-blob-type: AppendBlob`）<br>4. 调用 `mcs_CommitUploadFile` 完成上传<br>5. 创建 `mcs_customer_file` 记录，`mcs_api_fileid`=uploadId，`mcs_credit_recordid`=当前评估记录，`mcs_filetype`=2（客户资信报告） |
| 本地编译 | ✅ 通过（0 个错误，0 个警告） |
| DEV1 验证 | ✅ 通过。测试记录 `SCO202606180003`（ID: `926cdd86-4eec-4abd-8037-f7a44f803211`）状态 10→11 触发后：<br>• `mcs_api_msg` 显示 `[Report附件已保存]`<br>• 成功创建 `mcs_customer_file` 记录（ID: `7ca84d25-fd6d-f111-ab0f-7ced8db4dda8`）<br>• 文件名：`Coface_Report_SCO202606180003_20260622.pdf`<br>• 文件类型：`2`（客户资信报告）<br>• `mcs_api_fileid` = `dc49d703-2b01-4fbe-b767-10198a8b29de`（平台 uploadId）<br>• `mcs_filebyte` 长度为 `0`，确认未直接写入 Base64<br>• `mcs_api_msg` 记录 publicationId 与 originalSize=168294 bytes |
| DEV1 清理 | ✅ 已注销临时独立 Assembly `SanyD365.Plugins.CofaceIntegration` |
| 前端验证 | ✅ 通过。`mcs_credit_record` 为所有者的附件可在 Uploader 中展示，`Coface_Report_SCO202606180003_20260622.pdf` 下载后内容正常 |
| 远程同步 | ✅ 已同步到远程服务器 `tx-windows` 主项目 `SanyD365.D365Extension.Sales` |
| 远程编译 | ✅ 通过（项目原有警告，无新增错误） |
| Git 分支 | ✅ 已推送 `uat-20260622-peter-coface-report-upload` → Azure DevOps，用户已合并到 `uat` |
| 涉及文件 | `D365/SanyD365.D365Extension.Sales/Plugins/CofaceIntegration/CofaceIntegrationDataSyncPlugin.cs`<br>`D365/SanyD365.D365Extension.Sales/Application/Sales/CofaceIntegration/CofaceApiService.cs`（新增 `GetReportPdf` 等 PDF 下载方法） |
| DEV1 Assembly | ✅ 已更新 `SanyD365.D365Extension.Sales`，`modifiedon` = `2026-06-22 07:50:46 UTC` |
| 待确认 | `account` 实体作为附件所有者时 `mcs_InitUploadFile` 仍报类型不合法，当前方案使用 `mcs_credit_record` 作为所有者，前端已验证可展示 |
| 下一步 | 1. 用户发布相关实体（如需）<br>2. 用户通过 n8n Release Tool 发布 `McsPlugin` 到 UAT |

---

### 2.9 当前进行中的工作（客户信用标签 Owner 修复）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-24 |
| 目标 | 解决业务员账号（如 UATUser16）在 UAT 看不到 `mcs_credit_record` 子网格中生成的客户信用标签的问题 |
| 根因 | `CofaceDataSyncPlugin` 创建 `mcs_customer_tag` 时未显式设置 `ownerid`，标签负责人默认等于触发状态 11 的用户；当标签由管理员/系统上下文生成时，业务员因权限不足无法查看 |
| 状态 | ✅ PR 已合并到 `uat`；DEV1 Assembly 已更新；UAT 发布待用户手动执行 |
| 修改文件 | `Code/Customizations/Plugins/CofaceIntegration/Plugin/CofaceDataSyncPlugin.cs` |
| 核心改动 | 1. Retrieve `mcs_credit_record` 时增加 `ownerid` 字段<br>2. 创建 `mcs_customer_tag` 时显式设置 `ownerid` 为对应评估记录的负责人 |
| 本地编译 | ✅ 通过（0 个错误，0 个警告） |
| 远程编译 | ✅ 通过（项目原有警告，无新增错误） |
| Git 分支 | `uat-20260624-peter-coface-tag-owner` → `uat`（PR #3883 已合并，commit `dbec140285`） |
| Commit | `93c8cb8aa4` — `fix(coface): 客户信用标签负责人跟随评估记录负责人` |
| DEV1 Assembly | ✅ 已更新 `SanyD365.D365Extension.Sales`，ID: `9d6ff315-8c03-4d51-b641-ebeccf9e98b0` |
| 注意事项 | 同步过程中发现本地 `Urba360Parser.cs` 仍为旧注释（配置表汇率），已清理，未引入到本次 PR；AI 未擅自更新 UAT Assembly |
| UAT 测试用例 | ✅ 已重构 `Documents/Tests/测试用例总集.md`：明确 TC-FLOW-001~010 执行用户，新增 TC-ROLE-001~010 角色权限用例，测试环境改为 UAT；页面表单逻辑按功能模块融入对应用例步骤，不再单独成节；第二部分按信用评估模块主线重新组织为基础数据、核心流程、客户主数据联动辅助验证，Coface ID 校验移回评估记录流程；全文档清理 Plugin/JS/API/Trace 等技术术语，字段名改用中文业务名，面向业务测试人员 |
| 下一步 | 1. 用户通过 n8n Release Tool 发布 `McsPlugin` 到 UAT（Azure 代码不勾选）<br>2. 在 UAT 用业务员账号验证 `SCO202606240001` 等记录的标签可见性<br>3. 按重构后的 UAT 用例执行角色权限测试 |

---

### 2.10 当前进行中的工作（成交条件样板库国家/产品分类多选改造）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-25 |
| 目标 | 将 `mcs_trade_stpayterm` 的国家、产品分类从单选 Lookup 改为多行文本存储 GUID 逗号分隔，以支持一条样板库记录匹配多个国家/产品分类 |
| 状态 | ✅ 阶段 1（前端 JS）已完成并发布到 DEV1<br>✅ 阶段 2（后端 `TradeStPayTermValidationPlugin` 重复校验）已完成代码合并、DEV1 Assembly 更新并验证通过<br>✅ 阶段 3（`TradeStPayTermQueryService` Custom API 查询匹配）已完成代码合并、DEV1 Assembly 更新并验证通过 |
| 元数据变更 | `mcs_nation` / `mcs_trade_pttype`（Lookup）已删除；`mcs_countries` / `mcs_trade_type` 改为多行文本（Memo），存储 GUID 逗号分隔；分隔符为英文逗号 `,`，无空格；空值视为 NA 通配 |
| 修改文件 | **阶段 1/2**：`Code/Customizations/WebResources/JS/mcs_trade_stpayterm.js`（新增 `MULTISELECT_LOOKUP_CONFIG` 多选查找组件，同步 `mcs_countries`/`mcs_trade_type` 与辅助字段 `mcs_countrycode`/`mcs_typeid` 等；克隆/回填适配）<br>`Code/Customizations/Plugins/TradeStPayTerm/Validation/TradeStPayTermValidationPlugin.cs`（重复校验触发字段改为 `mcs_countries`/`mcs_trade_type`；`IsDuplicate` 使用 GUID 集合交集判断；NA 通配为空字符串）<br><br>**阶段 3**：`Code/Customizations/Plugins/TradeStPayTerm.Api/TradeStPayTermQueryService.cs`（Custom API 查询逻辑改为：国家代码→`mcs_country` GUID；产品线→`mcs_trade_ptgrouptype`→`mcs_trade_pttype` GUID；内存中按 GUID 集合交集匹配 `mcs_countries`/`mcs_trade_type`；返回时将 GUID 列表转换为编码/名称逗号分隔） |
| Git 分支 | `uat-20260625-peter-tradestpayterm-multiselect` → `uat`（已合并 PR 4004）<br>`uat-20260625-peter-tradestpayterm-api-multiselect` → `uat`（已合并 PR 4005） |
| DEV1 操作 | ✅ `SanyD365.D365Extension.Sales` Assembly 已用合并后的 `uat` 代码在 `tx-windows` 编译并更新（Assembly ID: `9d6ff315-8c03-4d51-b641-ebeccf9e98b0`，ModifiedOn: `2026-06-25 14:33:34`）<br>✅ 已发布实体 `mcs_trade_stpayterm` |
| DEV1 验证 | **重复校验（阶段 2）**：<br>✅ 相同国家/产品分类 GUID 集合创建重复记录被拦截<br>✅ 国家 GUID 集合有交集即被拦截<br>✅ 产品分类 GUID 集合有交集即被拦截<br>✅ 国家/产品分类 GUID 集合无交集可正常创建<br>✅ 空国家字段按 NA 通配，与任意国家匹配时视为重复<br><br>**Custom API 查询（阶段 3）**：<br>✅ 调用 `mcs_QueryTradeStPayTerm` 成功（status=1）<br>✅ 创建测试记录（中国 001 + AK/01 + buyerGrade=C）并生效后，Custom API 正确返回该记录<br>✅ 返回字段 `countryCode`/`countryName`/`typeId`/`typeName` 已按 GUID 转换为编码/名称逗号分隔（如 `001,中国,01,燃油牵引车`）<br>✅ 清理测试记录后，使用相同入参查询返回空结果，符合预期 |
| 下一步 | 1. 阶段 1/2/3 均已完成并在 DEV1 验证通过<br>2. 如有后续需求（如权限控制、UAT 发布、数据迁移），请按需安排 |

---

### 2.11 当前进行中的工作（中英多语言改造）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-27 |
| 目标 | 将客户信用评估模块（含成交条件相关实体）从单中文显示改造为支持英文(1033) + 简体中文(2052)双语显示 |
| 策略 | 元数据翻译为主（Solution Translations 导入），前端语言包为辅（WebResource JSON）；前端语言包采用"先独立测试文件、后合并公共文件"策略 |
| 已完成 | ✅ `mcs_trade_pttype` 试点实体翻译总表填充、DEV1 translations 导入、实体发布、字段/视图双语验证通过<br>✅ `mcs_trade_ptgrouptype` 翻译总表填充、DEV1 translations 导入、实体发布、字段/视图双语验证通过<br>✅ `mcs_trade_stpayterm` 翻译总表填充、DEV1 translations 导入、实体发布、字段/视图/选项集双语验证通过（含 2052 回填修复）<br>✅ `mcs_coface_nace_mapping` / `mcs_coface_financial_indicator` / `mcs_credititem_value` 翻译总表填充、DEV1 translations 导入、实体发布、字段/视图/选项集双语验证通过<br>✅ `mcs_credit_items` / `mcs_credit_scoringcard` 翻译总表填充、DEV1 translations 导入、实体发布、字段/视图/选项集双语验证通过<br>✅ `mcs_customer_tag` / `mcs_credit_record` 翻译总表填充、DEV1 translations 导入、实体发布、字段/视图/选项集双语验证通过<br>✅ `mcs_trade_pttype` 补漏：修复实体显示名/复数名/主键字段/业务字段 2052 缺失、1033 残留中文问题，重新导入并发布<br>✅ `mcs_credititem_value.mcs_listname` 中文修正为“选择项名称”，英文为“Option Name”<br>✅ `MetadataTool` 新增 `export-translations` / `import-translations` 命令入口<br>✅ `D365ToolCommon` 新增 `TranslationService`，统一封装 `ExportTranslation` / `ImportTranslation`，避免工具项目重复实现<br>✅ `MetadataTool` 新增 `list-webresources [前缀]` 命令（默认前缀 `ms_languagefile`），可列出 DEV1 现有语言文件 WebResource<br>✅ `D365ToolCommon.WebResource.WebResourceService` 新增 `ListByPrefix`，供工具项目复用<br>✅ 前端语言包实验：创建 `mcs_language_helper.js` + `ms_languagefile_credit_test_1033/2052`，并在 `mcs_credit_scoringcard.js` 中用 `LanguageHelper.getLabel` 替换金额提示文本；已部署到 DEV1 并发布（修复：用 script 标签注入 helper，避免 `new Function` 导致 `LanguageHelper` 未进入全局作用域）<br>✅ App Action 按钮实验：通过 `entity_20260603_peter` translations 导出/编辑/导入，为 `mcs_credit_scoringcard_clone` 按钮添加 1033 英文标签 "Clone New" 及英文 tooltip<br>✅ 实验验证通过：英文用户下金额提示显示英文、【克隆新建】按钮显示 "Clone New"；中文用户恢复正常中文<br>✅ `mcs_language_helper.js` 已从测试语言包切回公共语言包 `ms_languagefile_1033/2052`，并重新部署发布<br>🔄 BPF 阶段名称翻译：标准 translations 导入对 `mcs_credit` BPF 实体显示名生效（1033=Credit Assessment / 2052=信用评估），但 BPF 顶部阶段条实际读取的是 `processstage.stagename`（该字段 `IsLocalizable=False` 且 `processstage` 记录不支持 Update），因此阶段名称未变；用户将自行在 D365 UI 中修改 BPF 阶段名称为英文/双语方案 |
| 翻译总表 | `Documents/Planning/多语言翻译总表_v1.xlsx`（已补充 `WebResourceKeys` 工作表，含 47 个前端语言键） |
| 实施方案 | `Documents/Planning/D365中英多语言实施方案.md`（已更新 3.1 G 前端 WebResource 清单、4.3 语言包 key 规范与 helper 示例、4.3.2 独立测试文件策略） |
| DEV1 现有语言包 | `ms_languagefile_1033`、`ms_languagefile_2052` 等 12 个公共语言包；`ms_languagefile_lb_*` 等 9 个模块专用语言包（详见实施方案 5.2） |
| 已完成 | ✅ 用户已将 5 个 `CreditScoringCard_*` key 追加到 DEV1 公共语言包 `ms_languagefile_1033/2052`，双语验证通过<br>✅ 测试 WebResource `ms_languagefile_credit_test_1033/2052` 保留作为本地版本翻译文件备份 |
| `mcs_customer_file` | 为他人开发实体，本批次跳过 |
| 经验教训 | 见下方「多语言翻译复用检查清单」 |

**多语言翻译复用检查清单（避免重复犯错）**：
1. **必须复用 `D365ToolCommon`**：任何新增工具能力（如 translations 导入/导出、Solution 操作）先放到 `D365ToolCommon` 对应命名空间（`TranslationService` / `SolutionComponentService`），再在 `MetadataTool` / `DeployTool` 中保留薄命令入口。禁止在工具项目里重复实现。
2. **Solution 组件操作完全由用户执行**：实体加入 Solution、非目标依赖组件移除等，全部由用户在 D365 UI 中手动完成。AI **禁止执行 `add` / `remove` Solution 组件**的命令，只能检查并提醒。
3. **导出 translations 前由用户确认 Solution 归属**：AI 先用工具检查目标实体是否在 `entity_20260626_peter_trans` Solution 中；如果不在，必须提醒用户手动添加。
4. **读取 `CrmTranslations.xml` 必须处理 `ss:Index`**：`Cell` 节点可能带 `ss:Index` 属性，不能用 `findall('Cell')` 的下标直接当列号。必须按 `ss:Index` 计算实际列索引，否则列错位会导致匹配失败或写错语言列。
5. **翻译 source 文本列不固定**：字段标签的 source 中文通常在 `2052` 列，但视图名称/表单标签的 source 中文可能在 `1033` 列（2052 为空）。匹配逻辑必须优先查 2052，fallback 到 1033。
6. **不要只信 JSON 定义文件**：DEV1 中实际字段可能与 `MetadataTool/Definitions/*.json` 不一致（如 `mcs_trade_ptgrouptype` 实际有 `mcs_productlineid`，JSON 中未列出）。操作前先用 `list-fields` 核对 DEV1 真实字段清单。
7. **`dotnet run` 传参规范**：使用 `dotnet run --no-build -- <命令> <参数>`；不要把 `--nologo` 等 dotnet 选项放到命令参数后面，否则会被程序当成子命令。
8. **更新翻译总表要回填真实 GUID**：视图/表单行先用 `(待从DEV导出)` 占位，导出 CrmTranslations.xml 后必须回填真实 GUID，保持 Excel 为唯一真相源。
9. **先备份再改 XML**：修改 `CrmTranslations.xml` 前必须备份（如 `CrmTranslations.xml.bak`），方便回滚和 diff。
10. **必须同时设置 1033 和 2052**：翻译导入脚本不能只顾 1033。对于 2052 为空的行，必须先把原中文回填到 2052，再把 1033 改为英文，确保双语共存。
11. **导入后逐个字段核对双语**：使用 `get-entity-displayname <实体名>` 检查每个自定义字段是否同时具备 `LCID=1033` 和 `LCID=2052`；缺失任一语言立即重新导出补全。
12. **定期复查早期实体**：translations 导入后，早期已验证的实体也可能因后续操作（重新发布、Solution 导入等）丢失 2052 或 1033 被重置为中文。每次批次完成后应抽样复查历史实体。
13. **前端语言包先独立测试再合并公共文件**：为避免污染现有公共语言包，先新建 `ms_languagefile_credit_test_*` 测试 WebResource 在 DEV1 验证；验证通过后，再将 key 合并到 `ms_languagefile_1033/2052`；测试 WebResource 保留作为本地版本翻译文件备份，不删除。
14. **🚨 绝对禁止覆盖公共/通用文件**：AI 严禁直接覆盖任何文件内容，尤其是多人共用的通用文件（如 `ms_languagefile_1033/2052`、`1033.json`、`2052.json` 等）。只允许在已有内容后追加；如需修改通用文件，必须先获得用户逐字明确授权。
15. **本地语言文件是唯一数据源（2026-07-20 新增）**：`Code/Customizations/WebResources/Language/1033.json` 和 `2052.json` 只放我们自己的翻译 key（当前 23 个），作为唯一真相源。任何翻译修改必须**先改本地文件，再以本地为准**逐条核对、只更新 DEV 语言包 WebResource 中有差异的 key（严禁整包覆盖 `ms_languagefile_1033/2052`）；同步前先导出 DEV 现有文件备份。新增 key 时同样先落地本地文件再同步。
16. **🚨 语言包修改的唯一通道（2026-08-01 用户两次强调，强制）**：**禁止直连修改任何环境（含 DEV1）的语言文件 WebResource**；唯一通道 = **修改远程服务器 tx-windows（C:\Projects\D365）仓库里的语言文件，走 git 分支 + PR 合并发布，发布后 DEV 和 UAT 都生效**。完整顺序：①先改本地唯一数据源 `Language/1033.json`+`2052.json`（纯追加）→ ②scp 下载远程仓库语言文件 → 本地纯追加 → scp 回传（保持 CRLF）→ ③远程建分支 `uat-YYYYMMDD-peter-langfile-xxx` commit + push（必须等用户说「提交/推送」）→ ④用户 PR 合并 → ⑤发布后 DEV/UAT 自动生效，**无需也不允许再单独动环境 WebResource**。误改环境必须立即用备份回滚（2026-08-01 已执行一次回滚并 MD5 验证复原）。


---

### 2.12 当前进行中的工作（信用评估记录弹窗 Bug + 重复客户校验 + 365 天失效 + 评分卡总分确认）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-28 |
| 涉及问题 | 1. 信用评估记录保存时弹窗重复显示<br>2. 信用评估记录保存时需校验相同客户是否存在在途评估记录<br>3. BPF 流程条字段需只读，且禁用 BPF 内置阶段跳转按钮<br>4. 评分卡配置中 SA 级老客户“定量 176 + 定性 41 = 217”被质疑不等于 100<br>5. 信用评估记录审批通过 365 天后应自动失效 |
| 弹窗 Bug 根因 | `mcs_credit_record` 主窗体 XML 中 `mcs_credit_record.js` 被重复引用，且 `onload`/`onsave` 事件各绑定两次，导致 `CreditRecordForm.onSave` 中的 `Xrm.Utility.alertDialog` 触发两次 |
| 弹窗 Bug 修复 | ✅ 已导出主窗体 XML，删除重复的 `mcs_credit_record.js` Library 引用，删除重复的 `onload`/`onsave` 事件 Handler，通过 `MetadataTool update-form-xml` 更新回 DEV1，并发布实体 `mcs_credit_record`；二次导出验证确认 `formLibraries` 仅剩 1 个 JS 引用，`events` 仅剩 1 个 `onload` 和 1 个 `onsave` |
| 评分卡总分分析 | DEV1 当前 408 条评分卡配置中，SA Existing Customer 共 59 条。按“所有档次权重加总”为 247（定量 206 / 定性 41），但按“单个客户逐项取最高档得分加总”为 130（补入 OverdueModel 30 分后）。Excel 中 SA 老客户历史交易包含 `OverdueModel`（预计损失率）30 分，计算公式为 `30 × 逾期未回收率模型分 / 100` |
| 365 天失效逻辑 | ✅ 已扩展 `Code/ServiceJobs/CreditRecordExpiration/CreditRecordExpirationService.cs`：扫描 `mcs_active=true` 且 `mcs_approvedate < 今天-365` 的评估记录，批量置 `mcs_active=false`；同时检查受影响客户是否还有其他未过期有效评估记录，无则将其客户主数据 `mcs_creditvalid` 设为 `false`；`Program.cs` 新增 `--execute`/`--diagnose`/`--seed-test-data` 参数 |
| DEV1 失效测试 | ✅ 使用 `seed-test-data` 构造 1 年前审批的测试记录，预览确认保护逻辑生效；`--execute` 执行后成功失效 2 条记录，并将 1 个客户主数据 `mcs_creditvalid` 改为 `false`；`show-profile` 验证客户画像 `mcs_creditvalid: False` |
| 代码文件 | `Code/ServiceJobs/CreditRecordExpiration/CreditRecordExpirationService.cs`<br>`Code/ServiceJobs/CreditRecordExpiration/Program.cs`<br>`Code/Customizations/WebResources/JS/mcs_credit_record.js`（修复弹窗重复 + 在途重复客户校验 + BPF 字段只读/阶段跳转禁用）<br>`Code/Tools/MetadataTool/Program.cs`（新增 `add-overdue-model-sa` 命令，用于为 SA 老客户补入 OverdueModel 配置）<br>`Code/Customizations/Plugins/CreditRecord/Validation/CreditRecordStatusTransitionPlugin.cs`（本地独立验证，已归并远程主项目）<br>`Code/Customizations/Plugins/CustomerTag/AutoNumber/CustomerTagValidationPlugin.cs`（主键跳过修复，已同步远程主项目） |
| 已完成 | 2. ✅ 已补入 SA 老客户 `OverdueModel`（预计损失率）配置：`CategoryId=1`, `Weight=30`, `Min=0`, `Max=100`, `DataType=100000000`（定量），`TypeId=4`（历史交易）。DEV1 评分卡总记录数从 407 增加到 408，实体 `mcs_credit_scoringcard` 已发布<br>5. ✅ 已实现 **相同客户在途评估记录重复校验**：在 `mcs_credit_record.js` 的 `onSave` 中，新建记录时异步查询相同客户（`mcs_accountid`）是否存在状态为 9-14（在途）的评估记录；若存在则阻断保存并提示“存在有重复客户评估记录，需核查！”<br>6. ✅ 已实现 **BPF 阶段跳转禁用（CSS 隐藏方案）**：`preventDefault` 对 BPF 阶段变更无效且全局事件拦截会误触发，改为在 `mcs_credit_record.js` 的 `blockBpfInteraction` 中注入 CSS 隐藏 BPF 弹窗/流程条中的“下一阶段”“上一阶段”按钮，并用 `MutationObserver` 兜底。DEV1 WebResource `mcs_credit_record.js` 已更新并发布<br>7. ✅ 已修复 **`CustomerTagValidationPlugin` 主键误判**：在 `ValidateReviewFieldsOnly` 中跳过 `mcs_customer_tagid`，避免人工复核阶段仅修改复核定量指标时报错“mcs_customer_tagid 不允许修改”；DEV1 临时 Assembly `SanyD365.Plugins.CustomerTag` 已更新验证<br>8. ✅ 已实现 **`CreditRecordStatusTransitionPlugin` 后端状态流转校验**：Create/Update PreOperation 校验 `mcs_status` 合法性，阻止 BPF 侧窗格/直接 API 的非法状态变更；本地临时 Assembly `SanyD365.Plugins.CreditRecord.Validation` 已注销，代码已归并到远程主项目 `SanyD365.D365Extension.Sales` 并编译通过 |
| 待业务确认 | 1. 评分卡是否要求“所有档次权重加总=100”？若要求，需重新设计导入逻辑（归一化）。<br>2. 是否要求各分类小计（定量/定性/交易历史等）与 Excel 表头严格一致？ |
| 待完成 | 1. 用户手动在 DEV1 UI 验证：保存弹窗是否只显示一次；新建相同客户在途评估记录时是否被阻断并提示“存在有重复客户评估记录，需核查！”；点击 BPF 阶段/下一阶段/上一阶段是否被物理阻止；BPF 流程条字段是否只读（字段只读暂未实现，后续处理）<br>2. 确认评分卡总分规则后，按需调整导入逻辑/配置<br>3. `OverdueModel` 计算逻辑：`ScoreCalculator.cs` 当前按区间直接取固定 `weight`，补入后 SA 老客户每个有 OverdueModel 标签的客户都会得 30 分（满分），而非 Excel 要求的 `30 × 模型分 / 100`。用户已确认先记下，后续再改<br>4. `CreditRecordExpiration` 目前是手动 Console 应用，需后续配置 Windows 计划任务 / Azure Function 等定时触发<br>5. ✅ BPF 阶段跳转已由 `CreditRecordStatusTransitionPlugin` 后端补齐；重复客户校验仍只在 JS 前端，若业务要求严格防绕过，后续再补充 Plugin 后端校验<br>6. BPF 流程条字段只读：当前 `lockBpfFields` 通过 `formContext.getControl` 锁定表单控件，但 BPF 弹窗中的字段仍为独立控件，用户确认先处理阻止下一步，字段只读后续再处理<br>7. ✅ 远程代码归并：分支 `uat-260628-peter-creditrecord-status-transition` 已合并到 `uat`，DEV1 Assembly 已更新，`CreditRecordStatusTransitionPlugin` 的 Create/Update PreOperation Steps 及 Update PreEntityImage 已注册<br>8. **CustomerTag 复核字段校验已按数据类型细化并优化错误提示部署到 DEV1**：已解决系统字段误拦截问题（加入完整系统字段跳过集合）；已删除遗留临时 Assembly `SanyD365.Plugins.CustomerTag`；已根据关联评分项目 `mcs_credit_items.mcs_datatype` 限制可修改字段：定量只允许 `mcs_itemintvalue2`，定性只允许 `mcs_credititem_value`；错误提示已按数据类型区分并统一后缀：定量提示“当前评分项目的数据类型=【定量】，复核阶段只可修改【复核定量指标】，其他字段不可修改”，定性提示“当前评分项目的数据类型=【定性】，复核阶段只可修改【复核定性指标】，其他字段不可修改”；分支 `uat-260628-peter-customertag-msg-v2` 已合并到 `uat`，DEV1 `SanyD365.D365Extension.Sales` Assembly 已更新；待用户在 DEV1 UI 最终验证 |

---

### 2.13 当前进行中的工作（厂端授信模块 — FcaProcActivationPlugin）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 实现厂端授信模型计算表 `mcs_fca_proc` 生效启用时自动回写客户额度 `mcs_fca_quota` 与初始化台账 `mcs_fca_records` |
| 涉及实体 | `mcs_fca_proc`（厂端授信模型计算表）<br>`mcs_fca_quota`（厂端授信额度表）<br>`mcs_fca_records`（厂端授信台账表） |
| 涉及文件 | `Code/Customizations/Plugins/FactoryCredit/ProcActivation/FcaProcActivationPlugin.cs`<br>`Code/Customizations/WebResources/JS/mcs_fca_proc.js`<br>`Code/Customizations/WebResources/JS/mcs_fca_records.js` |
| 实现要点 | 当 `mcs_fca_proc.mcs_status` 从非 3 变为 3（生效启用）时：<br>1. 按客户维度查询 `mcs_fca_quota`；存在则更新、不存在则创建<br>2. 回写字段：`mcs_sellergrant`（初始授信额度）、`mcs_sellerbalance`（可用余额）、`mcs_isactive=1`、`mcs_doid`、`mcs_fca_procid`<br>3. 创建 `mcs_fca_records` 初始化台账：`mcs_proccess=1`（厂端授信模型计算）、`mcs_adjust=1`（初始化） |
| Git 分支 | `uat-20260701-peter-fca-proc-activation` → `uat`（用户已合并）<br>`uat-20260710-peter-fca-custcode-fix` → `uat`（用户已合并，修复 `GetCustomerCode` 客户编码来源） |
| 远程编译 | ✅ 通过（项目原有警告，无新增错误） |
| DEV1 Assembly | ✅ 已用合并后的 `uat` 代码在 `tx-windows` 重新编译并更新 `SanyD365.D365Extension.Sales`（Assembly ID: `9d6ff315-8c03-4d51-b641-ebeccf9e98b0`，ModifiedOn: `2026-07-10 10:54:34`） |
| DEV1 Plugin Steps | ✅ 已注册：<br>• `FcaProcActivationPlugin`: Update of `mcs_fca_proc`，Stage=PostOperation，Mode=Sync，Filter=`mcs_status`（Step ID: `f8f21c6f-fc74-f111-ab0e-7ced8de4eab4`）<br>• PreEntityImage `PreImage`（Alias=`PreImage`，字段=`mcs_status`，Image ID: `6268afdb-fc74-f111-ab0e-7ced8db4d37f`） |
| DEV1 验证 | ✅ 独立 Assembly `SanyD365.Plugins.FactoryCredit` 测试通过：创建 proc → 状态改为 3 → 成功生成额度/台账；测试后已注销独立 Assembly<br>✅ 主 Assembly 验证通过（客户 SELVI ENTERPRISES，SAP编号 `0210000680`）：<br>• `mcs_fca_quota.mcs_custname` = `0210000680`（与客户主数据 `mcs_sapnumber` 一致）<br>• `mcs_fca_quota.mcs_fca_procid` 正确指向触发 proc<br>• `mcs_fca_records.mcs_custname` = `0210000680`<br>• 测试数据已清理 |
| 待完成 | 1. 确认 `mcs_fca_quota.mcs_quotano`（额度编码）是否需要自动编号规则（PRD 未明确）<br>2. 按需发布相关实体/解决方案到 UAT |

---

### 2.14 当前进行中的工作（信用评估内部历史交易指标 SalesAmount/ARAmount/ARAge）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 / 2026-07-02 |
| 目标 | 在 `CofaceDataSyncPlugin` 状态 11（数据集成）触发时，自动获取客户内部历史交易指标：`SalesAmount`、`ARAmount`、`ARAge` |
| 涉及实体 | `mcs_credit_record`（评估记录）<br>`salesorder`（销售订单）<br>`mcs_outstanding`（客户月度在外货款）<br>`mcs_customer_tag`（客户信用标签） |
| 涉及文件 | `Code/Customizations/Plugins/CofaceIntegration/Plugin/CofaceDataSyncPlugin.cs` |
| 实现要点 | 1. `SalesAmount`：汇总客户关联的 `salesorder.totalamount_base`（USD，排除 `statecode=2`）<br>2. `ARAmount`：取 `mcs_outstanding` 中 `mcs_overdue=true` 且 24 个月内记录的 `mcs_overdueamount` 最大值，通过 `CofaceExchangeRateHelper.GetRateToUsd` 由 CNY 转换为 USD<br>3. `ARAge`：取 `mcs_outstanding` 中 `mcs_overdue=true` 且 24 个月内记录的 `mcs_overdurationdays` 最大值<br>4. 内部数据与 Coface 外部数据合并后，统一写入 `mcs_customer_tag` |
| Git 分支 | `uat-20260701-peter-internal-historical-data` → `uat`（PR #4652 已合并，commit `3860b09c95`） |
| 远程编译 | ✅ 通过（仅项目原有警告，无新增错误） |
| DEV1 Assembly | ✅ 已用合并后的 `uat` 代码在 `tx-windows` 重新编译并更新 `SanyD365.D365Extension.Sales`（Assembly ID: `9d6ff315-8c03-4d51-b641-ebeccf9e98b0`，ModifiedOn: `2026-07-02 05:46:46`） |
| DEV1 Plugin Steps | ✅ 已整理为仅保留 1 个 Step：<br>• `CofaceIntegrationDataSyncPlugin`: Update of `mcs_credit_record`，Stage=PostOperation，Mode=Sync，Filter=`mcs_status`（Step ID: `fc5d8097-ae64-f111-ab0d-000d3aa3354f`）<br>• 已删除多余的 PreOperation Step（ID: `b13fa909-2a69-f111-ab0c-6045bd1c0925`） |
| DEV1 清理 | ✅ 已注销独立 Assembly `SanyD365.Plugins.CofaceIntegration`（Step/Type/Assembly） |
| 已发布实体 | ✅ 已发布 `mcs_credit_record` |
| 待验证 | 🔄 在 DEV1 新建信用评估记录，状态 11 触发后检查 `SalesAmount` / `ARAmount` / `ARAge` 三个标签值是否正确 |

---

### 2.15 当前进行中的工作（成交条件基线库按钮角色权限控制）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-20 |
| 目标 | 按 2026-06-24 业务角色矩阵，为 `mcs_trade_stpayterm`【批量申请】【批量审批】【批量拒绝】按钮加角色权限控制（前端点击拦截 + 后端 Plugin 兜底），顺带将【克隆新增】旧硬编码角色改为新角色 |
| 角色映射 | 【克隆新增】【批量申请】= `风控配置管理员`（制定人）；【批量审批】【批量拒绝】= `事业部军长`（审批人）；`System Administrator` 全部放行 |
| 状态 | ✅ 代码完成、本地编译通过、JS 已部署 DEV1 并发布、远程主项目已同步并编译通过；⏸️ Git 推送暂缓（用户决定累计其他改动后一起推）；⏸️ DEV1 Assembly 未更新；⏸️ 功能验证待角色创建 |
| 修改文件 | `Code/Customizations/WebResources/JS/mcs_trade_stpayterm.js`（新增 `ROLES` 常量 + `currentUserHasAnyRole` + `checkBatchPermission`，`hasCloneButtonPermission` 改用新角色）<br>`Code/Customizations/Plugins/TradeStPayTerm/Validation/TradeStPayTermValidationPlugin.cs`（`ValidateStatusTransition` 增加角色校验，新增 `UserHasAnyRole`，系统身份查 `systemuserroles`）<br>`Code/Tools/sync-plugin-to-remote.py`（同步规则固定化改造，见下）<br>`Documents/DevelopmentStandards/D365-本地测试项目到远程主项目同步指南.md`（新增第 11 章「本地 Plugin 写法规范」） |
| 同步脚本改造（2026-07-20） | 针对“每次同步都要改很久”的问题做了 3 项固定化改造，并用 40 个映射文件全量回归验证与远程一致：<br>1. **Plugin 自动识别**：文件含 `: IPlugin` 即自动做 PluginBase 转换，废除 if/elif 硬编码清单；类名重命名集中到 `PLUGIN_RENAME_MAP`（3 个类）；`PLUGINBASE_SKIP_FILES` 记录远程有意保持 IPlugin 的 4 个 BPP 插件（`FcaQuotaApp*`/`FsmData*`）；`FILE_MAP_API` 项目不转换<br>2. **fail-fast 断言**：转换后残留 `serviceProvider`/`factory.CreateOrganizationService`/`: IPlugin` 或入口正则不命中 → 本地立即报错指行号，不再等远程编译<br>3. **写法规范固定**：入口连续 4 行（context/factory/service/tracer）+ 系统服务固定写法 `factory.CreateOrganizationService(null)`，已写入同步指南第 11 章 |
| 关键结论 | 1. 列表命令栏按钮无法可靠按角色隐藏（无 form onLoad 钩子、Power Fx 读不到安全角色），采用“可见但点击拦截”<br>2. 角色按**名称**匹配（硬编码），环境角色改名需同步改代码<br>3. Plugin 角色查询用系统身份，避免普通用户无 `role` 实体读权限误判；仅查 `systemuserroles` 直接分配，不含团队继承角色<br>4. DEV1/UAT 均**不存在**「风控配置管理员」「事业部军长」角色，需用户手动创建并分配测试账号 |
| 经验教训 | `sync-plugin-to-remote.py` 的 PluginBase 转换正则要求入口 4 行初始化代码连续（context/factory/service/tracer），新增初始化行必须放在这 4 行之后，否则转换失败导致远程编译报错 |
| 下一步 | 1. 用户创建 DEV1/UAT 安全角色并分配测试账号<br>2. 用户决定时机后，在 `tx-windows` 推分支 `uat-20260720-peter-tradestpayterm-role-permission`（或与其他改动合并推送）→ 合并 `uat` → 编译更新 DEV1 Assembly<br>3. DEV1 四场景验证：无角色拦截 / 制定人可申请不可审批 / 审批人可审批不可申请 / 管理员放行（前端按钮 + 直接 WebAPI 改状态双重验证） |

---

### 2.16 发版核对（2026-07-22 发版，Solution 分布核对）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-20 |
| 状态 | ✅ 主清单 `AllComponent_Peter_NoUAT`（104 个组件）已全部正确分布到发版包，核对 104/104 全绿 |
| 发版包 | `entity_20260722`（34：20 实体+2 BPF 实体+2 BPF 流程+10 App Action，另有字段/关系/表单由用户 UI 添加）<br>`McsWebResource`（22）<br>`McsPlugin`（34：主 Assembly + 33 Step，含本轮补入的 CustomerMasterDataCreditValidationPlugin 2 Step、FcaProcCalculationPlugin PreOp Step）<br>`McsCustomAPI`（14：2 个 Custom API 含参数/响应 + ExtensionApi.Sales Assembly） |
| 新增工具命令（MetadataTool） | `check-solution-coverage <源Solution> [实体包Solution]`：以主清单为真相源反向核对组件分布（只读）<br>`list-solution-components <Solution>`：列出 Solution 全部组件（只读）<br>`list-workflows [关键字]`：列出工作流/BPF（只读）<br>`add-solution-component <type> <objectId> <Solution>`：通用加组件（写，需授权，幂等） |
| 核对映射（开发手册 4.4） | WebResource→McsWebResource；OptionSet→McsOptionSet；普通工作流→McsAutomate；Custom API 本体/参数/响应及其实现 Assembly/Step→McsCustomAPI；其余 Assembly+Step→McsPlugin；实体/BPF/表单/App Action→entity_XX（每次发版新建）；角色→role_XX（跳过）<br>• Custom API 实现判断：Step 的 Type 被 `customapi.plugintypeid` 引用；Assembly 按 Step 多数决<br>• **BPF 归 entity 包**（用户确认），按 `workflow.category=4` 识别 |
| 关键结论 | 1. 加实体选「包含所有组件」不会自动带入 App Action（独立组件，需单独加）<br>2. `SanyD365.D365ExtensionApi.Sales`（284 Type/282 Step）是 Custom API 实现 Assembly，归 `McsCustomAPI`，已在包内<br>3. componenttype 60 = System Form（主键 `formid`），此前误映射为 sitemap 已修正<br>4. `FcaProcCalculationPlugin` 有 PreOp + PostOp 两个 Step，代码内判断 Stage!=40 直接跳过，PreOp Step 无害，已一并入 `McsPlugin` 保持 DEV/UAT 一致 |
| 清单存档 | `Documents/Planning/Releases/release-20260722-add-*.json`（三份添加清单） |
| 组件类型穷尽审计（2026-07-20） | 已确认主清单无遗漏：业务规则/普通工作流/操作全环境排查无本人资产；选项集均为局部（随实体隐式分发），`McsOptionSet` 无内容；历史 5 个实体包未出现过 SiteMap（信用模块导航入口在公共 App 的 SiteMap，属共享资产）；现代化流 `AC0-CustomerCredit` 经用户确认非本人资产；角色（风控配置管理员/事业部军长未创建）与配置数据（按 `D365配置数据清单.md` 迁移）不在 Solution 核对范围 |
| 规则固化（2026-07-20） | 「新增组件必须及时加入主清单 Solution」已写入三处：`AGENTS.md` 第 4 节红线、`/skill:d365-dev` 第 8.3.1 节（详细规则+工具用法）、`/skill:d365-deploy` 第 5.1 节（发版核对前提）。要点：DEV1 新增任何组件后 DEV 验证通过第一时间加入主清单；本地临时独立 Assembly/测试组件/他人组件不得加入；环境删除组件须同步移除；AI 增删需用户授权 |
| 下一步 | DEV 验证 → 用户 n8n 发布 UAT（entity_20260722 / McsWebResource / McsPlugin / McsCustomAPI） |

---

### 2.15 中英多语言改造批次进展（2026-07-20/21）

| 项目 | 内容 |
|---|---|
| 本地语言文件唯一数据源 | `Code/Customizations/WebResources/Language/1033.json` / `2052.json`（当前 **352 keys**），只放我们自己的翻译。同步方向永远：本地→DEV；同步前导出 DEV 备份、逐条 diff、只追加缺失 key（规则已写入 2.11 检查清单第 15 条） |
| 已完成-元数据 | ✅ account 表单「客户画像」页签双语（Credit Profile，`entity_20260716_peter` translations）✅ fca_quotaapp【提交】按钮英文（Submit）✅ FSM 两个提交审批按钮英文（`entity_20260713` translations） |
| 已完成-附件语言 key | ✅ `mcs_credit_record_filetype_001~008,099`（9×2）✅ `mcs_fca_quotaapp_filetype_001~004,099`（5×2），均追加到公共语言包 `ms_languagefile_1033/2052` |
| 已完成-HTML 页面（3 个） | ✅ `mcs_credit_profile.html`（87 key，含 `.info-row-label` 列宽 110→160px）✅ `mcs_coface_company_search.html`（26 key）✅ `mcs_credit_wheel.html`（10 key + 复用 8 key）。机制：页面自包含 `L(key,中文)`，中文用户零请求、英文用户加载 1033 语言包 |
| 已完成-JS 表单脚本（12 个，全部） | ✅ `mcs_credit_record.js`（60 key，含 BPF 拦截/BPP/状态流转提示）✅ 批次 1：`mcs_credit_scoringcard/items/credititem_value/customer_tag/customermasterdata/fsm_data`（50 key）✅ 批次 2/3：`mcs_trade_stpayterm`（20 key，占位符句式）+ `mcs_fca_quotaapp/proc/mdlconfig/mdlversion`（26 key）。红线：角色名常量、aria-label、BPF 阶段名、落库值（非信用证/信用证、（克隆））未动；`mcs_account.js` 用户明确跳过（非我方主要负责） |
| 数据型翻译已停用（代码注释保留） | 因与 D365 表单/视图中文显示不一致，以下翻译已屏蔽（语言包 key 保留，取消注释即可恢复）：`Data_Item_*`/`Data_ItemDesc_*`（指标名/说明）、`CreditProfile_Group_*`（标签分类）、`CreditProfile_Category_/DealerRank_/DirectLevel_/NormalCustomer`（客户类别/级别）、`CreditProfile_Source_*`（内部/外部） |
| 语言 key 入仓（2026-07-27） | ✅ **已完成并关闭**。本地 355 个中英 key 已纯追加到远程仓库 `ms_languagefile_1033.json`（5610→5965）/ `ms_languagefile_2052.json`（5599→5954），0 冲突；分支 `uat-20260727-peter-langfile-credit`（commit `0ff86ad406b`）→ **PR 6148 已合并**（merge commit `e07d4d2c954`），合并后已验证 origin/uat 两个语言文件 JSON 可解析、355 key 全量在场且值一致。效果：团队用 CrmWebResourcesUpdater 上传语言包不再丢失我方 key；Solution 整包导入覆盖风险仍在（29 个 Solution 含语言文件）。**2026-07-27 三方一致性已验证**：DEV1 `ms_languagefile_1033/2052` = 仓库 uat 版 = UAT 版（逐字节一致，5965/5954 keys，我方 355 key 三方全在场），本次无需为语言文件单独发布 |
| 🚨 语言包三次被覆盖（2026-07-20/21） | 根因已定位：**`mcs_serviceorderspecialfees`（服务特殊费用）模块的翻译版本**（新增 24 个 `mcs_serviceorderspecialfees_filetype_*` key、改 2 个 key、删 1 个 key）用旧底版整包覆盖。DEV1 有 **29 个 Solution 包含 ms_languagefile_1033/2052**，任一导入都会整包覆盖；重点嫌疑：`Solution_Sany_India_20260722`、`McsWebResource`、`langjson_20260407_xzh`。处理原则：**非我们的 key 一律不动**（他们的 24 个 key 已保留）；恢复方式=本地 352 key 一键 diff 追加。待用户找对应人整改（基于最新版追加 + 从 Solution 移除语言文件） |
| 待办 | 1. E 类 Plugin 报错消息（约 99 条，29 个 .cs，需按执行用户语言取词，机制不同）2. F 类特殊项（落库中文值、角色名/BPF 阶段名匹配常量，需逐项决策）3. 成交条件重复旧版按钮（3 个旧 GUID appaction）暂不处理 4. FSM 银行产品 2052 中文缺失暂不处理 |
| 备份位置 | 语言包快照+追加版：`Backups/Tests/languagefile_backup_20260720/`；translations：`Backups/Tests/trans_export_20260720_*/` |

---

### 2.17 当前进行中的工作（厂端授信余额调整接口 mcs_AdjustFcaQuotaBalance）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-22 |
| 目标 | 按 PRD《LTC营销风控_授信额度动态运用和厂端授信》提供厂端授信余额调整统一内部接口（初始化/占用/释放 + 台账），模式复用成交条件样板库 Custom API |
| 状态 | ✅ 本地开发完成、编译通过、DEV1 独立 Assembly 全场景验证通过、测试数据已清理<br>✅ 分支 `uat-20260722-peter-fca-quota-api` 已推送（commit `15b019ad386`，含 07-20 暂缓的 tradestpayterm 角色权限批次），**PR 5904 已合并到 `uat`（merge commit `20a4653c99f`）**<br>✅ 已用合并后 `uat` 代码重编译并更新 DEV1 主 Assembly：`SanyD365.D365Extension.Sales`（ID `9d6ff315`）+ `SanyD365.D365ExtensionApi.Sales`（ID `3aa32db6`）<br>✅ Custom API 已重绑主 Assembly 类 `SanyD365.D365ExtensionApi.Sales.Apis.FactoryCredit.AdjustFcaQuotaBalancePlugin` 并回归验证通过（初始化/占用/重复拦截/释放）<br>✅ 临时 Assembly `SanyD365.Plugins.FactoryCredit.Api` 已注销（红线执行完毕）；回归测试数据已清理<br>⏸️ 待用户 n8n 发布 UAT |
| 方案文档 | `Documents/Planning/厂端授信余额调整接口_实施方案.md`（含 10 条已确认决策）<br>`Documents/BusinessAnalysis/厂端授信余额调整API调用说明.md`（接口文档） |
| Custom API | 唯一名 `mcs_AdjustFcaQuotaBalance`；入参 `mcs_accountid`(客户编码/sapnumber)/`mcs_usebalance`(Decimal)/`mcs_proccess`(环节1-11)/`mcs_adjust`(1初始化/3占用/4释放)/`mcs_contractid`/`mcs_orderid`；出参回显+`mcs_usedbalance`/`mcs_sellerbalance`/`mcs_usedflag`/`mcs_recordid`/`mcs_failreason` |
| 核心不变式 | **授信额度 = 授信余额 + 占用金额**；余额允许负数（超额不拦截）；占用按「订单号优先否则合同号」+客户防重，释放不防重；合同/订单按 `mcs_name` 反查，查不到返回"未找到"失败；接口内部补偿回滚防半提交 |
| 新增文件 | `Code/Customizations/Plugins/FactoryCredit.Api/`（AdjustFcaQuotaBalancePlugin + FcaQuotaAdjustService + csproj，临时 Assembly `SanyD365.Plugins.FactoryCredit.Api`） |
| 修改文件（旧逻辑口径修正） | `FactoryCredit/ProcActivation/FcaProcActivationPlugin.cs`（决策A：更新额度占用不清零 balance=grant-used；决策B：初始化台账 adjustamt=0）<br>`FactoryCredit/Bpp/Services/QuotaActivationService.cs`（回写 balance=tobeGrant-used，不信前端 tobeBalance；决策C：归零时 balance=-used 保留敞口）<br>`FactoryCredit/Bpp/Services/QuotaRecordService.cs`（台账 adjustamt=0，tobebalance=tobeGrant-实时占用）<br>`WebResources/JS/mcs_fca_quotaapp.js`（查询增读 usedsellerbalance，tobeBalance=tobeGrant-used，已发布 DEV1） |
| 元数据变更（DEV1 已执行） | ✅ 新建 `mcs_fca_quota.mcs_usedsellerbalance`（Money，占用金额）<br>✅ `mcs_fca_quota.mcs_sellerbalance` 最小值放宽为 -999999999999.99<br>✅ `mcs_fca_records.mcs_asisbalance`/`mcs_tobebalance` 最小值放宽为负数（PRD：允许负数表示超额）<br>✅ 实体 mcs_fca_quota/mcs_fca_records 已发布<br>Definitions JSON 已同步更新 |
| 工具扩展 | MetadataTool 新增 `deploy-fcaquota-api`/`delete-fcaquota-api`/`test-fcaquota-api`/`query-records`/`delete-record`/`update-field-range` 命令；`CustomApiDeployer.DeployFcaQuotaAdjustApi`（CreateOrUpdateCustomApi 已参数化）；D365ToolCommon `MetadataFieldService.UpdateMoneyRange`；sync-plugin-to-remote.py 增加 FactoryCredit.Api → `D365ExtensionApi.Sales.Apis.FactoryCredit` 映射 |
| DEV1 验证（8 场景全过） | ✅ 初始化新建（balance=grant,used=0,台账adjustamt=0）✅ 占用（100000→70000）✅ 重复占用拦截（PRD 原文提示）✅ 释放 ✅ 释放后再占用允许 ✅ 超额占用负余额+台账 ✅ 4 个异常路径（客户/合同/订单不存在、环节动作不匹配）✅ 初始化更新（占用 1019999 不清零，balance=200000-1019999=-819999） |
| 经验教训 | 1. **台账 mcs_tobebalance 字段范围也要放宽**（PRD 明确允许负数），否则额度已扣减但台账写入失败<br>2. Custom API 插件 catch 异常转失败响应时平台不会回滚，必须自己做补偿回滚（RevertQuota/DeleteQuotaQuietly）<br>3. 合同=`mcs_contract.mcs_name`、订单=`mcs_order.mcs_name`、客户=`mcs_customermasterdata.mcs_sapnumber`<br>4. **重绑 Custom API 到主 Assembly 前必须先删 Custom API 再注销临时 Assembly**：临时/主 Assembly 的同类名 PluginType 在 Default Solution 唯一索引冲突（2601）；且 Custom API 自动生成的 MainOperation Step 不能通过 SDK 删除，会阻塞 Assembly 注销 |
| 测试数据 | 已清理（删除测试额度记录 a6f18a5e 及台账 FCR2026072200001-0006） |
| 下一步 | 1. ✅ 远程同步+推送+PR 合并+主 Assembly 更新+重绑+临时 Assembly 注销均已完成<br>2. ✅ 主清单 `AllComponent_Peter_NoUAT` 已维护：Custom API 本体(10023)+6 参数(10024)+11 响应(10025)已加入（实现 Step 平台限制无法单独加，与 McsCustomAPI 发版包一致，导入时平台自动创建）<br>3. 用户 n8n 发布 UAT：`entity_20260722` + `McsCustomAPI` + `McsPlugin` + `McsWebResource`<br>4. 额度表表单/视图加占用字段 + 翻译总表补 1033/2052<br>5. 上线前历史数据初始化（存量占用核算，实施方案 §5.6） |

---

### 2.18 已完成（成交条件基线库事业部带出 + 审批共享 — 禅道 #1151）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-24 |
| 需求 | 1. 表单根据所选大区/子公司带出事业部；2. Bug #1151：审批人（事业部海外营销公司风控债权部部长）仅能看到本部门数据，提交后看不到记录无法审批 |
| 状态 | ✅ 已全部部署 DEV1 并回归通过（3/3）；⏸️ 待用户 DEV1 界面验证 + 环境配套（角色创建/部长加入 BuTeam）+ n8n 发布 UAT |
| 大区带出事业部 | `mcs_trade_stpayterm.js`：`onLookupChanged` 查子级时顺带取 `_mcs_buid_value`，新增 `autoFillParentLookup` 自动带出事业部并同步编码/名称；接通死代码 `validateChildLookups`（事业部变更→清空不匹配大区）；修复 `validateLookupParent` 用 SDK 属性名读 WebAPI 返回值的 Bug（应为 `_mcs_buid_value`）；表单设计器「大区按事业部筛选」保留（业务要求），双向联动闭环 |
| 审批共享（#1151） | 新增 `TradeStPayTermSharePlugin`（Update PostOperation，Filter=mcs_status，PreImage=mcs_status+mcs_businessunit）：状态变为待审批时按 `mcs_businessunit` 查 `mcs_bu.mcs_buteamid`（主数据已有，零配置），GrantAccess(Read+Write) 给 BuTeam，幂等；无事业部拦截提交；角色名 JS/Plugin 同步改为英文名（与 UAT 实际创建角色一致）：制定人 `LTC Risk Control Configuration Admin`、审批人 `LTC Regional Overseas Risk Director`（PR #6059） |
| 工具 | `D365ToolCommon.Data.RecordSharingService`（ShareToBuTeam/HasShare/批量共享）；MetadataTool 新增 `test-tradestpayterm-share`/`share-tradestpayterm-pending` 命令；存量待审批 DEV1 为 0 条 |
| Git | 分支 `uat-20260724-peter-tradestpayterm-share-1151`，PR #6044 已合并（merge commit `4bfaa210b0d`）。推送前发现远程落后 origin/uat 41 个提交，先拉平再同步重编译 |
| DEV1 | 主 Assembly 已更新（ID `9d6ff315`）；SharePlugin Step（`5b627677`）+ PreImage 已注册；独立 Assembly 已注销；主清单已补 Step（92→34 个） |
| 经验教训 | 1. 主 Assembly 更新后平台未自动扫出新 PluginType，用 `register-plugin-advanced` 显式创建 Type+Step 解决；2. 注册主 Assembly 同名类型前必须先注销独立 Assembly（PluginType 同名冲突 2601）；3. Step Image 是 Step 子组件随 Step 导出，不能单独加 Solution；4. scp 到 tx-windows 偶发 exit 255（连接节流），整脚本重试即可恢复 |
| 下一步 | 1. 用户创建 DEV1/UAT 角色（审批人/制定人，mcs_trade_stpayterm User 级 Read/Write）并把部长加入对应 XXX-BuTeam；2. 用户 DEV1 界面验证（含部长账号可见性）；3. 用户 n8n 发布 UAT（McsPlugin+McsWebResource）；4. UAT 存量待审批记录补共享（share-tradestpayterm-pending，D365_URL 切 UAT） |

---

### 2.19 已完成（批量申请/审批/拒绝后列表自动刷新 — 禅道 #1160）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-25 |
| 需求 | Bug #1160：未生效视图全选记录点【批量申请】后需手动刷新才能看到状态变“待审批”，期望自动更新 |
| 根因 | 现代命令栏（Command Designer）JS 运行在**隔离沙箱**，`batchUpdateStatus` 成功后的 `window.location.reload()` 不生效（DEV1 实测复现：数据已更新但列表不刷新、选中状态保留）。初步“旧版无 reload”判断被实测推翻 |
| 修复 | 1. JS：`mcs_trade_stpayterm.js` 的 apply/approve/reject 增加 `selectedControl` 形参，新增 `refreshGrid()`（`selectedControl.refresh()` 优先、reload 兑底）<br>2. DEV1 三个现代按钮（`cr0c0__mcs_trade_stpayterm_apply/approve/reject!cra80_SanyOverseasCRM!...`）`onclickeventjavascriptparameters` 由 `[{"type":23}]` 改为 `[{"type":23},{"type":12}]`（**type 枚举：5=PrimaryControl、12=SelectedControl、23=SelectedControlSelectedItemIds**，12 由系统 Refresh 按钮参数反推锁定） |
| 工具 | DeployTool 新增 `update-appaction-params <uniquename前缀> <参数JSON>` 幂等命令（`AppActionDeployer.UpdateButtonParameters`）；MetadataTool 新增 `setup-bu-team <mcs_buID> <用户domain>` 幂等命令（#1151 环境配套：建/挂 BuTeam + 加成员，关联名 `teammembership_association`） |
| 状态 | ✅ DEV1 部署+用户验证通过，已关闭；⏸️ UAT 发布中（JS 已到 UAT 并逐字节核对一致） |
| 发布方案（最终，2026-07-25 实测后定稿） | **JS 走 `McsWebResource` + 按钮参数各环境在 Command Designer 手动加 SelectedControl（发版手册手动步骤）**。方案 B（现代按钮收归 Solution）实测失败：现代按钮依赖 App 级 `DefaultCommandLibrary`（多团队共享、每环境独立副本），加按钮入包会被平台自动拖入组件库，与 UAT 同名库冲突导致导入失败——**命令组件库绝对不可跨环境打包**；移除按钮后恢复小包发布。navigateTo 替代方案同样实测失败（沙箱中稳定抛 `Unexpected xrm page input`） |
| DEV1 验证 | 批量拒绝：待审批视图 2→0 行自动刷新；批量申请：未生效视图 13→11 行自动刷新（grid 局部刷新）；测试数据 TC26072403/07 已恢复未生效；顺带实锤 #1151 SharePlugin 无 BuTeam 拦截正常 |
| 环境配套（DEV1） | 已创建 `CONCRETE MACHINERY BU/泵路海外营销公司-BuTeam`（10beac9a，参照既有 BuTeam 模式：Owner/管理员 AdminTest1 T/挂同名业务部门）、维护 `mcs_bu.mcs_buteamid`、邱正卫已加入团队。**UAT 配套时同样需执行**（`setup-bu-team`，属配置数据非 Solution 组件） |
| 关键经验 | 现代命令栏沙箱中 `window.location.reload()` 无效，列表刷新必须传 `SelectedControl` 参数用 `grid.refresh()`；teammembership 的 Associate 关系名为 `teammembership_association`；**UAT 的命令组件库与现代按钮是 UAT 本地独立副本（同名不同 ID/componentidunique），不能随 entity 包/组件库包导入（会冲突或产生重复按钮），参数变更只能由用户在 UAT Command Designer 手动同步；UAT 自定义项变更一律走 Solution 发布，禁止工具直连修改** |

---

### 2.20 已完成（成交条件查询 API 产品线编码支持逗号分隔多值）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-28 |
| 需求 | `mcs_QueryTradeStPayTerm` 入参 `mcs_prdgroupid` 支持逗号分隔传多个产品线编码（如 `2,4`） |
| 改动 | `TradeStPayTermQueryService.GetTradeTypeIds()`：入参拆分/去空格/去重，`mcs_groupid` 查询 `Equal` 改 `In`，多分类取并集后与记录交集匹配；Plugin 校验不变，单值完全兼容 |
| 背景排查 | 同事照抄文档示例参数（SUB-001/CN/PM）调接口返回空：早期 API 测试数据已清理（Memory 2.7 有记录）+ 映射表无 PM + 客户 C 级不在记录 buyerGrade(A/S) 内；接口本身正常。文档示例已更新为 DEV1 实测可调通组合 `BU-1018/A000025/001/4/ACN202605280000`（md + Excel） |
| 验证 | DEV1 临时独立 Assembly 验证：`4`命中/`2`空/`2,4`命中/`2, 4`命中；验证后绑回主 Assembly 类并注销临时 Assembly（红线执行完毕） |
| 远程合并 | 分支 `uat-20260728-peter-tradestpayterm-multi-prdgroup` → **PR 6246 已合并 `uat`**（merge `ee919f16c02`，仅 `D365ExtensionApi.Sales/Apis/TradeStPayTerm/TradeStPayTermQueryService.cs` +13/-1）；已用合并后 uat 重编并更新 DEV1 主 Assembly `SanyD365.D365ExtensionApi.Sales`（3aa32db6），主 Assembly 多值回归通过 |
| 工具 | MetadataTool：`deploy-tradestpayterm-api` 增加可选 [Plugin类名] 参数（默认值修正为实际绑定的 ExtensionApi 类，原硬编码 `D365Extension.Sales` 副本为 `_Legacy`）；新增 `bind-customapi <唯一名> <类名>` 命令（复用 `CustomApiDeployer.BindPluginType`，只改绑定不改 Assembly） |
| 注意 | 远程存在两份查询服务代码：当前绑定 `SanyD365.D365ExtensionApi.Sales`（本次已改）；`SanyD365.D365Extension.Sales` 副本 plugintype 名 `QueryTradeStPayTermPlugin_Legacy` 已废弃未动。plugintype 短名 `QueryTradeStPayTermPlugin` 唯一索引冲突 → 临时 Assembly 验证时本地类名临时改为 `QueryTradeStPayTermMultiPlugin`，验证后已改回 |
| 状态 | ✅ DEV1 已生效；⏸️ 待用户 n8n 发布 UAT（**只需勾 `McsCustomAPI`**：实现 Assembly `SanyD365.D365ExtensionApi.Sales` 在该包内，不在 McsPlugin）；无新增 D365 组件，不涉及主清单变更 |

---

### 2.21 已完成（成交条件查询 API records 输出改裸记录数组）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-30 |
| 需求 | 调用方反馈 `records` 输出多包了一层（整个 `{"status","message","records":[...]}` 包装对象），要求改为裸记录数组 |
| 根因 | `QueryTradeStPayTermPlugin` 成功路径 `SerializeResult(result)` 序列化了整个 QueryResult；失败路径 `SetErrorResult` 本来就是 `"[]"`，两路径不一致 |
| 改动 | `TradeStPayTermQueryService` 新增 `SerializeRecords(List<TradeStPayTermRecord>)`；Plugin 成功路径改 `SerializeRecords(result.Records)` |
| 验证 | DEV1 临时 Assembly 三路径验证：成功有匹配→裸数组/成功无匹配→`[]`/失败→`status=0`+`message`+`[]`；清理后绑回主 Assembly、注销临时 Assembly（红线执行完毕） |
| 远程合并 | 分支 `fix-20260728-peter-tradestpayterm-records-array` → **PR 6377 已合并 `uat`**（merge `f8a83cf51f0`，2 文件 +16/-3）；已重编并更新 DEV1 主 Assembly（3aa32db6），回归通过 |
| 注意 | ⚠️ 首次更新 DEV1 时撞库（他人导入 Solution 超时），但 Assembly 内容已更新成功、API 绑定未受影响；补跑 deploy 完成参数/解决方案归属刷新。接口契约变更存在切换窗口：发布前 records 为包装结构（需 `.records` 多取一层），发布后为裸数组，需同步通知调用方 |
| 状态 | ✅ DEV1 已生效；⏸️ 待用户 n8n 发布 UAT（**只需勾 `McsCustomAPI`**）后验证；文档（md+Excel）已改为裸数组契约；无新增 D365 组件 |

---

### 2.22 已修复待验证（融资资源启用/停用 + 删除守卫 — 禅道 #1433）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-30 |
| 需求 | Bug #1433：融资资源列表点系统【激活/停用】按钮后「是否启用」列不更新；顺带落地 PRD 删除规则（未启用过+创建人才可删，角色不写死靠安全角色删除权限配置） |
| 根因 | 系统按钮只改 `statecode/statuscode`，`mcs_fsm_resource` 无任何 Plugin/JS 回写自定义字段；且 DEV1 `mcs_fsm_rl_status` 标签误建为「是否启用/Is Active」（本地定义与 PRD 均为「是否启用过/Ever Enabled」） |
| 方案（用户确认） | ① DEV1 标签纠错「是否启用过/Ever Enabled」（已生效，实体已发布）；② 新增 `FsmResourceStateSyncPlugin`（Update Filter=statecode PostOp，激活幂等回写 rl_status=true，单向）；③ 新增 `FsmResourceDeleteGuardPlugin`（Delete PreOp + PreImage，已启用过/非创建人拦截，SysAdmin 放行）；④ PRD「新增默认停用」不落 statecode（避免保存后只读），由「是否启用过=否」承担草稿态语义；删除按物理删除 |
| ⚠️ 重要发现 | **DEV1 Delete 管道 `context.UserId` 恒为 SYSTEM（110ee7ff），真实操作人必须取 `InitiatingUserId`**（Trace 实锤；Update 管道两者一致）。创建人比对、角色校验一律用 InitiatingUserId |
| 改动文件 | `Code/Customizations/Plugins/FinancingManagement/Resource/FsmResourceStateSyncPlugin.cs`（新增）<br>`Code/Customizations/Plugins/FinancingManagement/Resource/FsmResourceDeleteGuardPlugin.cs`（新增）<br>`Code/Tools/sync-plugin-to-remote.py`（Resource 映射 + 新增 `--only` 参数绕历史文件校验失败 + build_remote `cd /d`→`cd` 兼容 PowerShell） |
| Git | 分支 `uat-20260730-peter-fsm-resource-1433`（commit `2e87ac1e9d1`，3 文件 +221）→ PR 已合并 uat（用户操作，2026-07-30） |
| DEV1 | 主 Assembly `SanyD365.D365Extension.Sales`（ID `9d6ff315`）已更新；StateSync Step（`3b449718`；首个 `2cfa6d37` 注册后立即加 Solution 报 does not exist，删除重注册换新 ID）+ DeleteGuard Step（`f0ff8ab9`）+ PreImage 已注册启用（平台未自动扫新 Type，register-plugin-advanced 显式创建）；临时 Assembly 已注销（红线执行完毕） |
| 验证 | 临时 Assembly 7/7 通过 + 主 Assembly 回归通过（激活回写/幂等/删除拦截/创建人可删，Trace 实锤主 Assembly 触发）；重建 Step `3b449718` 二次回归通过（停用→激活 rl_status=是、删除拦截实锤）；测试数据已清理，样本 FSMR-TEST-1433D（已启用过不可删）留存 |
| 新增组件提醒 | 2 个 Step 已加主清单 `AllComponent_Peter_NoUAT`（2026-07-30，componenttype=92）；⚠️ 教训：PluginType(90) 不能显式 add-solution-component（报 does not exist），随 Step/Assembly 隐式入包；刚注册的 Step 立即加 Solution 可能同样报错，删除 Step 重注册可解。发版包 McsPlugin 的 Assembly+Step 待用户分布 |
| 环境配套 | 「融资资源管理员」角色配置 `mcs_fsm_resource` Delete 权限（各环境手动，已登记《D365配置数据清单》+《上线核对清单》3.3.7） |
| 下一步 | 1. 用户 DEV1 界面验证（停用→激活后「是否启用过」变「是」、删除拦截提示）<br>2. 用户 n8n 发布 UAT（McsPlugin + entity 包带标签）<br>3. 可选增强（暂不做）：列表自定义【启用】按钮一键启用 |

---

### 2.23 进行中（厂端授信额度调整需求变更：字段精简 + 默认值公式 + 生效改走BPP）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-30 |
| 需求 | ① 额度生效申请表单隐藏 4 字段（产品类型/意向合同金额/支付方式/账期，用户已界面处理，字段保留非必填）；新增多行文本调整原因 `mcs_reason`（必填），旧 `mcs_remark` 隐藏降非必填；② 「调整厂端授信额度」改名「厂端授信额度调整为」，选客户时默认值=厂端授信额度、调整后余额默认=厂端授信余额，公式=调整为-额度+余额；③ 模型计算生效启用（proc 状态 3）不再直写 `mcs_fca_quota`，自动创建 `mcs_fca_quotaapp`（状态=申请，人工提交 BPP），审批通过后复用现有回调链路写额度 |
| 改动文件 | `mcs_fca_quotaapp.js`（默认值带出+新公式+删账期30倍数校验+文案改名，删 currentUsedBalance）<br>`FcaProcActivationPlugin.cs`（状态3→自动创建申请单，含在途申请单防重、组织字段带出、`mcs_reason` 必填默认值）<br>`BPPHandlerServiceForFcaQuotaApp.cs`（BPP 表单变量取数 mcs_remark→mcs_reason，变量 Code 不变模板无需改）<br>`Definitions/mcs_fca_quotaapp.json`（补 mcs_reason、remark 降非必填、tobegrant 改名）<br>本地 `Language/2052.json`（3 个提示语 key 值改名） |
| Git 分支 | `uat-20260730-peter-fca-proc-quotaapp`（3 commits：d4bfe43 plugin + 9fd1c95 bpp handler + 1eacec9 mcs_reason 补必填）**PR 6446 已合并 uat**（merge `0208702f26e`）<br>`uat-20260730-peter-langfile-fcaquotaapp`（a14e44c，仓库语言文件 2052 三 key 值变更）**PR 6450 已合并 uat**（merge `408d79d6b22`），仓库 uat 版与 DEV1 WebResource 已一致 |
| DEV1 已执行 | ✅ `mcs_fca_quotaapp.js` 更新+发布<br>✅ `ms_languagefile_2052` 仅 3 key 值更新+发布（备份 `Backups/Tests/languagefile_backup_20260730/`）<br>✅ 字段标签：`mcs_tobegrant`→「厂端授信额度调整为/Adjusted Factory Credit Quota」、`mcs_reason` 补 2052/英文，实体已发布<br>✅ 远程两项目编译均通过（仅原有警告） |
| ⚠️ 关键坑 | 1. `mcs_reason` 在 DEV1 是 **ApplicationRequired**，Plugin 自动建单必须填默认值否则创建失败；2. 远程 tx-windows 工作区常驻 8 个「M」文件是 LF/CRLF 换行符假象（`git diff --ignore-cr-at-eol` 为空），commit 只 add 指定文件；3. Windows ssh 会话无 `head` 命令、PowerShell 内联中文+`$变量` 会被本地 bash 展开，改远程文件一律「scp 下载→本地改→scp 回传（保持 CRLF）」 |
| 新增组件 | `mcs_fca_quotaapp.mcs_reason` 字段（用户已建）。已核实：主清单 `AllComponent_Peter_NoUAT` 无任何字段级组件（按实体整体管理，quotaapp 实体已在其中且 `rootcomponentbehavior=0` 包含所有组件），**新建字段随实体导出自动带上，主清单和 entity 发版包均无需单独加字段**；发版前跑 `check-release --with-fields` 核对即可 |
| 用户偏好（新） | **DEV 直接发布不再逐项询问**（2026-07-30 用户明确）；语言 key 持久化走远程仓库语言文件分支+PR（勿只改 DEV WebResource） |
| DEV1 Assembly | ✅ 已用合并后 uat 代码重编译并更新 `SanyD365.D365Extension.Sales`（ID `9d6ff315`），无 PluginType 差异 |
| DEV1 后台验证（7/30 全过） | ✅ LTC客户-1 calc→activate（proc `FCM202607300002` 状态 2→3）：自动创建申请单 `FCA202607300001`（带出当前额度/余额、tobegrant=initGrant、bppstatus=1、mcs_reason 默认值、`mcs_doid` 正确指向 proc）；✅ 额度表未触碰（仍旧 doid）；✅ 无新台账；✅ 防重：再次 2→3 未重复创建。失败残留 proc 已删，`FCA202607300001`+proc 留存供界面验证 |
| UAT 发布与验证（7/31） | 用户 n8n 已发 UAT。首轮核对发现 entity 包漏带 quotaapp 元数据（`mcs_reason` 不存在/标签未更新/remark 仍必填），用户补发后全齐。✅ Assembly/JS/语言包均一致；✅ 功能验证通过：TEST ACCOUNT 建 proc（initGrant=120000）→ 状态 3 → 自动建单 `FCA202608010001`（无额度客户按 0 带出、tobegrant=120000、bppstatus=1、reason 默认值、doid 正确）、额度表未写（0 条）、无台账；测试数据已清理。⚠️ 教训：新增字段后 entity 发版包必须重新导出（首版包漏了 quotaapp 元数据） |
| 下一步 | 1. 用户 UAT 界面验证：表单默认值/公式/隐藏字段/多行调整原因、人工提交 BPP → 审批通过写额度（端到端最后一环）<br>2. 全部验证通过后本需求变更关闭 |

---

### 2.24 进行中（Coface 系统内下单 — 本地开发完成，待 DEV1 部署与沙盒联调）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-08-01 |
| 需求 | Coface 下单从线下人工改为 D365 系统内完成：「关联客户代码」阶段【Coface 下单】按钮手动触发，点击推进式状态机（调查单→URBA监控单→Report单），未就绪前端硬阻断进入数据集成（PRD 口径） |
| 方案/计划 | 实施方案 V2.0：`Documents/Planning/Coface系统内下单/Coface系统内下单实施方案.md`；开发实施计划：`~/.kimi/plans/valkyrie-gorgon-kid-flash.md`（含默认决策：Plugin 归 Extension.Sales + 前端硬阻断） |
| 状态 | ✅ 本地开发完成、编译全部通过；✅ **2026-08-01 DEV1 部署与非沙盒测试全部完成**（3字段+双语标签+表单、CofaceCountryConfig 新结构、JS+发布、【Coface 下单】按钮、Custom API 部署测试后已按红线注销临时 Assembly）；⏸️ Coface 沙盒联调待下周一用户协调后进行（联调前一条命令重建：`deploy-cofaceorder-api <本地DLL> SanyD365.Plugins.CofaceIntegration.Plugin.CofacePlaceOrderPlugin`） |
| DEV1 测试记录（2026-08-01 全过） | ① 后端前置校验：状态≠10 拦截“仅在关联客户代码阶段可用”✅；无 Coface ID 拦截 ✅<br>② 状态机推进（测试记录 SCO202608010001，绑定 icon#5415240/PL 已有订单，**全程只读 GET 零新单零费用**）：第1次调用复用已有 URBA 单→状态2 ✅；第2次 URBA ready→复用已有 Report 单→状态3 ✅；第3次 Report ready→状态4 已就绪 ✅；第4次幂等提示 ✅；下单三字段回写正确 ✅<br>③ 前端 UI（Playwright 实测）：表单 3 字段正常显示；【Coface 下单】按钮（溢出菜单）点击弹窗提示正确 ✅；【下一步】硬阻断：状态0→自动推进一次查询→阻断提示“URBA已下单待就绪”✅；再点推进到“Report已下单待就绪”阻断 ✅；第三次推进到已就绪并自动放行进入数据集成 ✅，**CofaceDataSyncPlugin 重构后回归正常**（13 指标 58 标签+Report附件已保存）✅<br>④ 测试数据已清理（15 标签+1 附件+1 评估记录）；McsCustomAPI 无组件残留 |
| ⚠️ 环境偏差 | `ms_systemconfiguration` **不存在** `coface_idtoken`/`coface_token_expiry` 字段（方案假设有误）：Token 缓存化代码按设计降级为每次重新认证（行为与旧版一致，功能不受影响）；是否在共享实体上加这两个字段待用户决策（或改存我方实体/不缓存） |
| 语言 key | 本地 1033/2052.json 已追加 15 key；**已按红线 16 走仓库分支推送**：`uat-20260801-peter-langfile-cofaceorder`（commit `ed637f19b60`，5965/5954 基础上纯追加 15×2，远程验证 JSON 可解析）；DEV1 提示语当前走 JS 中文兜底（显示正常） |
| 选项集英文标签 | `mcs_cofaceorderstatus` 全部 6 个选项双语已齐（2026-08-01 晚）：选项 1-5 早前已设；选项 0 因公共方法 `MetadataFieldService.UpdateOptionSetLabels` 的 `value==0` 跳过 bug 被漏，**用户批准后已修复该 bug（D365ToolCommon，删除 value==0 跳过）并补上 `Not Ordered`**，实体已发布，2052 中文验证无丢失。翻译总表已同步登记 |
| 收尾开发（2026-08-01 晚） | ✅ `AppActionDeployer` 中【Coface 下单】CreateButton 代码已删（防 buttons 命令重建已删 App Action），DeployTool 重编通过；✅ 《上线核对清单》4.1 CofaceCountryConfig 新结构迁移要求 + 9.13/9.14 Open Items；✅ `Code/INDEX.md` Ribbon 条目 + CustomApiDeployer 方法清单；✅ 实施方案文档按钮方案同步改为 Ribbon |
| 按钮改 App Action（2026-08-01 用户最终决策） | 用户最终定稿：【Coface 下单】无特殊显隐控制，**用 App Action 不用 Ribbon**（AGENTS.md 红线已修订为：需显隐随包/跨环境零手动用 Ribbon，简单按钮经用户确认可用 App Action）。✅ App Action `mcs_credit_record_place_coface_order` 已重建（fonticon=ShoppingCart，seq=100100019）；✅ Ribbon 残留已清除。⚠️ 教训：① 非托管导入**永不删除** Ribbon diff（从包中移除节点再导入无效）；② 同 Id 覆盖 + 恒否 DisplayRule 可隐藏残留按钮——**`CrmClientTypeRule Type="Legacy"` 在 UCI 也匹配（无效），恒否规则用 `<ValueRule Field="statecode" Value="999999" InvertResult="false" Default="false"/>`**；③ 清除后已把 entity_20260727_peter 里的 Ribbon 节点剥净重新导入（UAT 发版包干净），DEV1 隐藏覆盖层留在非托管层不可见无害；④ 命令栏缓存于 Cache Storage+Service Worker，验证必须先清缓存 |
| ⚠️ uat 主干阻塞（他人问题，用户知悉不管） | 2026-08-01 拉平 uat 后发现**他人提交把加密二进制 .cs 文件带进了 uat**（文件头 `%TSD-Header-###%`，涉 `Const.cs`/`Startup.cs`/`ShipmentDeclare*.cs`/`LtcController.cs` 等，来自 wangtianjiu/mamf3 等的 QD-WTJ 等分支，PR 6591 等批次）：`git show` 直接读仓库对象确认仓库存的就是密文，**全团队 uat 当前编译不过（CS2015）**。已取证非我方造成（我方 PR 仅 7 个 Coface 文本文件 + 2 个语言 JSON；加密前基线 `052e8fc7986` 是明文）。**后果：DEV1 主 Assembly 更新暂停**（红线要求用合并后 uat 编译 DLL），待 uat 修复后补做；周一沙盒联调如需 Custom API，先用本地临时 Assembly（deploy-cofaceorder-api 传本地 DLL+类名），测完即注销 |
| 推送（2026-08-01 用户授权） | ✅ 分支 `uat-20260801-peter-coface-placeorder`（commit `a16b9352119`，7 文件 +1452/-315：4 修改+2 新增+csproj）已推送；✅ 语言分支 `uat-20260801-peter-langfile-cofaceorder` 已推送；均基于最新 uat（推送前 stash→ff 拉平 56 提交→pop）；待用户合并 PR<br>⚠️ 同步踩坑：远程 Plugin 命名空间父链不同，新 Plugin 文件必须显式 `using SanyD365.Plugins.CofaceIntegration;`（本地靠父命名空间隐式解析，远程会编译失败 CS0246），已修 |
| 主清单 | 已核实 `mcs_credit_record` 实体整体 + `mcs_credit_record.js` 均在主清单：3 字段随实体自动带、JS 已覆盖；✅ **Custom API 本体(31ec7f7c)+参数 CreditRecordId(53facb82)+响应 ResultJson(62facb82) 已加入主清单 `AllComponent_Peter_NoUAT`**（2026-08-01，绑主 Assembly 后重建的新 ID，旧 ID 平台级联清除）；按钮为 Ribbon 随实体包无独立组件 |
| ✅ 主 Assembly 归并完成（2026-08-01 晚） | uat 加密文件已由提交人修复（PR 6595 等）→ 代码 PR 6596 合并 → 重编 → DEV1 主 Assembly `SanyD365.D365Extension.Sales`（ID `9d6ff315`）已更新含 `CofacePlaceOrderPlugin` → 平台未自动扫出新 Type，用 `deploy-cofaceorder-api` 显式创建主 Assembly Type（28ec7f7c）并重绑 Custom API → **临时 Assembly `SanyD365.Plugins.CofaceIntegration` 已注销（红线执行完毕）** → 主 Assembly 冒烟回归 2→3→4 通过，测试数据已清理。⚠️ 教训：两个分支只被合并了一个时（语言 PR 6590 先于代码 PR 6596），用 uat 编译的 DLL 不含新代码，推送分支后必须确认每个分支的 PR 都已合并再编译 |
| ✅ 真实下单联测（2026-08-01 晚，沙盒） | 用户指示用 `icon#5239780`（PL，无 Report 单）DEV1 实测：① URBA 复用正常；② **Report 下单文档参数已过时**——`report` 字符串形式被接口拒绝（400 Invalid JSON structure），curl 逐参数探明正确结构 `{"slug":"customized-report","customReportId":301,"format":["json"],"language":"en"}`（report 对象、format 数组、language 必填、customReportId 数字）；③ JSON 单 `ff0c5bcc`、PDF 单 `0fb440d2` 真实下单成功（in-preparation）；④ 修复代码（PlaceReportOrder 签名改 slug/productCode/format/language、删 GetReportOrderValue）→ PR 6599 合并 → DEV1 Assembly 更新 → 续测：URBA 复用→Report 复用（防重不重复下单）→状态 3→提示 6-7 工作日，全链路通；⑤ 测试记录 `bc1e42c2-a78d-f111-8077-6045bd1c0e3b`（SCO202608010001）**保留**，待 Report Ready 后测最后一步（3→4→数据集成取新报告+PDF 附件） |
| 核心设计 | 状态机字段 `mcs_cofaceorderstatus`（0未下单/1调查单已提交/2URBA已下单待就绪/3Report已下单待就绪/4已就绪/5下单失败）+ `mcs_cofaceordermsg` + `mcs_cofaceorderdate`；防重复扣费：每分支先查已有订单再下单；下单失败置 5 不静默成功；双格式国家 JSON/PDF 两单间隔 5 秒 |
| 新增/修改文件 | **新增** `CofaceIntegration/Plugin/CofacePlaceOrderPlugin.cs`（Custom API `mcs_CofacePlaceOrder` 实现，入参 CreditRecordId，出参 ResultJson）<br>**新增** `CofaceIntegration/CofaceOrderInfoHelper.cs`（URBA/Report 订单就绪判定公共逻辑，从 DataSyncPlugin 抽取，纯重构行为不变）<br>`Api/CofaceApiService.cs`（ExecutePost/RetryPost + 5 个下单方法：即时报告/调查单/查调查单/URBA监控单/Report单）<br>`Token/CofaceTokenManager.cs`（Token 缓存化：读 `coface_idtoken`/`coface_token_expiry`，systemService 回写，失败降级）<br>`CofaceCountryConfig.cs`（DualFormatCountries 36 国 + LegitimateInterestByCountry DE=100 + GetReportOrderValue）<br>`CofaceDataSyncPlugin.cs`（改调 Helper + 入口 4 行初始化修正）<br>`mcs_credit_record.js`（placeCofaceOrder 按钮命令 + nextStep 10→11 就绪硬阻断，先自动推进一次查询）<br>`CustomApiDeployer.cs`（DeployCofacePlaceOrderApi）/`MetadataTool Program.cs`（deploy/delete/test-cofaceorder-api 三命令 + add-fields mcs_credit_record 三字段块）<br>`AppActionDeployer.cs`（【Coface 下单】按钮 seq=100100019）<br>`sync-plugin-to-remote.py`（FILE_MAP +2）<br>`DeployTool/CofaceConfigDeployer.cs`（配置数据类加 DualFormatCountries/LegitimateInterestByCountry）<br>本地 `Language/1033.json`/`2052.json` 各纯追加 15 个 CreditRecord_CofaceOrder* key（共 375 keys） |
| 待确认（阻塞细节） | C1~C5（给 Coface）/ B1~B7（给业务）下周一协调；report 下单参数枚举值（GetReportOrderValue 映射）为沙盒联调第 1 优先级；双格式国家列表文档标题 39 国/要点 35 国/表格实际 36 国，代码按表格 36 国实现待澄清 |
| 新增组件清单（待加入主清单/发版包，下周一 DEV1 验证后执行） | ① 3 个新字段（随 mcs_credit_record 实体，`add-fields mcs_credit_record` 创建）② Custom API `mcs_CofacePlaceOrder` 本体+1 参数+1 响应（→McsCustomAPI）③ Plugin Step（Custom API MainOperation，平台自动创建）④ App Action【Coface 下单】（→entity 包）⑤ `mcs_credit_record.js` 变更（→McsWebResource）⑥ 语言 key 15×2（走仓库语言文件分支+PR 红线流程） |
| 下一步（下周一） | 1. 用户协调 Coface 沙盒 + 回填 C1~C5/B1~B7<br>2. DEV1：`add-fields mcs_credit_record` 建 3 字段+发布 → DeployTool 更新 CofaceCountryConfig JSON → 注册临时 Assembly `SanyD365.Plugins.CofaceIntegration` + deploy-cofaceorder-api → 部署 App Action → 更新 mcs_credit_record.js → 语言 key 同步<br>3. 按实施方案第 11 章 10 场景沙盒联调<br>4. 验证通过：注销临时 Assembly（红线）→ sync 归并远程 → 用户授权后推送/PR → 主 Assembly 更新+重绑 → 组件加主清单 → 发版核对 |

---

## 3. 核心资产清单

### 3.1 实体（7 个自定义实体）

| 实体逻辑名 | 显示名 | 核心字段 | 功能 |
|-----------|--------|---------|------|
| `mcs_credit_items` | 客户信用评分项目表 | `mcs_itemname`, `mcs_datatype`(定量/定性), `mcs_itemweight` | 定义评分维度 |
| `mcs_credititem_value` | 定性评分项目枚举值 | `mcs_credititemno`, `mcs_listname`, `mcs_listvalue` | 定性选项值 |
| `mcs_credit_scoringcard` | 客户评分卡配置表 | `mcs_cardname`, `mcs_categoryid`(SA/BC/个人/经销商), `mcs_typeid`(实力/财务/宏观), `mcs_credititem`(Lookup) | 评分规则配置 |
| `mcs_credit_record` | 客户信用评估记录表 | `mcs_scoreid`(SCO编码), `mcs_accountid`(Lookup→Account), `mcs_status`(9-16), `mcs_creditscore` | 评估流程主表 |
| `mcs_customer_tag` | 客户信用标签表 | `mcs_credit_record`(Lookup), `mcs_credit_item`(Lookup), `mcs_scorevalue`, `mcs_reviewvalue` | 逐项评分结果 |
| `mcs_customer_file` | 客户资信附件表 | `mcs_fileid`, `mcs_accountid`(Lookup) | 附件归档 |
| `mcs_custcredit` | 客户信用评估记录表(旧) | — | 保留兼容 |

### 3.2 Plugin（9 个 C# 类，分 6 个模块）

| Plugin | 触发实体 | 事件 | 功能 |
|--------|---------|------|------|
| `ScoringCardAutoNumberPlugin` | `mcs_credit_scoringcard` | Create/PreOp | 编码生成 `SCYYYYMMDD####` |
| `CreditScoringCardValidationPlugin` | `mcs_credit_scoringcard` | Create/Update/PreOp | 同一评分项目下定性值不重复、定量区间不重叠 |
| `CreditRecordAutoNumberPlugin` | `mcs_credit_record` | Create/PreOp | 编码生成 `SCOYYYYMMDD####` |
| `CreditScorePlugin` | `mcs_credit_record` | Update/PostOp | 信用分计算（遍历标签×权重） |
| `CofaceDataSyncPlugin` | `mcs_credit_record` | Update/PostOp | 调用 Coface API 获取企业数据 |
| `BppIntegrationPlugin` | `mcs_credit_record` | Update/PostOp | BPP 审批提交（调用mcs_bppstartapi） |
| `AccountValidationPlugin` | `account` | Create/Update/PreOp | Account 字段校验 |
| `CreditItemsValidationPlugin` | `mcs_credit_items` | Create/Update/PreOp | 评分项目校验 |
| `CustomerTagInitPlugin` | `mcs_customer_tag` | Create/PostOp | 标签记录初始化 |

### 3.3 JS WebResource（6 个表单脚本）

| 脚本 | 绑定实体 | 核心功能 |
|------|---------|---------|
| `mcs_credit_scoringcard.js` | 评分卡配置 | 编码只读、自动带出、显隐控制、下拉联动、数值校验 |
| `mcs_credit_record.js` | 评估记录 | 编码只读、客户信息带出、状态锁定、保存校验、【搜索 Coface 企业】弹窗命令 |
| `mcs_account.js` | Account | Coface ID 校验、信用状态提示 |
| `mcs_coface_company_search.html` | 弹窗 | Coface 企业搜索与 Coface ID 绑定（fetch + Web API） |
| `mcs_credit_items.js` | 评分项目 | 数据类型变更提示、必填校验 |
| `mcs_credititem_value.js` | 枚举值 | 项目类型校验、编码唯一性 |
| `mcs_customer_tag.js` | 标签 | 复核字段显隐、评估状态锁定 |

### 3.4 状态流转（8 个阶段）

```
9:发起信用评估 → 10:关联客户代码 → 11:数据集成 → 12:人工复核
    → 13:信用分计算 → 14:审核申请 → 15:审批通过 / 16:审批未通过
```

### 3.5 客户信用画像（Phase 7，新增）

| 组件 | 类型 | 说明 |
|------|------|------|
| `mcs_credit_profile.html` | HTML WebResource | 信用画像主页面，嵌入 Account 表单页签 |
| `mcs_credit_wheel.html` | HTML WebResource | 客户飞轮图形页面（Vue+eCharts 旭日图） |
| `mcs_credit_profile.js` | JS WebResource | 画像页面逻辑 |
| 内部 API | Custom API | 信用标签评分查询，供飞轮和画像使用 |
| 双表同步 Plugin | C# Plugin | account ↔ mcs_customermasterdata 字段同步 |

状态值存储在 `mcs_credit_record.mcs_status`（选项集，值 9-16），由前端按钮控制流转。

---

## 4. 四大核心文件（AI 协作入口）

任何 AI 接手本项目，**先看这 4 个文件**：

| 文件 | 路径 | 用途 | 更新时机 |
|------|------|------|----------|
| **开发计划** | `Documents/Planning/开发计划.md` | 功能清单 + 进度跟踪 + 功能-测试-Bug 关联总表（57 个功能映射约 169 个用例） | 每完成一个功能 |
| **测试用例总集** | `Documents/Tests/测试用例总集.md` | 169 条测试用例，TC-{MODULE}-{NNN} 编号，含功能编号列；页面表单逻辑作为功能用例步骤中的验证点 | 每新增/修改功能 |
| **Bug 反馈记录** | `Documents/Tests/BugReports/测试Bug反馈记录.md` | 8 个 Bug，含功能编号、修复记录、根因分析 | 每发现/修复 Bug |
| **工作日报汇总** | `Documents/DailyReports/工作日报汇总.md` | 5 天工作记录，按日期章节，表格化核心数据 | 每个工作日结束 |

### 4.1 功能编号体系（F1.x ~ F6.x）

```
F1.x — Phase 1: 客户主数据扩展（实体/字段/视图/表单）
F2.x — Phase 2: 评分项目管理（评分项目/评分卡配置/克隆导入导出）
F3.x — Phase 3: 评估记录管理（CRUD/状态机/Coface关联/数据集成/人工复核/状态条UI）
F4.x — Phase 4: 信用分计算（评分卡匹配/逐项评分/逾期处理/等级映射/结果保存）
F5.x — Phase 5: 审批与集成（审批记录/虚拟审批/状态同步/BPP对接/Coface对接）
F6.x — Phase 6: 系统管理（批量处理/权限/日志/监控）
```

### 4.2 测试用例编号体系

```
TC-BASE   — 基础实体与字段测试（第二部分 2.1）
TC-ACC    — 客户主数据联动与辅助验证（第二部分 2.3）
TC-ITEM   — 评分项目表逻辑（第二部分 2.1）
TC-ENUM   — 枚举值表逻辑（第二部分 2.1）
TC-SCCFG  — 评分卡配置数据测试（第二部分 2.2）
TC-CARD   — 评分卡配置表逻辑（第二部分 2.2）
TC-EVCF   — 评估记录数据测试（第二部分 2.3）
TC-EVAL   — 评估记录表逻辑（第二部分 2.3）
TC-TAG    — 标签表逻辑（第二部分 2.4）
TC-FLOW   — 完整业务流程
TC-CFPI   — Coface Plugin 测试
TC-CSPI   — 信用分计算 Plugin 测试
TC-EXCP   — 异常流程
TC-BULK   — 批量处理
```

---

## 5. 当前进度（2026-06-15）

### 当前焦点与阻塞

**当前焦点**：
- **`mcs_credit_items.mcs_group` 选项集标签修正**：移除"评分项目分类"选项前的数字前缀（`1 客户实力` → `客户实力` 等），保持与 `mcs_customer_tag` / `mcs_credit_scoringcard.mcs_typeid` 显示风格一致。
  - ✅ 已修改本地 `Entity.xml`
  - ✅ 已在 DEV1 更新选项标签并发布 `mcs_credit_items`
  - ✅ 已在 UAT 更新选项标签并发布 `mcs_credit_items`
  - ✅ 已在 MetadataTool 封装 `rename-options` 命令
- **资信附件上传功能（mcs_customer_file）**：基于三一通用上传组件 `mcs_/CommonCore/Html/Uploader.html` 实现评估记录表单附件上传。附件归属 `mcs_credit_record`，Uploader 以 `entityName=mcs_credit_record` + `entityId=当前评估记录ID` 传参。
  - ✅ 已创建 `CustomerFileAutoNumberPlugin`（`SanyD365.Plugins.CustomerFile`），生成 `ATT+YYYYMMDD+4位序列号`，默认填充 `mcs_filedate`
  - ✅ 已修改 `mcs_credit_record` 主窗体，新增【Attachments】页签并嵌入 Uploader.html；移除错误的静态 `Data` 参数
  - ✅ 已更新 `mcs_credit_record.js`，新增 `initAttachmentTab` 方法，动态向 Uploader 传入 `mcs_credit_record` 上下文和 `fullFileTypes` 过滤
  - ✅ 已编写 `Documents/Planning/资信附件上传配置说明.md`
  - ✅ 已在 DEV1 注册 `SanyD365.Plugins.CustomerFile` Assembly + `CustomerFileAutoNumberPlugin` Step（Create PreOperation of mcs_customer_file）
  - ✅ 已在 MetadataTool 封装 `add-uploader-tab` 命令，用于为任意实体主窗体添加 Uploader HTML WebResource Tab
  - ✅ 已在 MetadataTool 新增 `update-webresource` 命令
  - ✅ 已追加 DEV1 `UploadFileTypeMapping` 的 `mcs_credit_record` 配置（9 个附件类型 001~008, 099，未覆盖其他 145 个实体配置）
  - ✅ 已为 `mcs_customer_file` 实体新增 `mcs_credit_recordid` Lookup 字段
  - 🔄 待 DEV1 验证：发布后测试 `mcs_credit_record` 表单附件页签是否正常工作
  - ⏸️ 待配置多语言文件 `ms_languagefile_xxxx.json`（key 为 `mcs_credit_record_filetype_xxx`）
  - ⏸️ 待 UAT 发布时同步配置：`UploadFileTypeMapping` + 多语言文件，并做完整上传测试
  - ✅ 已更新并发布 `mcs_credit_record` 实体
  - ✅ 已重新发布 `mcs_credit_record.js` WebResource
- **Coface 4 个缺口 DEV1 注册与功能验证**：按开发流程，先在 DEV1 使用独立临时 Assembly `SanyD365.Plugins.CofaceIntegration` 测试，通过后再集成到远程主项目。
  - ✅ `CofaceDataSyncPlugin` 已注册到 `mcs_credit_record` Update PostOperation
  - ✅ `CofaceSearchCompanyPlugin` 已注册到 Custom Action `mcs_CofaceSearchCompany`
  - ✅ Custom Action `mcs_CofaceSearchCompany` 已在 D365 UI 创建并激活（Unique Name: `CofaceSearchCompany`）
  - ✅ HTML WebResource `mcs_coface_company_search.html` 已创建并发布
  - ✅ JS WebResource `mcs_credit_record.js` 已更新并发布
  - ✅ Modern Command Bar 按钮【搜索 Coface 企业】已部署
  - ✅ `ms_systemconfiguration` 配置 `CofaceCountryConfig` 已创建（39国 + CEE RU）
  - ✅ `mcs_credit_items` 中 10 个内部指标已标记 `mcs_source=100000000`（ProjectAmt/ProductNum/DebtAmount/TotalAssets/OverdueModel/BigAccount/SalesAmount/ARAmount/ARAge/DealerRating）
  - ✅ 【搜索 Coface 企业】弹窗 DEV1 功能验证通过：DE+Sany 返回 20 条候选企业，绑定后 `mcs_cofaceid` 已正确写入 `mcs_credit_record` 和 `account`
  - ✅ 39 国 / CEE Report 产品选择逻辑已按截图修正：非受限用 `customized-report/301`，受限非 RU 用 `full-report`，RU 用 `customized-report/21000`
- **工具项目重构 — 创建 D365ToolCommon 共享库**：整理 MetadataTool 与 DeployTool 的重复功能。
  - ✅ 已创建 `Code/Tools/D365ToolCommon/` 共享库
  - ✅ 已抽取通用服务
  - ✅ `MetadataTool` 和 `DeployTool` 已引用共享库并编译通过
  - ✅ 修复 `PluginRegistrationService.DeployPlugin` 中 `RegisterOrUpdateAssembly(dllPath, assemblyName)` 误把路径当 base64 传参的 bug
  - 🔄 隔离测试：`MetadataTool test-common` 命令已添加；DEV 当前发布操作较多，测试实体暂时保留
- ⚠️ **2026-06-13 备份提醒**：同事曹阳通知 `uat` 可能回滚到 `Merged PR 2982: fix隐藏字段`（2026/6/12 23:44），已备份 PR 2983（Coface API 配置化）和本地 F6.8 汇率表配置化代码到 `Backups/Solutions/2026-06-13-Coface-Config-Backup/`，并在远程仓库创建备份分支 `backup/coface-api-config-20260613`。

**本会话已完成**：
- ✅ Coface 4 个缺口本地开发完成并编译通过（0 警告 0 错误）
- ✅ DEV1 注册独立 Assembly `SanyD365.Plugins.CofaceIntegration` 及两个 Plugin Step
- ✅ D365 UI 创建并激活 Custom Action `mcs_CofaceSearchCompany`
- ✅ 部署 HTML WebResource 与 Modern Command Bar 按钮
- ✅ 创建 `ms_systemconfiguration.CofaceCountryConfig` 并修正 `mcs_credit_items.mcs_source`
- ✅ 修复 `CofaceCountryConfig` 模型与 `CofaceDataSyncPlugin` 调用逻辑：按国家类型选择 Report 产品
- ✅ 修复 `CofaceSearchCompanyPlugin` JSON 解析：支持 address 对象、giid 二次搜索获取 icon id
- ✅ 修复 `D365ToolCommon.PluginRegistrationService.DeployPlugin` 中 DLL 路径被误当 base64 的 bug
- ✅ CofaceSearchCompany Custom Action DEV 调用验证通过，返回 icon#xxx 格式企业列表
- ✅ CofaceDataSyncPlugin DEV 触发验证通过（PL 记录状态 11 → SUCCESS）
- ✅ PR 已合并到 `uat`，远程服务器拉取最新 `uat` 并重新编译通过
- ✅ DEV Assembly `SanyD365.D365Extension.Sales` 已更新，`modifiedon` = `2026-06-18 00:28:19`
- ✅ DEV/UAT `ms_systemconfiguration.CofaceCountryConfig` 已按新 JSON 结构更新
- ✅ PL 测试记录 `SCO202606160004` 重新触发状态 11，CofaceDataSync 返回 SUCCESS
- ✅ 评分卡配置界面增加金额类定量指标提示：`mcs_credit_scoringcard.js` 已修改并更新到 DEV WebResource
- ✅ 更新 `d365-deploy` skill 与 `D365配置数据清单.md`
- ✅ Coface 财务指标对照表按客户 `4国家财务指标-更新版0612.xlsx` 全量刷新：87 国 429 条记录，DEV1 已导入
- ✅ 编写 `Documents/Planning/Coface财务指标对照逻辑说明.md`，说明 Excel 解析规则、代码读取逻辑、导入命令、测试方法
- ✅ DEV1 触发 PL 国家测试记录验证标签生成：`CofaceDataSyncPlugin` 成功加载 PL 配置并生成 7 个 `mcs_customer_tag`，其中 `净资产` 解析为 `11361465.84`
- ✅ UAT 环境已同步最新 Coface 财务指标配置（87 国 429 条记录）
- ✅ **2026-07-10**：Coface 财务指标对照表按客户最终版 `SANY-financial indicators-combined-updated20160709按国家取财务指标Final version.xlsx` 全量刷新为 **107 国 510 条记录**，DEV1/UAT 已导入
- ✅ **2026-07-10**：DEV1 用 PL 测试记录 `SCO202607020005` 重新验证，生成 15 个客户信用标签，`净资产=11361465.84`，`资产负债率=0.13`，`流动比率=0.08`，`净利润率=0.04`
- ✅ **2026-07-10**：UAT 用 CN 测试记录 `SCO202607070008` 验证，`CofaceDataSync` 成功执行并正确读取新配置

**关键发现**：
- Coface `/companies` 按名称搜索返回的 `externalIds` 中 repositorySlug 为 `giid`，不是 `icon`；需用 `externalId=giid#<giidId>` 二次搜索才能拿到 `icon` id
- 按开发流程，Coface 新功能先在 DEV1 用独立 Assembly 测试，通过后再集成到远程主项目；不是因主 Assembly 更新阻塞才用独立 Assembly
- `mcs_bppstatus=Submitted` 是 `BppIntegrationPlugin` 设置，不代表 BPP 平台已创建流程实例

**阻塞点**：
- ⏳ 39 国 / CEE Report 产品选择逻辑无法通过 Coface 测试环境实际调用验证（测试数据只有 PL 有 Report 订单）
- ⏳ UAT 尚未通过 n8n 发布新 DLL
- ⏳ PR `!3530`（`uat-20260618-peter-registrationdate-years` → `uat`）因 `.gitignore` 冲突无法合并（`Added in both`）

**下一步**：
1. ✅ 已推送到远程分支 `uat-260618-peter-coface-report-product`，等待用户合并 PR
2. ✅ PR 已合并，远程 `uat` 已重新编译，DEV Assembly 已更新
3. ✅ DEV/UAT `ms_systemconfiguration.CofaceCountryConfig` 已按新 JSON 结构更新
4. ✅ DEV 验证通过（PL 测试记录 SUCCESS）
5. ⏳ 用户通过 n8n Release Tool 发布 `McsPlugin` 到 UAT
6. ⏳ 解决 PR `!3530` 的 `.gitignore` 冲突并完成合并
7. 继续推进其他 Coface 配置化项（汇率表、NACE 映射表、字段映射表、定性值映射表）

---

### 工具项目共享库规划

**目标**：将 `MetadataTool` 与 `DeployTool` 中的重复能力下沉到 `Code/Tools/D365ToolCommon/`，形成可复用的共享类库，后续 AI 开发新工具时应优先调用。

**MetadataTool 目录整理（2026-06-13）**：
- 删除无用文件：`EntityManager.cs.bak`、`Program.cs.bak3`、`Program_delete_step.cs`、`DeletePluginStep.cs`、`delete_step.sh`、`Deploy.ps1`
- 文件归类：
  - `Models/EntityDefinition.cs`
  - `Services/EntityManager.cs`、`QueryPluginSteps.cs`、`PublishProfileWebResources.cs`、`TestCommonService.cs`
  - `Helpers/LabelHelper.cs`
  - `Scripts/register-bpp-plugins.ps1`、`register-credit-plugins.ps1`
- 更新 `README.md` 反映新结构、共享库引用、`test-common` 命令
- 编译通过
- 修复 `CofaceApiTest` 项目加载失败：该项目有两个 `Main` 入口点（`Program.cs` 和 `TestCountryCode.cs`），已将 `TestCountryCode.Main` 改为 `Run`，并通过 `dotnet run countrycode` 调用

**分阶段抽取优先级**：
1. **阶段 1（最高优先级）**：连接认证、Plugin 注册/查询/注销、WebResource 部署、字段检查/创建
2. **阶段 2（中等优先级）**：表单 XML 操作、视图操作
3. **阶段 3（业务层）**：BPP 诊断、CreditRecord 查询/修复、PluginTrace 查询

**核心共享类**：
- `D365ConnectionFactory`（认证/连接）
- `PluginRegistrationService` / `PluginQueryService`
- `WebResourceService`
- `MetadataFieldService`
- `FormXmlService` / `ViewService`
- `CreditRecordService` / `BppDiagnosticsService` / `PluginTraceQueryService`

**AI 约束（已写入 AGENTS.md）**：
> 在 `Code/Tools/` 下新增功能时，优先使用 `D365ToolCommon` 中的通用方法。如果通用方法不满足需求，先扩展通用方法，而不是在工具项目里临时写重复代码。

**发布功能固化（2026-06-23）**：
- 通用发布入口：`D365ToolCommon.WebResource.WebResourceService.PublishWebResources`（WebResource）、`D365ToolCommon.Publishing.PublishingService.PublishEntities`（实体）
- 实现要点：WebResource 发布先查 `webresourceid` 再用 GUID 构造 `PublishXmlRequest`，统一 ParameterXml 格式，内置 3 次重试
- 已统一：`MetadataTool` / `DeployTool` 中所有直接构造 `PublishXmlRequest` / `PublishAllXmlRequest` 的代码已改为调用通用服务
- 规范文档：`/skill:d365-tools` 第 3 章「发布规范（强制）」、`AGENTS.md` 发布红线

---

| Phase | 状态 | 关键完成项 |
|-------|------|-----------|
| Phase 1 客户主数据 | ✅ 完成 | 7 实体 + 字段 + 窗体 + 视图 + SiteMap |
| Phase 2 评分项目管理 | ✅ 完成 | 评分项目/枚举值/评分卡 CRUD + 编码生成 |
| Phase 3 评估记录管理 | 🔄 进行中 | 评估记录 CRUD + 自定义进度条 + 状态同步 + 附件页签 |
| Phase 4 信用分计算 | ✅ 完成 | 评分卡匹配 + 逐项计算 + 结果保存 + value2优先value1回退 |
| Phase 5 审批与集成 | ✅ 完成 | BPP 审批对接问题已解决；Coface接口代码已完成，**DEV 主 Assembly 更新因 Type 差异暂停，UAT 尚未发布** |
| Phase 6 系统管理 | ⏸️ 待开发 | 批量处理 / 权限 / 日志 |
| **Phase 7 客户信用画像** | 🔄 进行中 | 画像页面/飞轮页面HTML+JS已开发，WebResource已创建并加入Solution，已手动发布到dev环境 |
| **Phase 7 客户信用画像** | ⏸️ 待开发 | 画像页签 / 画像页面 / 客户飞轮 / 内部API / 权限控制 |

**待办：**
- ✅ **BPP domainaccount配置**：已通过创建mcs_personnel记录解决
- ✅ **BPP审批对接代码重构**：删除BPPHandlerServiceForCreditRecord，新增BppCallbackPlugin监听mcs_bppstatus变更
- ✅ **UAT Plugin Step 验证**：10 个 Plugin Types / 12 个 Steps 全部确认已同步到 UAT
- ✅ **BPP 审批对接问题已解决**：`GetBppFormData` 从 `mcs_credit_record` 查询了不存在的 `mcs_creditgrade`，改为从 `account` 查询并回写，UAT 审批流程已正常
- 🔄 **UAT Coface 数据集成**：配置化代码已完成，DEV 测试通过；**DEV 主 Assembly 更新因 Type 差异暂停，UAT 尚未发布**
- ⏸️ **测试环境Plugin注册**：已完成（通过 Solution 导入同步）
- 基础-06 测试：JS 表单逻辑验证
- 主流程-01~09 测试（TC-FLOW-008/009 依赖BPP配置完成）
- BPP审批结果回调处理验证
- 批量处理功能开发（import/sync-all/report）
- 信用等级映射（A/B/C/D/E）
- 客户主数据信用信息更新
- **Coface 硬编码数据配置化改造**：
  - ✅ **财务指标国家编码对照表**：已完成（`mcs_coface_financial_indicator` 实体 + `Urba360Parser` 重构）；2026-07-10 已按客户最终版 Excel 全量刷新为 **107 国 510 条记录**，DEV1/UAT 已导入并验证
  - 🔄 **Coface API 配置表**：代码已配置化（从 `ms_systemconfiguration` 读取 `CofaceApiConfig`），DEV 测试通过；**DEV 主 Assembly 更新因 Type 差异暂停，UAT 尚未发布**
  - ✅ **货币汇率表**：已完成。DEV 已创建并发布 `mcs_coface_exchange_rate` 实体，已导入 17 条 2026 Budget 汇率；代码已改为读取配置表；**本地 mock 测试已通过（EUR/CNY/PLN → USD），临时 Assembly 已注销；待 DEV 主 Assembly 更新后做最终集成验证**
  - ✅ **NACE 行业映射表**：本地代码已改造完成，本地 mock 测试通过；DEV 实体 `mcs_coface_nace_mapping` 已创建并导入 10 条映射数据；DEV 临时 Assembly 已注销；评分卡配置中尚未包含 `Sectors` 项目，未生成 Sectors 标签；待评分卡补充 Sectors 后做最终集成验证
  - ⏸️ **Coface 字段 → D365 评分项目映射表**：`CofaceDataSyncPlugin.CofaceToD365Mapping` 硬编码映射，用户决定暂缓，待后续需要时再做配置化
  - ✅ **定性指标值映射表**：已改造完成，复用 `mcs_credititem_value`；新增 `CofaceQualitativeMappingHelper`，删除 `MapValueToDisplayName` 硬编码 switch；DEV 数据已修复对齐 Coface 原始值；DEV 临时 Assembly 测试通过；临时 Assembly 已注销
  - ✅ **D365 配置数据清单**：已创建 `Documents/Planning/D365配置数据清单.md`，汇总 `ms_systemconfiguration`、自定义实体配置数据、业务基础数据、代码/部署配置、上线部署检查清单，避免 DEV/UAT/生产迁移遗漏
- **Coface 业务需求缺口（2026-06-13 文档对照梳理）**：
  - ✅ **诉讼债权标的金额解析**：Coface JSON 不提供结构化金额字段，仅返回诉讼记录数量；真实金额通过人工复核阶段线下读取 PDF 后，录入 `mcs_customer_tag.mcs_itemintvalue2`（复核定量指标），评分计算优先读取复核值（来源：Coface业务分析文档.md、评分卡响应参数.md）
  - ✅ **Coface 无法提供的 6 个指标内部录入标记**：代码已完成。`CofaceDataSyncPlugin` 读取评分项目 `mcs_credit_items.mcs_source`（100000000=内部，100000001=外部），内部数据源指标跳过 Coface 取数，创建空标签等人工复核补录。待上线前在 D365 中给 6 个指标（在手项目合同、自有设备数量、还款来源、资产证明、行业地位、财务报表真实性）设置 `mcs_source` = 内部（来源：海外客户评分卡项目和科法斯接口字段取数反馈表-不能提供数据.md）
  - ✅ **国别/行业风险等级映射**：已实现。Coface 原始值（A1-E / 1-4）通过 `mcs_credititem_value` 配置映射为中文等级（低风险/中风险/高风险），由 `CofaceQualitativeMappingHelper.GetDisplayName` 在写入标签时自动转换，`mcs_itemvalue1` 保留原始值用于算分，`mcs_itemtxtvalue1` 显示中文等级（来源：海外客户评分卡项目和科法斯接口字段取数反馈表-枚举值映射.md）
  - ✅ **39 国 / CEE Report 产品选择逻辑**：代码已完成并推送到分支 `uat-260618-peter-coface-report-product`。`CofaceDataSyncPlugin` 读取 `CofaceCountryConfig`，按国家类型选择 Report 产品：
    - 非受限国家：`customized-report` + `customReportId=301`（Full Report URBA）
    - 受限国家（39 国，除 RU）：`full-report`（Full Report）
    - RU：`customized-report` + `customReportId=21000`（Full report CEE）
    - URBA360 监控接口仍对所有国家调用
    - 待上线前在 `ms_systemconfiguration` 中创建 `CofaceCountryConfig` 并导入 39 国列表（来源：截图确认）
  - ✅ **企业搜索弹窗 / Coface ID 人工匹配**：DEV1 已部署并验证通过。入口放在 `mcs_credit_record` 表单（状态 9/10 且 `mcs_cofaceid` 为空时显示按钮）；后端新增 Custom Action `mcs_CofaceSearchCompany` + Plugin `CofaceSearchCompanyPlugin` 调用 `CofaceApiService.SearchCompany`；前端 WebResource `mcs_coface_company_search.html` 使用原生 `fetch` 调用 Web API（避免 HTML WebResource 中 `Xrm` 对象不可用问题）；`mcs_credit_record.js` 增加 `searchCofaceCompany`；`AppActionDeployer.cs` 增加 Modern Command Bar 按钮定义。UAT 部署时：注册 Custom Action + Plugin Step、发布 WebResource、运行 `dotnet run appactions` 部署按钮（来源：Coface业务分析文档.md）
- **待客户确认事项（2026-06-09记录）**：
  1. ✅ BPP审批 domainaccount配置（P0阻塞）已解决
  2. 评分卡算分规则：定性指标listvalue为空时默认给分 vs 精确匹配
  3. 504家大客户Coface ID批量导入时间表
  4. 中国客户国别风险内部评级表（风控部）
  5. ✅ Account实体字段（黑名单/不予授信）+ 信用画像页签：已确认暂缓开发
  6. 行业属性正式权重（当前测试=0）
  7. 外部评级评分卡配置：单值精确匹配 vs 范围匹配
  8. 信用等级映射具体分数区间
  9. ✅ 逾期未回收率来源：已确认人工录入，无 SAP 对接需求
  10. 生产环境Coface下单流程
  11. 受限国家/俄罗斯CEE特殊处理规则
- **Phase 7 客户信用画像模块开发**：
  - ⏸️ F7.1 account/mcs_customermasterdata 新增黑名单/不予授信字段 + 双表同步 Plugin（暂缓：客户主数据为一期已上线内容，且画像 PRD 仅提到字段名、无详细业务逻辑，经确认暂不开发）
  - F7.2 Account 表单新增"信用画像"页签 + WebResource 嵌入
  - F7.3 画像页面开发（基本信息 / 标签评分 / 采购要求 / 重点尽调）
  - F7.4 客户飞轮图形化（Vue+eCharts 旭日图 WebResource）
  - F7.5 信用标签评分查询内部 API
  - F7.6 画像权限控制（角色数据范围）
  - F7.7 画像应用集成（线索/配置报价/合同独立调用）

### 代码同步跟踪（2026-06-11 新增）

> 完整清单见：`Code/Customizations/SYNC_TRACKING.md`
> 远程服务器：`tx-windows` (122.51.232.70)，`C:\Projects\D365\D365\SanyD365.D365Extension.Sales`

**✅ 已同步（2026-06-11 修复）**：

| 本机文件 | 远程对应文件 | 修改时间 | 修改内容 |
|---------|-------------|------------|---------|
| `CofaceIntegration/Plugin/CofaceDataSyncPlugin.cs` | `Plugins/CofaceIntegration/CofaceIntegrationDataSyncPlugin.cs` | 2026-06-11 | `record["mcs_itemid"]` → `GetAttributeValue<string>("mcs_itemid")` + fallback
| `CofaceIntegration/Api/CofaceApiService.cs` | `Application/Sales/CofaceIntegration/CofaceApiService.cs` | 2026-06-11 | 去掉 `format=json`
| `CofaceIntegration/Parser/Urba360Parser.cs` | `Application/Sales/CofaceIntegration/Urba360Parser.cs` | 2026-06-11 | 添加 `FillUrbaMissingValues`

|---------|-------------|------------|---------|
| `CofaceIntegration/Parser/Urba360Parser.cs` | `Application/Sales/CofaceIntegration/Urba360Parser.cs` | 2026-06-11 12:31 | #13 productStatus 判断 + #14 debtorRiskValue 取 isCurrent=true |
| `CofaceIntegration/Parser/FullReportParser.cs` | `Application/Sales/CofaceIntegration/FullReportParser.cs` | 2026-06-11 12:32 | #29 三种 share capital 取 max |

**✅ 已同步（12 个文件）**：CofaceApiService、CofaceTokenManager、CofaceDataSyncPlugin、BppCallbackPlugin、BppIntegrationPlugin、CreditRecordAutoNumberPlugin、CreditScore BpfStageSync/BpfSyncHelper、CreditItemsValidation、CreditItemValueValidation、CustomerTagInit/Validation。

**⚠️ 结构差异需确认**：
- 本机 `ScoreCalculator.cs` + `CreditScorePlugin.cs` → 远程可能合并为 `CreditScoreCalculationPlugin.cs`
- 本机 `AccountValidationPlugin.cs` → 远程 `AccountCreditValidationPlugin.cs`（文件名不同）

**同步流程**：本机开发 → 单元测试验证功能 → DEV 注册 → **DEV 测试确认（卡点，必须测试人通过）** → 改命名空间 → 复制到远程 → 远程编译(D365.sln, .NET 4.6.2) → Git提交(uat) → PR → Release Tool(n8n) → 部署

---

## 6. 外部系统集成

| 系统 | 集成方式 | 状态 | 文档位置 |
|------|---------|------|----------|
| **Coface** | REST API (搜索/URBA360/Report) | 🔄 进行中 | 代码已完成，**UAT 报错：版本偏差导致旧代码吞异常**（根因已定位，待专人同步最新 DLL） |
| **BPP** | D365自定义API (mcs_bppstartapi等) | ✅ 已完成 | UAT Plugin Step 已全部验证（10 Types / 12 Steps） |
| **SAP** | 逾期未回收率数据 | ⏸️ 待确认 | — |

### BPP审批对接详情（2026-06-08）

**实现方式（已重构）：**
- Plugin: `BppIntegrationPlugin` | 触发: `mcs_credit_record` Update PostOp, 状态=14
- 调用: `mcs_bppstartapi` (EntityId + EntityName + UserId)
- 回调: `BppCallbackPlugin` | 触发: `mcs_credit_record` Update PostOp, mcs_bppstatus变更
- 事务: 同步执行，失败回滚，状态保持13

**完整交互流程：**

```
阶段1: 触发审批（D365 → BPP）
  用户点击【下一步】状态13→14
    → BppIntegrationPlugin触发
    → 调用mcs_bppstartapi(EntityId, EntityName, UserId)
    → SanyD365.D365ExtensionApi处理
      → 查用户domainaccount映射（已通过mcs_personnel解决）
      → 发送SMessage到BPPStartWorkflow队列
      → 异步HTTP调BPP平台

阶段2: BPP平台审批（BPP内部）
  BPP接收请求 → 创建审批实例 → 审批人登录后台审批
  状态: Submitted → InReview → Approved/Rejected

阶段3: 结果回传（BPP → D365）
  BPP平台回调D365 → BPP框架更新mcs_bppapply → 更新mcs_credit_record.mcs_bppstatus
  → BppCallbackPlugin触发 → 根据状态更新mcs_status

阶段4: 通过后处理（D365内部）
  mcs_bppstatus=Approved → mcs_status=15 → 更新Account: mcs_creditscore/mcs_creditgrade/mcs_creditvalid
```

**关键变更（2026-06-10）：**
- 删除: `BPPHandlerServiceForCreditRecord.cs`（当前BPP框架不存在IBPPHandlerService/BPPHandlerServiceMain）
- 删除: `BppIntegrationPlugin` 中的反射注册逻辑
- 新增: `BppCallbackPlugin.cs` 监听 `mcs_bppstatus` 字段变更，处理 Approved/Rejected/Withdrawn/Abandoned
- 参考: 同事《非限额申请审批》模块的BPP对接方式（前端调mcs_bppstartapi + Plugin监听mcs_bppstatus）

**当前状态：**
- ✅ domainaccount问题已解决
- ✅ BPP审批发起代码已重构
- ✅ BPP回调处理代码已完成
- ✅ UAT Plugin Step 已全部验证（通过 Solution 导入同步）
- ✅ UAT BPP 审批问题已解决：根因是 `BPPHandlerServiceForCreditRecord.GetBppFormData` 从 `mcs_credit_record` 查询了不存在的 `mcs_creditgrade` 字段，导致 FetchXML 异常；修复后改为从 `account` 实体查询并回写，UAT 审批流程可正常创建实例并回传 `mcs_workflowid`
- ❌ **BPP 审批通过后 Account 信用信息回写失败（2026-06-15 发现）**：`CreditRecordBppCallbackPlugin.UpdateAccountCreditInfo` 报错 `Incorrect attribute value type System.String`。根因：`account.mcs_creditgrade` 为 Picklist，代码按字符串赋值。本地独立 Assembly 已修复，待同步到主项目并重新部署 DEV/UAT 主 Assembly 后复测。
- 🔄 UAT Coface 测试：配置化代码已完成，DEV 测试通过；**DEV 主 Assembly 更新因 `LeadMainPostUpdate2IMCS` Type 差异暂停，UAT 尚未发布**
- 详细文档: `Documents/BusinessAnalysis/BPP/BPP交互流程详解.md`

Coface API 关键文档：
- `Coface API Solutions Flow - SANY.md` — 接口调用流程
- `科法斯接口清单API Mandatory Query Parameters.md` — 必填参数
- `Coface API Mandatory Query Parameters SANY20260512.md` — 沙盒环境配置

---

## 7. 待确认 / 待处理事项

### 7.2 Coface 汇率改用 D365 标准汇率评估（2026-06-21 记录）

> 状态：✅ 已合并到 `uat`、远程 Release 编译通过、DEV1 Plugin Assembly 已更新；默认 EUR 逻辑暂不修改；非 USD 货币真实场景验证待后续安排  
> 详细报告：`Documents/BusinessAnalysis/coface/D365标准汇率替换评估报告.md`

**背景**：业务提出将 Coface 对接中使用的汇率从自定义实体 `mcs_coface_exchange_rate`（2026 Budget 固定汇率）改为 D365 已维护的标准汇率。

**核心发现**：

| 评估项 | 结论 |
|---|---|
| 基础货币 | DEV1 / UAT 均为 **USD** |
| 维护币种 | DEV1 55 种，UAT 81 种；Coface 当前 17 种均已覆盖 |
| 汇率方向 | D365 为 **1 USD → LC**；Coface 需要 **1 LC → USD**，需取倒数 |
| DEV1 数据质量 | 多处异常：KRW `1,277,000`、MYR `0.24595`、SGD `0.1`、INR `46.69`、VND 缺失 |
| UAT 数据质量 | 相对正常，但 NZD (+43%)、JPY (+14%) 与 Coface 2026 Budget 偏差较大 |
| 全局影响 | D365 汇率被报价/订单/发票/回款等全财务模块共享 |

**结论与建议**：

- **当前不建议直接替换**。DEV1 的 D365 标准汇率数据质量不足以替代 Coface 2026 Budget 汇率。
- 若业务坚持改用 D365 汇率，需先确认：汇率更新频率/责任人、是否接受取倒数、DEV1 异常数据如何清洗、三环境如何保持一致。
- 建议采用**双轨读取**（配置开关 + 默认走原自定义表），保留回滚能力。

**已完成动作**：

- `MetadataTool/Program.cs` 新增：
  - `list-transaction-currencies` 命令，可查询 D365 标准汇率。
  - `test-coface-exchange-rate [币种列表]` 命令，本地连接 DEV1/UAT 测试汇率读取与方向转换。
- 已导出 Coface 汇率数据到 `Backups/Tests/coface-export/mcs_coface_exchange_rate.json`。
- 已生成对比分析报告。
- 已生成技术实施方案：
  - `Documents/BusinessAnalysis/coface/Coface汇率改用D365标准汇率_实施方案.md`（含方案 A 直接替换、方案 B 配置开关双轨读取）。
  - `Documents/BusinessAnalysis/coface/Coface汇率改用D365标准汇率_方案A实施文档.md`（方案 A 单独实施文档，待最终确认后执行）。

---

### 7.1 客户主数据表与 Account 表架构变更（2026-06-16 记录）

> 状态：📝 已记录，待后续与三一 IT 架构师牛同达确认后调整

**当前数据模型：**
| 实体 | 逻辑名 | 关键字段 |
|---|---|---|
| 客户主数据表 | `mcs_customermasterdata` | `new_customermasterdata_id`（与 account 关联）、客户编码字段 |
| 客户表 | `account` | `accountid`（序列号）、`accountnumber`（客户编码）、`mcs_customermasterdata`（Lookup → mcs_customermasterdata） |

**两表关系：**
```
account.mcs_customermasterdata = mcs_customermasterdata.new_customermasterdata_id
```

**历史理解与最新建议：**
- **2026.5.28 前理解**：新增客户字段需同时在 `account` 表和 `mcs_customermasterdata` 表增加，并在数据同步插件中维护两表一致性；数据录入在客户管理模块。
- **2026.5.28 理解**：仅需在 `mcs_customermasterdata` 表增加字段，未来客户管理界面会显示相关字段。
- **最新建议（待确认）**：只修改客户主数据管理和表，不再维护 `account` 表及其页面。

**最终方案（2026-06-16 确认）：**

1. **8 个自定义字段在 `mcs_customermasterdata` 上全部新建**，字段名与 Account 上保持一致。
2. **`mcs_credit_record.mcs_accountid` 继续指向 `account`**，不改为指向 `mcs_customermasterdata`。
3. **8 个字段之外的客户字段（如英文名称、国家、客户类别、客户类型等）继续从 `account` 读取**，相关代码不修改。
4. **只调整与这 8 个自定义字段相关的代码**：读取/写入这 8 个字段时，目标从 `account` 改为 `mcs_customermasterdata`。

**需迁移的 8 个 Account 新增字段（全部在 `mcs_customermasterdata` 上新建）：**

| # | Account 字段 | 描述 | 类型 | 备注 |
|---|---|---|---|---|
| 1 | `mcs_cofaceid` | 科法斯客户代码 | String | 已在 Account 上存在 |
| 2 | `mcs_dealerrank` | 经销商分级 | Picklist | 1钻石/2铂金/3白银/4认证/5意向，与 `mcs_customermasterdata.mcs_dealerlevel` 不对应 |
| 3 | `mcs_externalrate` | 客户信用外部评级 | String | 已在 Account 上存在 |
| 4 | `mcs_overduemodel` | 逾期未回收率模型分 | Decimal | 已在 Account 上存在 |
| 5 | `mcs_creditscore` | 客户信用评分 | Decimal | 已在 Account 上存在 |
| 6 | `mcs_creditgrade` | 客户等级 | Picklist | 100000000-100000004 (A0-A4)，与 `mcs_customermasterdata.mcs_creditlevel` 不对应 |
| 7 | `mcs_isdd` | 重点尽调 | Boolean | 已在 Account 上存在 |
| 8 | `mcs_creditvalid` | 信用评估有效状态 | Boolean | 已在 Account 上存在 |

**不在本次功能范围内的字段（F7.1 客户画像模块已暂缓）：**

| Account 字段 | 说明 |
|---|---|
| `mcs_blacklist` | 黑名单客户，属于 F7.1 客户信用画像模块，**已确认暂缓开发**（见 `开发计划.md` 21.1 节） |
| `mcs_creditgrant` | 不予授信客户，属于 F7.1 客户信用画像模块，**已确认暂缓开发** |

> 当前 UAT 上 Account 和 `mcs_customermasterdata` 都没有这两个字段，`mcs_credit_profile.html` 中也未引用。若后续重启 F7.1，再按开发计划新增双表字段和同步 Plugin。

**需要修改的代码清单（已完成）：**

| # | 资产 | 修改内容 | 状态 |
|---|---|---|---|
| 1 | `MetadataTool/Program.cs` | 新增 `mcs_customermasterdata` 8 个字段批量创建逻辑；复用 `AddAccountCreditFields` 保持 account/主数据字段定义一致；`mcs_creditgrade` 统一为 Picklist（A0-A4） | ✅ |
| 2 | `BppCallbackPlugin.cs` 的 `UpdateAccountCreditInfo` | 审批通过后，`mcs_creditscore`/`mcs_creditgrade`/`mcs_creditvalid` 回写目标从 `account` 改为 `mcs_customermasterdata`（通过 `account.mcs_customermasterdata` 关联） | ✅ |
| 3 | `AccountValidationPlugin.cs` | 移除 8 个字段校验，仅保留 `mcs_blacklist`/`mcs_creditgrant`（F7.1 暂缓） | ✅ |
| 4 | `CustomerMasterDataValidationPlugin.cs`（新增） | 注册在 `mcs_customermasterdata` Create/Update PreOperation，校验 `mcs_cofaceid`/`mcs_creditgrade`/`mcs_creditvalid`/`mcs_dealerrank`/`mcs_isdd` | ✅ |
| 5 | `mcs_credit_record.js` | `mcs_cofaceid` 带出来源改为 `account.mcs_customermasterdata.mcs_cofaceid`；其他客户基础字段仍从 `account` 带出 | ✅ |
| 6 | `mcs_coface_company_search.html` | 绑定 Coface ID 时，更新 `mcs_customermasterdata.mcs_cofaceid` 而非 `account.mcs_cofaceid` | ✅ |
| 7 | `mcs_credit_profile.html` | 读取 `mcs_creditscore`/`mcs_creditgrade`/`mcs_creditvalid`/`mcs_externalrate`/`mcs_dealerrank`/`mcs_isdd` 时，数据源改为 `account.mcs_customermasterdata` | ✅ |
| 8 | `mcs_customermasterdata.js`（新增） | 客户主数据表单 JS：8 个字段只读控制、Coface ID 格式校验、信用评估状态提示 | ✅ |
| 9 | `mcs_account.js` | 保留原逻辑（Account 上 8 字段若仍存在仍可展示/校验）；本次不删除 | ⏸️ 按需 |

**无需修改的资产（已核实）：**

| 资产 | 说明 |
|---|---|
| `CofaceDataSyncPlugin.cs` | 读取 `mcs_credit_record.mcs_cofaceid`，而评估记录的 cofaceid 已通过 `mcs_credit_record.js` 改从 `mcs_customermasterdata` 带出，Plugin 本身无需改动 |
| `CreditScorePlugin.cs` | 评分卡匹配使用 `account.mcs_accountcategory`/`mcs_accountlevel`/`mcs_accounttype`，均不在 8 个迁移字段范围内 |
| `mcs_credit_wheel.html` | 当前从 `mcs_credit_record.mcs_creditscore` 读取总分，无需改动 |

**页面/WebResource 状态：**

| # | 资产 | 绑定实体 | 说明 |
|---|---|---|---|
| 1 | `mcs_customermasterdata.js` | `mcs_customermasterdata` | 新增，需部署并绑定到客户主数据表单 |
| 2 | `mcs_credit_profile.html` | `account`（数据源已切到主数据表） | 客户信用画像页面 |
| 3 | `mcs_credit_wheel.html` | `account`（数据源已是评估记录） | 客户飞轮页面 |
| 4 | `mcs_account.js` | `account` | 保留，若 Account 上 8 字段后续删除再移除 |

**待确认问题：**
1. 客户主数据表单上是否需要新增这 8 个字段的展示/编辑？还是只作为系统回填字段隐藏？
2. 信用评估页面（画像/飞轮）是否立即迁移到客户主数据表单，还是等业务确认后再迁移？

**行动计划：**

| 步骤 | 任务 | 涉及资产 | 备注 |
|---|---|---|---|
| 1 | 在 `mcs_customermasterdata` 新建 8 个字段 | `MetadataTool`：`dotnet run add-fields mcs_customermasterdata` | 字段名与 Account 一致 |
| 2 | 修改 BPP 审批回写逻辑 | `BppCallbackPlugin.cs` | 回写到 `mcs_customermasterdata` |
| 3 | 迁移字段校验 Plugin | `AccountValidationPlugin.cs` + 新增 `CustomerMasterDataValidationPlugin.cs` | 8 字段校验注册到主数据表 |
| 4 | 修改前端数据源 | `mcs_credit_record.js` / `mcs_coface_company_search.html` / `mcs_credit_profile.html` | 8 字段读取/写入目标改为主数据表 |
| 5 | 新增客户主数据表单 JS | `mcs_customermasterdata.js` | 部署并绑定到主数据表单 |
| 6 | 本地编译验证 | 3 个 Plugin 项目 + JS 语法检查 | ✅ 已完成 |
| 6 | 修改信用评估记录表单 JS | `mcs_credit_record.js` | `mcs_cofaceid` 来源改为主数据表 |
| 7 | 迁移信用画像/飞轮页面（后续按需） | HTML/JS WebResource | 绑定到客户主数据表单 |
| 8 | DEV 测试 + UAT 测试 | 测试用例总集、Bug 记录 | 重点验证带出、评分、回写、审批流程 |
| 9 | 清理 Account 上的这 8 个新增字段 | Account 表单、字段定义 | 最后执行，确认无其他模块依赖 |
| 10 | 更新数据字典和开发文档 | `Documents/DataDictionary/`、`开发计划.md` | 保持文档一致 |

**风险与兼容性：**
- `mcs_credit_record.mcs_accountid` 保持指向 `account`，通过 `account.mcs_customermasterdata` 间接关联主数据表，历史记录无需迁移 Lookup。
- 在 `mcs_customermasterdata` 新建字段后，需确认 `account` 与 `mcs_customermasterdata` 之间的同步插件不会把新字段值反向同步回 `account`（避免清理后出现数据不一致）。
- `mcs_creditgrade` 在 `AccountValidationPlugin` 中为字符串类型，在 `BppCallbackPlugin` 中为 OptionSetValue，新建 `mcs_customermasterdata.mcs_creditgrade` 时需统一为 Picklist。

**下一步：** 准备 `mcs_customermasterdata` 8 个字段的实体定义 JSON，确认后即可开始改代码。

---

### 7.2 评分卡字段外部系统取值待对接（2026-06-19 记录）

> 状态：⏸️ 暂缓，待 CRM/BW 系统接口方案确认

**背景：**
评分卡中有部分字段需要从 CRM 或 BW 等外部系统取值。根据最新业务梳理，除**客户评级**已实现从 CRM 客户分类分级字段取值外，其余涉及历史交易的字段暂不具备对接条件。

**字段对接状态：**

| 序号 | 评分卡项目 | 数据来源 | 取数说明 | 对接状态 |
|------|-----------|---------|---------|---------|
| 17 | 客户评级 | CRM系统取值判断 | 根据 CRM【客户分类分级】字段取值 | ✅ 已对接 |
| 18 | 历史采购金额 | CRM系统取值判断 | 客户在过去历史交易中的累计采购金额 | ⏸️ 待对接 |
| 19 | 历史逾期 | BW系统取值判断 | 海外在货款明细中逾期金额的最大值 | ⏸️ 待对接 |
| 20 | 还款习惯 | BW系统取值判断 | 海外在货款明细中逾期天数的最大值 | ⏸️ 待对接 |

**阻塞原因：**
- CRM 历史交易数据接口尚未明确（数据范围、聚合方式、权限控制）。
- BW 系统（erp/bw，表名：海外在货款明细）接口暂未开放，当前无法自动获取历史逾期金额/天数。

**当前影响与应对：**
- 上述字段在数据集成阶段暂时为空，不影响评分卡其他字段计算。
- 在人工复核阶段，可由业务人员线下收集数据后，通过复核字段补录到 `mcs_customer_tag`。
- 待接口就绪后，通过 Phase 5 内部接口开发自动回填，并重新触发评分计算。

**下一步行动：**
1. 与三一 IT 确认 CRM 历史交易数据接口方案（数据视图、API、调用频率）。
2. 与 BW 团队确认海外在货款明细接口方案（主键、字段、筛选条件）。
3. 接口方案确认后，在 `SanyD365.D365ExtensionApi` 或 `SanyD365.InnerApi` 中新增内部 API。
4. 在 `CofaceDataSyncPlugin` 或新增内部数据同步 Plugin 中调用内部 API，回填标签记录。

---

### 7.2.1 历史交易类内部指标数据源确认与实现（2026-06-19 记录 / 2026-07-01 完成）

> 状态：✅ 已实现并推送到远程分支

**已确认信息：**

| 评分项目编码 | 评分项目名称 | 数据类型 | 内外部 | 数据源 |
|-------------|-------------|---------|--------|--------|
| `SalesAmount` | 历史采购金额 | 定量 | 内部（100000000） | `salesorder.totalamount_base`（USD，排除 statecode=2） |
| `ARAmount` | 历史逾期金额 | 定量 | 内部（100000000） | `mcs_outstanding.mcs_overdueamount`（CNY→USD，24个月内逾期记录最大值） |
| `ARAge` | 历史逾期账龄（还款习惯） | 定量 | 内部（100000000） | `mcs_outstanding.mcs_overdurationdays`（24个月内逾期记录最大值） |

**实现方式：**
在 `CofaceDataSyncPlugin` 状态 11（数据集成）触发时，调用新增方法 `GetInternalTransactionData`：
1. `SalesAmount`：Query `salesorder`，按 `customerid` 过滤，排除 `statecode=2`，累加 `totalamount_base`
2. `ARAmount` / `ARAge`：Query `mcs_outstanding`，按 `mcs_account` 过滤且 `mcs_overdue=true`，取 24 个月内记录，分别取 `mcs_overdueamount` 最大值和 `mcs_overdurationdays` 最大值；`ARAmount` 通过 `CofaceExchangeRateHelper.GetRateToUsd` 由 CNY 转换为 USD

**修改文件：**
`Code/Customizations/Plugins/CofaceIntegration/Plugin/CofaceDataSyncPlugin.cs`

**Git 分支：**
`uat-20260701-peter-internal-historical-data` → Azure DevOps（待用户合并 PR）

**远程编译：**
✅ 通过（仅项目原有 NU1902 警告，无新增错误）

**DEV1 操作：**
✅ 已注销独立 Assembly `SanyD365.Plugins.CofaceIntegration`（Step/Type/Assembly）

**待验证：**
🔄 新建信用评估记录，状态 11 触发后检查三个标签值是否正确

---

### 2.15 当前进行中的工作（厂端授信模块 — FcaProcCalculationPlugin）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 ~ 2026-07-07 |
| 目标 | 实现厂端授信模型计算表 `mcs_fca_proc` 状态变为 2（模型计算）时的核心额度计算逻辑，并归并到主 Assembly 完成 DEV1 部署验证 |
| 涉及实体 | `mcs_fca_proc`（厂端授信模型计算表）<br>`mcs_fca_mdlconfig`（厂端授信模型配置表）<br>`mcs_fca_mdlversion`（厂端授信模型版本表）<br>`mcs_customermasterdata`（客户主数据表）<br>`account`（客户表）<br>`mcs_customer_tag`（客户信用标签表）<br>`mcs_outstanding`（客户月度在外货款） |
| 涉及文件 | `Code/Customizations/Plugins/FactoryCredit/Calculation/FcaProcCalculationPlugin.cs`<br>`Code/Customizations/Plugins/FactoryCredit/Calculation/Services/CustomerCategoryService.cs`<br>`Code/Customizations/Plugins/FactoryCredit/Calculation/Services/ModelParameterService.cs`<br>`Code/Customizations/Plugins/FactoryCredit/Calculation/Services/ThreeFactorCalculationService.cs`<br>`Code/Customizations/Plugins/FactoryCredit/Calculation/Services/OverdueAdjustmentService.cs`<br>`Code/Customizations/Plugins/FactoryCredit/Calculation/Services/CreditRejectCheckService.cs`<br>`Code/Customizations/Plugins/FactoryCredit/Calculation/Services/CalculationLogService.cs`<br>`Code/Customizations/Plugins/FactoryCredit/Calculation/Services/CurrencyConversionHelper.cs`<br>`Code/Tools/sync-plugin-to-remote.py` |
| 实现要点 | 当 `mcs_fca_proc.mcs_status` 从非 2 变为 2（模型计算）时：<br>1. 根据客户主数据字段计算客户分类（个人/经销商/大客户/其他公司客户）<br>2. 按客户分类 + 客户等级查询 `mcs_fca_mdlconfig`，读取三因子系数、聚合方法、历史基准额度<br>3. 计算三因子：财务能力（净资产×系数）、历史回款能力（近12个月月均回款×汇率×系数）、历史基准额度（基准额度×系数）<br>4. 按聚合方法（MAX/MIN）得到模型授信额度<br>5. 根据逾期规则调整额度：逾期>180天且逾期占比>50%→0；20%-50%→×50%；≤20%→×80%<br>6. 执行不予授信校验：实质性逾期触发场景3；黑名单客户触发场景4；回写 `mcs_customermasterdata.mcs_creditgrant=true`<br>7. 按 PRD 1-9 步骤生成计算日志并回写 `mcs_fca_proc.mcs_modeldesc`<br>8. 回写 `mcs_modelgrant`（模型授信额度）、`mcs_initigrant`（初始授信额度）、`mcs_versionid`（模型版本 Lookup）、`mcs_doproc`（计算日志） |
| 本地状态 | ✅ 独立项目 `Code/Customizations/Plugins/FactoryCredit/` 编译通过（Release）<br>✅ 本地测试工具 `Code/Tools/FactoryCreditTest` 可正常调用服务方法 |
| 远程主项目归并 | ✅ 已通过 `sync-plugin-to-remote.py` 同步 `FcaProcCalculationPlugin.cs` + 10 个 Service 类到 `tx-windows`<br>✅ 远程 `SanyD365.D365Extension.Sales.csproj` 更新并编译通过（0 错误）<br>✅ 分支 `uat-20260707-peter-fca-calculation` 已创建并推送，用户已合并 PR<br>✅ 拉取合并后的 `uat` 重新编译成功 |
| DEV1 部署 | ✅ 主 Assembly `SanyD365.D365Extension.Sales` 已更新（ID: `9d6ff315-8c03-4d51-b641-ebeccf9e98b0`，modified=`2026/7/6 23:31:08`）<br>✅ Plugin Step 已注册：`FcaProcCalculationPlugin: Update of mcs_fca_proc`，PostOperation/Sync，Filter=`mcs_status`（Step ID: `07192e40-9379-f111-ab0e-7ced8db4dda8`）<br>✅ PreEntityImage 已注册：`PreImage` / Alias=`PreImage`，字段=`mcs_status`（Image ID: `4876044a-9379-f111-ab0e-7ced8db4d7a7`） |
| DEV1 独立 Assembly 测试 | ✅ 已注册临时 Assembly `SanyD365.Plugins.FactoryCredit`，Update PostOperation Step + PreEntityImage<br>✅ 场景1：S级/A2/信用分85，正常三因子计算<br>✅ 场景2：逾期占比 100% → 额度归 0，触发不予授信场景3<br>✅ 场景3：逾期占比 30% → 额度 ×50%<br>✅ 场景4：逾期占比 10% → 额度 ×80%<br>✅ 场景5：黑名单客户 → 触发不予授信场景4<br>✅ 场景6：信用分空/0 或 `mcs_creditvalid≠true` → 按历史基准额度兜底 |
| DEV1 主 Assembly 端到端验证 | ✅ 测试客户：五强集团（`mcs_customermasterdata`: `e8b28b63-2b8a-4516-9a79-e068efe481f0`）<br>✅ 创建 `mcs_fca_proc` → 状态 1→2 → 模型计算成功：模型计算额度 USD 1,000,000 / 调整模型额度 USD 1,000,000 / 计算日志完整<br>✅ 状态 2→3 → 生效启用成功：生成 `mcs_fca_quota` 额度记录（卖方额度/余额 1,000,000，已生效）与 `mcs_fca_records` 初始化台账（环节=1，调整类型=1） |
| DEV1 清理 | ✅ 已注销临时独立 Assembly `SanyD365.Plugins.FactoryCredit`（Step/Type/Assembly） |
| 状态 | ✅ 已完成并合并到 `uat`，DEV1 验证通过 |
| 待确认 | `mcs_fca_quota.mcs_quotano`（额度编码）是否需自动编号规则；PRD 未明确，待业务确认 |
| UAT 发布 | ✅ 已通过 n8n Release Tool 发布 `McsPlugin` 到 UAT（仅勾选 Plugin Assembly/Step，未勾选实体） |
| 状态 | ✅ 已完成并合并到 `uat`，DEV1 验证通过，UAT 已发布 |
| 待确认 | `mcs_fca_quota.mcs_quotano`（额度编码）是否需自动编号规则；PRD 未明确，待业务确认 |

---

### 7.3 BPP 审批通过 365 天后评估记录失效（2026-06-28 更新）

> 状态：✅ 已实现。`CreditRecordExpiration` 服务项目已扩展，支持同时失效 `mcs_credit_record.mcs_active` 和 `mcs_customermasterdata.mcs_creditvalid`。本地编译通过。

**需求描述：**
客户信用分经 BPP 审批通过后，评估记录的【有效状态】初始为有效。若审批日期超过 365 天，则该评估记录的【有效状态】应自动置为失效，并同步将客户主数据的 `mcs_creditvalid` 置为失效。

**涉及字段：**
- `mcs_credit_record.mcs_active`：评估记录有效状态（Boolean，审批通过时置为 `true`）
- `mcs_credit_record.mcs_approvedate`：审批日期（DateTime，审批通过时填充 `DateTime.Now`）
- `mcs_customermasterdata.mcs_creditvalid`：客户主数据信用评估有效状态

**实现方案：**
采用 `Code/ServiceJobs/CreditRecordExpiration/` 定时任务项目，每日扫描并批量更新：
1. 查询所有 `mcs_active = true` 且 `mcs_approvedate < 今天 - 365 天` 的 `mcs_credit_record`
2. 批量将这些记录的 `mcs_active` 更新为 `false`
3. 收集受影响的 `accountid`，对每个客户检查是否仍存在其他未过期且有效的评估记录
4. **仅当客户没有其他有效评估记录时**，才将其关联的 `mcs_customermasterdata.mcs_creditvalid` 更新为 `false`

**修改文件：**
- `Code/ServiceJobs/CreditRecordExpiration/CreditRecordExpirationService.cs`
  - 新增 `ExpiredCreditRecord`、`ExpirationPreviewResult`、`ExpirationResult` 记录类型
  - `QueryExpiredRecordIdsAsync` 改为 `QueryExpiredRecordsAsync`，同时返回评估记录 ID 和关联客户 ID
  - 新增 `ExpireCustomerMasterDataAsync`：批量更新客户主数据 `mcs_creditvalid`
  - 新增 `CountAffectedCustomerMasterDataAsync`：预览受影响的客户主数据数量
  - 新增 `GetCustomerMasterDataIdAsync`：通过 `account.mcs_customermasterdata` 查找主数据 ID
  - 新增 `HasValidCreditRecordAsync`：判断客户是否仍有未过期的有效评估记录
- `Code/ServiceJobs/CreditRecordExpiration/Program.cs`
  - 预览模式输出：待失效记录数、涉及客户数、预计受影响客户主数据数
  - 执行模式输出：实际失效记录数、实际失效客户主数据数

**运行方式：**
```bash
cd Code/ServiceJobs/CreditRecordExpiration

# 预览模式（默认）
dotnet run

# 实际执行
dotnet run --execute
```

**验证状态：**
- ✅ 本地编译通过（`0 个警告，0 个错误`）
- ✅ DEV1 实际测试通过：
  - 初始预览：0 条过期记录（DEV1 无真实过期数据）
  - 使用 `--seed-test-data` 构造 2 条过期评估记录（`SCO202606160004`、`SCO202606110002`，approvedate 设为 1 年前）
  - 预览模式正确识别 2 条待失效记录，但因该客户最初仍有 1 条未过期有效记录，预计 0 个客户主数据失效
  - 将第 3 条记录也设为过期后，预览模式正确识别 2 条待失效记录、1 个客户、预计 1 个客户主数据失效
  - `--execute` 实际执行：成功失效 2 条评估记录，成功将 1 个客户主数据的 `mcs_creditvalid` 标记为 `false`
  - `show-profile` 验证：`mcs_customermasterdata.mcs_creditvalid` 已显示为 `False`
  - `--diagnose` 验证：`mcs_active = true` 的评估记录数变为 0

**测试影响：**
- DEV1 中 `LTC客户-1` 的 2 条测试评估记录被设为 `mcs_active = false`，`mcs_creditvalid` 被设为 `false`
- 这些均为 DEV 测试数据，如需要恢复可手动重新执行 `simulate-approval` 或 `update-credit-record`

**下一步：**
1. ✅ DEV1 测试已完成
2. 根据部署环境配置定时调度（Windows 计划任务 / Azure Function / 手动执行）
3. 补充测试用例（TC-FLOW / TC-EVAL 中增加 365 天失效场景）

---

### 7.4 定性评分项目枚举值三一标准映射（2026-07-01 记录）

> 状态：✅ DEV1 验证通过。Coface 同步生成 L/M/H/O 标准编码标签，评分计算匹配新评分卡配置，信用分计算正常  
> 决策：采用**配置化映射方案 B**，把 Coface 原始值 → 三一标准编码的映射关系维护在 `mcs_credititem_value` 基础数据中，代码通过查表映射。

**背景**：
- 当前 `mcs_credititem_value` 中国别风险按 Coface 原始编码维护（A1/A2/A3/A4/B/C/D/E + O），导致前端下拉重复显示（两个低风险、两个中风险、四个高风险）。
- PRD 要求以三一标准四级（L/M/H/O）为基础，标签取数仍按 Coface 编码去取，前端展示和评分卡匹配使用三一标准。

**方案要点**：
1. `mcs_credititem_value` 新增字段 `mcs_cofacevalue`（字符串），存储 Coface 原始值映射，多个值用英文逗号分隔。
2. 国别风险/行业风险合并为 4 条记录：
   - `mcs_listvalue` = L/M/H/O（三一编码，用于评分卡匹配和标签保存）
   - `mcs_listname` = 低风险/中风险/高风险/缺失（前端展示）
   - `mcs_cofacevalue` = A1,A2 / A3,A4 / B,C,D,E / O 等
3. 修改 `CofaceQualitativeMappingHelper.cs`：新增 `GetSanyValueByCofaceValue`，按 `itemCode + mcs_cofacevalue` 查表返回 `mcs_listvalue`。
4. 修改 `CofaceDataSyncPlugin.cs`：保存标签时把 Coface 原始值映射为 L/M/H/O 写入 `mcs_itemvalue1`，中文显示名写入 `mcs_itemtxtvalue1`。
5. 修改 `ScoreCalculator.cs`：定性匹配时从 `mcs_credititem_value` Lookup 读取 `mcs_listvalue`（L/M/H/O）进行匹配；中文复核值（低风险/中风险/高风险）归一化为 L/M/H/O。

**已完成动作**：
- ✅ 新增字段 `mcs_credititem_value.mcs_cofacevalue`（字段 ID `0e29f2bb-0775-f111-ab0e-7ced8db4d37f`），通过 `D365ToolCommon.Metadata.MetadataFieldService` 创建。
- ✅ 修改 `CofaceQualitativeMappingHelper.cs`：新增 `CofaceValueField` 常量 + `GetSanyValueByCofaceValue` 查表映射。
- ✅ 修改 `CofaceDataSyncPlugin.cs`：定性标签保存时先映射为 L/M/H/O 再写入 `mcs_itemvalue1`/`mcs_itemvalue2`；中文显示名写入 `mcs_itemtxtvalue1`/`mcs_itemtxtvalue2`。
- ✅ 修改 `ScoreCalculator.cs`：评分卡定性匹配读取 `mcs_credititem_value.mcs_listvalue`；新增 `NormalizeQualitativeValue` 兼容中文复核值。
- ✅ 本地 Plugin 编译通过：`CofaceIntegration` 和 `CreditScore` 均 `0 警告 0 错误`。
- ✅ 修复 `MetadataTool` 编译错误（`Program.cs` 补充 `using Microsoft.Crm.Sdk.Messages;`）。
- ✅ DEV1 执行 `dotnet run fix-qualitative-enums-mapping`：
  - **CountryRisk**：创建 L/M/H 3 条，更新 O 1 条，停用旧记录 8 条，更新评分卡 21 条。
  - **SectorRisk**：创建 L/M/H 3 条，更新 O 1 条，停用旧记录 4 条，更新评分卡 18 条。
- ✅ 新增方案文档：`Documents/Planning/定性评分项目枚举值三一标准映射方案.md`。
- ✅ 同步到远程主项目（仅 CofaceIntegration + CreditScore/ScoreCalculator）：
  - 临时调整 `sync-plugin-to-remote.py` 的 `FILE_MAP` / `NAMESPACE_MAP`，同步 15 个文件。
  - 远程 `SanyD365.D365Extension.Sales.csproj` 编译通过（项目原有警告，无新增错误）。
- ✅ 更新 DEV1 `SanyD365.D365Extension.Sales` Assembly：
  - DLL 大小 8183 KB，Assembly ID `9d6ff315-8c03-4d51-b641-ebeccf9e98b0`。
- ✅ DEV1 端到端验证（测试记录 `SCO202606160004`）：
  - 重新触发状态 11（Coface 同步）→ 12（人工复核）→ 13（信用分计算）。
  - `CountryRisk`：`value1=M`，`txtvalue1=中风险`（原 `A3` 已映射为 `M`）。
  - `SectorRisk`：`value1=M`，`txtvalue1=中风险`（原 `2` 已映射为 `M`）。
  - 信用分从 `40` 重新计算为 `87`，评分卡 Lookup 匹配正常。
  - Coface 同步状态 `SUCCESS`，Report 附件正常保存。

**文档位置**：`Documents/Planning/定性评分项目枚举值三一标准映射方案.md`

**风险与待处理**：
- 历史 `mcs_customer_tag.mcs_itemvalue1` 中可能仍存 A1/A2… 等旧值，用户明确不需要处理；后续 Coface 同步和评分计算均走新逻辑即可。
- `mcs_credititem_value` 实体发布命令曾 300s 超时，但 `list-fields` 已确认字段存在；如后续窗体/视图需要展示 `mcs_cofacevalue`，可再次尝试发布。
- 新增字段、数据整理均在 DEV1 完成；代码改动尚未同步到远程主项目 `SanyD365.D365Extension.Sales`，DEV1 主 Assembly 也未更新。

**下一步**：
1. 在 D365 UI 验证 `mcs_credititem_value` 下拉是否只显示 4 条标准记录（L/M/H/O）。
2. 按发布流程提交 PR / 发布 UAT。
3. UAT 如需同步 `mcs_credititem_value` 数据（创建 L/M/H/O 记录、停用旧记录、重指向评分卡 Lookup），可在 UAT 执行 `fix-qualitative-enums-mapping`。

---

## 8. AI 协作指南

> **用户偏好（2026-06-18 更新）**：
> - 所有回复使用 **中文**
> - 需要展示 **思考过程 / 推理链**，不要只给结论
> - **思考过程 / 推理链也必须使用中文**（已写入根目录 `AGENTS.md`，新会话自动生效）
> - **AI 内部思考（analysis / reasoning）全程使用中文**，不需要用户每次提醒

### 8.1 接手项目时

1. **读 SKILL.md**（通用D365开发技巧）→ 了解工具和规范
2. **读 Memory**（本文件）→ 了解项目当前状态和资产
3. **读 `Code/Customizations/AGENTS.md`** → 了解本目录下代码操作的自动生效红线
4. **读 开发计划.md** → 了解当前进度和待办
5. **读 工作日报汇总.md** → 了解最近做了什么
6. **读 需求变更记录.md** → 了解原始 PRD 之后的业务需求变更（如 A0–A4 等级映射规则）
7. **按需查阅** 测试用例 / Bug 记录 / PRD / 数据字典

### 8.2 开发新功能时

1. 在 `开发计划.md` 中找到对应功能编号（如 F3.2.1）
2. 开发完成后，更新开发计划中的状态
3. 补充测试用例到 `测试用例总集.md`
4. 发现的 Bug 记录到 `测试Bug反馈记录.md`
5. 当天工作记录到 `工作日报汇总.md`
6. **更新 Memory 中的进度和待办**

### 8.3 修复 Bug 时

1. 在 `测试Bug反馈记录.md` 中找到对应 Bug
2. 修复后更新「修复记录」和「状态」
3. 关联的功能编号确保一致
4. 更新 `开发计划.md` 中的 Bug 关联表
5. **更新 Memory 中的进度**
6. **🚨 禅道 Bug 登记（2026-07-24 新增，强制）**：凡修 Bug 先在 `Documents/Tests/BugReports/禅道Bug修复记录.md` 登记禅道编号并全过程更新；commit message 关联编号；详见根目录 `AGENTS.md` 第 5 节

### 8.4 代码规范检查清单

```
□ Plugin 开头校验 target.LogicalName
□ JS 使用逻辑名而非硬编码 ID
□ 不修改原生系统组件
□ 选项集值先查询确认再编码
□ 窗体复制时同步修改 functionName
□ cell id 使用 Guid.NewGuid().ToString("B")
□ Plugin catch OrganizationService 异常后必须 rethrow（禁止吞异常）
□ 本地独立 Assembly 测试结束后必须执行 unregister-assembly 注销，避免影响他人发布 Solution
```

### 8.5 新增 MetadataTool 命令（2026-06-11）

| 命令 | 用途 | 示例 |
|------|------|------|
| `query-plugin-steps <类名>` | 查询指定 Plugin 的所有 Step | `dotnet run query-plugin-steps BppCallbackPlugin` |
| `query-plugin-namespace <前缀>` | 查询命名空间下所有 Plugin Steps | `dotnet run query-plugin-namespace SanyD365.D365Extension.Sales.Plugins.CreditRecord` |
| `query-assembly-version <名称>` | 查询 Assembly 版本号 + ModifiedOn | `dotnet run query-assembly-version SanyD365.D365Extension.Sales` |
| `add-webresource-to-solution <名称> <解决方案唯一名>` | 将 WebResource 加入指定 Solution | `dotnet run add-webresource-to-solution mcs_coface_company_search.html entity_20260603_peter` |
| `query-customapi-solution <唯一名>` | 查询 Custom API / Custom Action 所在 Solution 及实现 Plugin 归属 | `dotnet run query-customapi-solution mcs_QueryTradeStPayTerm` |
| `export-translations <Solution唯一名> <输出ZIP路径>` | 导出 Solution Translations ZIP | `dotnet run export-translations entity_20260626_peter_trans /tmp/trans.zip` |
| `import-translations <translations ZIP 路径>` | 导入 Solution Translations ZIP | `dotnet run import-translations /tmp/trans.zip` |
| `list-webresources [前缀]` | 列出 WebResource（默认 `ms_languagefile`） | `dotnet run list-webresources ms_languagefile` |
| `deploy-webresource <名称> <文件路径> <类型> [显示名]` | 创建/更新 WebResource 并自动发布 | `dotnet run deploy-webresource ms_languagefile_credit_test_2052 credit_test_2052.json 3` |

> 💡 **用途**：跨环境对比 Plugin 是否同步时，不要只看 `version`（可能都是 `1.0.0.0`），要对比 `modifiedon`。

---

*本文件记录当前项目的特定上下文，每次对话开始时自动读取。*
*更新规则：每完成一个里程碑、发现新Bug、或进度变更时更新。*

## 附录：DEV1 Custom API / Custom Action 归属查询结果（2026-06-25）

> 查询目标：确认 `mcs_QueryTradeStPayTerm` 与 `mcs_CofaceSearchCompany` 在 DEV1 中的 Solution 归属及 Implementation Plugin 归属。
> 查询工具：MetadataTool `query-customapi-solution` 命令（已新增）。

### A. `mcs_QueryTradeStPayTerm`

| 项目 | 结果 |
|------|------|
| 类型 | **Custom API**（`customapi` 表有记录） |
| Unique Name | `mcs_QueryTradeStPayTerm` |
| Custom API ID | `c86279d7-ae6e-f111-ab0f-7ced8de4eab4` |
| 所在 Solution | `McsCustomAPI`（主要归属）、`Default Solution` |
| Implementation Plugin Type | `QueryTradeStPayTermPlugin`（`c16279d7-ae6e-f111-ab0f-7ced8de4eab4`） |
| Plugin Type 所属 Assembly | `SanyD365.D365Extension.Sales`（`9d6ff315-8c03-4d51-b641-ebeccf9e98b0`） |
| Plugin Assembly 所在 Solution | `McsPlugin`、`Default Solution`、`Approval Center`、`D365-11489` 等 |

**结论**：Custom API 本体归 `McsCustomAPI`；实现 Plugin 随 `SanyD365.D365Extension.Sales` Assembly，已包含在 `McsPlugin` 中。

### B. `mcs_CofaceSearchCompany`

| 项目 | 结果 |
|------|------|
| 类型 | **不是 Custom API**，是 **Custom Action / 自定义 SdkMessage**（`customapi` 表无记录，`sdkmessage` 表有记录） |
| SdkMessage Name | `mcs_CofaceSearchCompany` |
| SdkMessage ID | `e074503d-8a67-f111-ab0c-7ced8de4efcf` |
| 关联 Plugin Steps | 2 个 |
| 主实现 Step | `b2d63491-c14d-4d18-b8ac-36b0818de6dd: mcs_CofaceSearchCompany of none` |
| Stage / Mode | `PostOperation`（40）/ `Synchronous`（0） |
| Implementation Plugin Type | `CofaceSearchCompanyPlugin`（`b2d63491-c14d-4d18-b8ac-36b0818de6dd`） |
| Plugin Type 所属 Assembly | `SanyD365.D365Extension.Sales`（`9d6ff315-8c03-4d51-b641-ebeccf9e98b0`） |
| Plugin Assembly 所在 Solution | `McsPlugin`、`Default Solution`、`Approval Center`、`D365-11489` 等 |
| 次要 Step | `SyncWorkflowExecutionPlugin`（系统 Workflow 执行插件，属 `Microsoft.Crm.ObjectModel`） |

**结论**：`mcs_CofaceSearchCompany` 在 DEV1 中以旧式 Custom Action 形式存在（D365 UI 中显示为 Process/Action），不是新版 Custom API 机制。其实现 Plugin `CofaceSearchCompanyPlugin` 同样位于 `SanyD365.D365Extension.Sales` Assembly，已包含在 `McsPlugin` 中。

### C. 关键区分

| 机制 | 存储表 | 特点 |
|------|--------|------|
| **Custom API** | `customapi` + `customapirequestparameter` + `customapiresponseproperty` | 现代推荐方式，可带类型化参数/响应 |
| **Custom Action** | `sdkmessage` + `sdkmessageprocessingstep`（Process 类型 Action） | 旧方式，参数通过 `InputParameters`/`OutputParameters` 传递 |

---

*本文件记录当前项目的特定上下文，每次对话开始时自动读取。*
*更新规则：每完成一个里程碑、发现新Bug、或进度变更时更新。*


## 会话更新（2026-06-28）— 信用评估记录 BPF 控制

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-28 |
| 目标 | 解决 `mcs_credit_record` 表单上 BPF 流程条的非法回退、字段可编辑、标准"下一阶段"按钮体验问题 |
| 已验证通过 | 1. BPF 回退已阻止：使用官方 Client API `formContext.data.process.addOnPreStageChange`，方向 `Previous` 时 `preventDefault()` + 提示<br>2. BPF 侧窗格字段已只读：通过 `formContext.ui.controls.forEach` 锁定所有 `header_process_*` 控件，并在 `addOnStageChange` 后重新锁定<br>3. BPF 标准"下一阶段"按钮已与工具栏《进入下一阶段》等价：方向 `Next` 时 `preventDefault()` + 调用 `CreditRecordForm.nextStep(formContext)`，走相同校验和 `updateStatus` 逻辑 |
| 已移除 | 未生效的 `closeBpfFlyout()` DOM 模拟关闭代码（选择器不匹配 D365 当前 UI） |
| 待后续处理 | **BPF 标准"下一阶段"点击后，flyout 侧窗格仍停留在前一步** — 需要获取 D365 当前 BPF flyout 的精确 DOM 选择器后，用 MutationObserver 或精确点击关闭按钮处理。用户确认先记下，后续再优化 |
| 相关文件 | `Code/Customizations/WebResources/JS/mcs_credit_record.js`<br>`Code/Customizations/Plugins/CreditRecord/Validation/CreditRecordStatusTransitionPlugin.cs`<br>`Code/Tools/MetadataTool/Services/EntityManager.cs`（新增 `RegisterPluginPreImage`）<br>`Code/Tools/MetadataTool/Program.cs`（新增 `register-plugin-image` 命令）<br>`Code/Tools/DeployTool/Program.cs`（`UpdateWebResource` 更新后自动发布 WebResource） |


## 会话更新（2026-06-29）— 厂端授信模块主窗体配置

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-29 |
| 目标 | 配置厂端授信模块 6 个实体的 D365 主窗体，保持与 PRD 原型图一致的 Section 标题和字段摆放 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260629_peter` |
| 6 个实体 | `mcs_fca_mdlversion`、`mcs_fca_mdlconfig`、`mcs_fca_proc`、`mcs_fca_quota`、`mcs_fca_quotaapp`、`mcs_fca_records` |
| 已完成 | 1. Phase 1 元数据双语标签：6 个实体 + `mcs_customer_file.mcs_fca_quotaappid` 英文显示名已更新；中文标签通过 `entity_20260629_peter` translations 导出/导入已补充并验证通过<br>2. Solution 迁移：6 个 `mcs_fca_*` 实体已从 `entity_20260603_peter` 改放到新 Solution `entity_20260629_peter`<br>3. `mcs_fca_mdlconfig` 表单：使用 `rearrange-form` 配置 4 个 Section（参数管理维度、模型系数和聚合方法、历史基准额度、记录更新信息），移除主字段 `mcs_argid`，已发布<br>4. `mcs_fca_mdlversion` 表单：Section「常规」，字段：是否生效 / 模型描述 / 开始日期 / 结束日期 / 编辑人 / 编辑时间；主字段 `mcs_versionid` 已从 body 移除，已发布<br>5. `mcs_fca_quota` 表单：Section「额度信息」，字段：客户编码 / 客户名称 / 厂端授信额度USD / 厂端授信余额USD / 模型计算序列号 / 是否生效 / 生效日期；主字段 `mcs_quotano` 已从 body 移除，Lookup 字段已修复，已发布<br>6. `mcs_fca_records` 表单：Section「台账信息」，字段：客户编码 / 客户名称 / 合同编码 / 订单编码 / 流程环节 / 额度调整动作 / 授信限额USD / 现有授信余额USD / 调整金额USD / 调整后授信余额USD / 记录修改人 / 记录修改时间；主字段 `mcs_recordid` 已从 body 移除，Lookup / Picklist 字段已修复，已发布<br>7. `mcs_fca_proc` 表单：Tab「授信校验」+「模型计算」，使用 `update-form-xml` 手动构造多 Tab XML 并发布；主字段 `mcs_doid` 已从 body 移除<br>8. `mcs_fca_quotaapp` 表单：Tab「常规/意向合同」+「调整额度」+「申请组织」+「审批（BPP）」+「附件」，附件 Tab 嵌入 `mcs_/CommonCore/Html/Uploader.html`；主字段 `mcs_grantid` 已从 body 移除 |
| 设计约束 | 1. Section 标题保持与 PRD 原型图一致<br>2. 主字段若与表单标题重复，从 body Section 中移除<br>3. 记录更新信息使用系统已有字段 `modifiedby` / `modifiedon`，不再新建自定义字段<br>4. 不允许自己写新代码布局表单，必须复用 `MetadataTool` 现有 `rearrange-form` 通用方法 |
| 进行中 | 暂无 |
| 待处理 | 1. 6 个实体主窗体已配置完成，待用户在 DEV1 UI 中统一查看效果<br>2. `mcs_customer_file.mcs_fca_quotaappid` 中文标签按用户要求暂不处理 |
| 已知问题 | `rearrange-form` 发布阶段偶发 300s 超时；窗体 XML 更新已持久化，如遇未发布可在 D365 表单设计器中手动点击发布 |
| 相关文件 | `Code/Tools/MetadataTool/Program.cs`<br>`Code/Tools/MetadataTool/Services/EntityManager.cs`<br>`Documents/Planning/厂端授信额度管理开发计划.md`<br>`Documents/BusinessAnalysis/厂端授信PRD.docx` |


## 会话更新（2026-06-30）— BPP 驳回后允许重新提交

| 项目 | 内容 |
|---|---|
| 日期 | 2026-06-30 |
| 目标 | 修复 BPP 审批被驳回后回到人工复核阶段，再次提交时被误拦截为"重复提交"的问题 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，UAT 记录 `SCO202606300002` 仅作复现参考 |
| 根因 | `BppIntegrationPlugin` 与 `mcs_credit_record.js` 的重复提交判断均只检查 `mcs_workflowid` 是否非空；BPP 驳回后 `mcs_workflowid` 仍保留，`mcs_bppstatus` 为 `Rejected`，导致再次提交时被误拦截 |
| 修改文件 | `Code/Customizations/Plugins/BppIntegration/Plugin/BppIntegrationPlugin.cs`<br>`Code/Customizations/WebResources/JS/mcs_credit_record.js`<br>远程主项目：`D365/SanyD365.D365Extension.Sales/Plugins/CreditRecord/CreditRecordBppIntegrationPlugin.cs` |
| 核心改动 | 重复提交判断改为：只有 BPP 状态为 **进行中**（`Submitted` / `InReview` / `Pending` / `10` / `20`）时才阻止；已结束状态（`Approved` / `Rejected` / `Withdrawn` / `Abandoned` / `SubmitFailed`）允许重新提交。JS 新增 `CreditRecordForm.isBppInProgress` 辅助函数，后端新增 `IsBppInProgress` 辅助方法 |
| 本地编译 | ✅ 通过（`BppIntegration` 项目：0 错误，0 警告） |
| 远程编译 | ✅ 通过（`SanyD365.D365Extension.Sales.csproj`：项目原有警告，无新增错误） |
| Git 分支 | `uat-260630-peter-bpp-resubmit` → `uat`（已合并） |
| Commit | `fix(credit): BPP驳回后允许重新提交，仅阻止进行中的流程重复发起` |
| DEV1 Assembly | ✅ `SanyD365.D365Extension.Sales` 已更新，`modifiedon` = `2026-06-30 10:45:52` |
| DEV1 WebResource | ✅ `mcs_credit_record.js` 已更新并发布 |
| 待验证 | 在 DEV1 模拟：第一次提交 → BPP 驳回 → 回到人工复核 → 再次提交，应可正常发起新流程 |
| UAT 发布 | 用户通过 n8n Release Tool 勾选 `McsPlugin` 发布（Azure 代码不勾选） |


## 会话更新（2026-07-01）— 字段 RequiredLevel 必须通过 Web API PUT 更新

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 将 `mcs_fca_mdlversion` 的 `mcs_modeldesc`、`mcs_validfrom`、`mcs_validend` 三个字段在 DEV1 设为业务必需 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260629_peter` |
| 尝试 1：SDK `UpdateAttributeRequest` | 不抛异常但状态不持久化；`list-fields` 仍显示 `Required=None` |
| 尝试 2：Web API `PUT` | ✅ 成功，三个字段均变为 `Required=ApplicationRequired` |
| 关键发现 | Device Code Flow 认证方式下，SDK `UpdateAttributeRequest` 对 `RequiredLevel` 的更新是"虚假成功"；必须通过 Web API `PUT` 到 `/api/data/v9.2/EntityDefinitions(LogicalName='...')/Attributes(LogicalName='...')`，并在 body 中携带完整类型化 payload（含 `@odata.type` 和 `RequiredLevel.Value`） |
| 实现改动 | 1. `Code/Tools/D365ToolCommon/Connection/D365ConnectionFactory.cs`：新增公共方法 `GetAccessTokenAsync(...)`，复用现有 Device Code Flow token 缓存，返回 `AccessToken`<br>2. `Code/Tools/MetadataTool/Services/EntityManager.cs`：`UpdateAttributeRequiredLevel` 改为 `async Task`，SDK 方式后继续以 Web API `PUT` 兜底；`UpdateAttributeRequiredLevelViaWebApi` 使用 `D365ConnectionFactory.GetAccessTokenAsync` 获取 token<br>3. `Code/Tools/MetadataTool/Program.cs`：`update-field-required` 分支改为 `await` |
| 验证结果 | ✅ `mcs_modeldesc` → `Required=ApplicationRequired`<br>✅ `mcs_validfrom` → `Required=ApplicationRequired`<br>✅ `mcs_validend` → `Required=ApplicationRequired` |
| 副作用 | 无；仅修改 MetadataTool 和 D365ToolCommon，未改远端主项目 |
| 相关文件 | `Code/Tools/D365ToolCommon/Connection/D365ConnectionFactory.cs`<br>`Code/Tools/MetadataTool/Services/EntityManager.cs`<br>`Code/Tools/MetadataTool/Program.cs` |


## 会话更新（2026-07-01）— 字段 RequiredLevel 公共方法提取 + mcs_fca_mdlversion 全字段必填

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 1. 将字段必填性更新逻辑沉淀到 `D365ToolCommon` 公共方法，供后续工具复用<br>2. 将截图中的 `mcs_versionid`（模型版本）和 `mcs_isactive`（是否生效）也设为业务必需 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260629_peter` |
| 公共方法 | `D365ToolCommon.Metadata.MetadataFieldService.UpdateRequiredLevelAsync(entityName, fieldLogicalName, required, url?)` |
| 方法实现 | 先尝试 SDK `UpdateAttributeRequest`；无论是否成功，都通过 Web API `PUT` 兜底。PUT payload 包含 `@odata.type`、`LogicalName`、`RequiredLevel.Value` |
| 关键修复 | Picklist 字段（如 `mcs_isactive`）的 Web API PUT 必须携带 `LogicalName`，否则返回 `0x80040203` "A null value was provided for attribute logicalname" |
| MetadataTool 改动 | `EntityManager.UpdateAttributeRequiredLevel` 已简化为调用 `MetadataFieldService.UpdateRequiredLevelAsync`，删除本地重复代码 |
| 验证结果 | ✅ `mcs_modeldesc` → `ApplicationRequired`<br>✅ `mcs_validfrom` → `ApplicationRequired`<br>✅ `mcs_validend` → `ApplicationRequired`<br>✅ `mcs_versionid` → `ApplicationRequired`<br>✅ `mcs_isactive` → `ApplicationRequired` |
| 相关文件 | `Code/Tools/D365ToolCommon/Metadata/MetadataFieldService.cs`<br>`Code/Tools/MetadataTool/Services/EntityManager.cs` |


## 会话更新（2026-07-01）— 厂端授信阶段一收尾（字段必填性 + 默认值 + 发布）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 完成厂端授信模块阶段一收尾：字段必填性、字段默认值、实体发布 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260629_peter` |
| 新增公共方法 | `D365ToolCommon.Metadata.MetadataFieldService.SetPicklistDefaultValueAsync(entityName, fieldLogicalName, defaultValue, url?)` — 设置选项集字段默认值，SDK + Web API PUT 兜底 |
| 新增 CLI 命令 | `dotnet run update-field-default <实体名> <字段名> <默认值>` — 仅支持 Picklist 字段 |
| 字段必填性（已设 ApplicationRequired） | **mcs_fca_mdlconfig**：`mcs_argid`<br>**mcs_fca_proc**：`mcs_doid`、`mcs_accountid`、`mcs_status`、`mcs_modeldesc`<br>**mcs_fca_quota**：`mcs_accountid`、`mcs_sellergrant`、`mcs_sellerbalance`、`mcs_isactive`<br>**mcs_fca_records**：`mcs_recordid`、`mcs_accountid`、`mcs_proccess`、`mcs_adjust`、`mcs_sellergrant`、`mcs_asisbalance`、`mcs_adjustamt`、`mcs_tobebalance`<br>**mcs_fca_quotaapp**：`mcs_grantid`、`mcs_accountid`、`mcs_initigrant`、`mcs_quotasum`、`mcs_quotabalance`、`mcs_sellergrant`、`mcs_sellerbalance`、`mcs_tobegrant`、`mcs_tobebalance` |
| 默认值（已完成） | `mcs_fca_mdlversion.mcs_isactive` → `1`（是） |
| 默认值（待决策） | `mcs_fca_mdlversion.mcs_validfrom` → "今天"（无法静态设置，需 Plugin/JS/Business Rule）<br>`mcs_fca_mdlversion.mcs_validend` → `9999/12/31`（可静态设置，待确认是否通过元数据默认值实现） |
| mcs_customer_file.mcs_fca_quotaappid | ✅ 已确认存在并可用 |
| 实体发布 | ✅ `mcs_fca_mdlversion`、`mcs_fca_mdlconfig`、`mcs_fca_proc`、`mcs_fca_quota`、`mcs_fca_records`、`mcs_fca_quotaapp` 均已发布 |
| 待办 | 1. 确认 `mcs_validfrom`/`mcs_validend` 默认值实现方式<br>2. 角色权限矩阵配置 |
| 相关文件 | `Code/Tools/D365ToolCommon/Metadata/MetadataFieldService.cs`<br>`Code/Tools/MetadataTool/Services/EntityManager.cs`<br>`Code/Tools/MetadataTool/Program.cs` |


## 会话更新（2026-07-01）— mcs_fca_mdlversion JS 表单脚本

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 用 JS 简单实现 `mcs_fca_mdlversion` 的日期默认值和重叠生效版本校验，不部署 Plugin |
| 文件 | `Code/Customizations/WebResources/JS/mcs_fca_mdlversion.js` |
| 实现功能 | 1. **onLoad**：新建记录时自动设置默认值<br>   - `mcs_isactive` = 1（是，如为空）<br>   - `mcs_validfrom` = 今天（如为空）<br>   - `mcs_validend` = 9999/12/31（如为空）<br>2. **onSave**：保存前校验是否存在重叠的生效模型版本<br>   - 仅当 `mcs_isactive` = 1 时校验<br>   - 查询其他 `mcs_isactive` = 1 的记录，检查 `[validfrom, validend]` 区间是否重叠<br>   - 重叠则 `preventDefault()` 并弹窗提示 |
| DEV1 部署 | ✅ WebResource `mcs_fca_mdlversion.js` 已部署并发布<br>✅ 已绑定到 `mcs_fca_mdlversion` 主窗体 `Information` 的 `onLoad` 和 `onSave` 事件<br>✅ 已加入 Solution `entity_20260629_peter` |
| 待完成 | 1. 自动编号（另一个 AI 在做）<br>2. 视图优化（最后一起做）<br>3. 角色权限（最后一起做） |
| 相关文件 | `Code/Customizations/WebResources/JS/mcs_fca_mdlversion.js`<br>`Code/Tools/MetadataTool/Services/EntityManager.cs`（`BindJsToForm` 增加 `mcs_fca_mdlversion` 映射） |


## 会话更新（2026-07-01）— mcs_fca_mdlversion 默认视图配置

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 按 PRD 截图配置 `mcs_fca_mdlversion` 默认公共视图列 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260629_peter` |
| 视图列（按顺序） | `mcs_versionid`（模型版本）、`mcs_isactive`（是否生效）、`mcs_modeldesc`（模型描述）、`mcs_validfrom`（开始日期）、`mcs_validend`（结束日期）、`modifiedby`（编辑人）、`modifiedon`（编辑时间） |
| 实现方式 | 在 `MetadataTool/Program.cs` 的 `update-view` 命令中新增 `mcs_fca_mdlversion` 字段映射 case；运行 `dotnet run update-view mcs_fca_mdlversion` |
| 执行结果 | ✅ Active Factory Credit Model Version 视图已追加 6 列并发布实体 |
| 待完成 | 1. 自动编号（另一个 AI 在做）<br>2. 角色权限矩阵（最后统一做）<br>3. 其余 5 个实体视图优化（最后统一做） |
| 相关文件 | `Code/Tools/MetadataTool/Program.cs` |


## 会话更新（2026-07-01）— 第二个实体 mcs_fca_mdlconfig 配置完成

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 完成厂端授信第二个实体 `mcs_fca_mdlconfig`（厂端授信模型参数配置表）的默认值、保存校验、视图配置 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260629_peter` |
| 默认值 | `mcs_aggfunc`（聚合方法）→ `MIN`（值 2）✅ 通过 `update-field-default` 设置成功<br>`mcs_adjust1/2/3`（系数1/2/3）→ `0`：Dataverse Decimal 字段不支持通过元数据设置 `DefaultValue`（SDK/Web API 均报错），改为通过表单脚本在新建时默认填充 |
| 表单脚本 | 文件：`Code/Customizations/WebResources/JS/mcs_fca_mdlconfig.js`<br>1. **onLoad**：新建记录时 `mcs_adjust1`/`mcs_adjust2`/`mcs_adjust3` 为空则设为 `0`<br>2. **onSave**：<br>   - 客户等级 = ALL（值 6）且历史基准额度 ≤ 1000 时阻断保存<br>   - 客户分类 + 客户等级 组合唯一校验（更新时排除当前记录） |
| DEV1 部署 | ✅ WebResource `mcs_fca_mdlconfig.js` 已部署并发布<br>✅ 已绑定到 `mcs_fca_mdlconfig` 主窗体 `Information` 的 `onLoad` 和 `onSave` 事件<br>✅ 已加入 Solution `entity_20260629_peter`<br>✅ `MetadataTool` 的 `bind-js` 实体映射增加 `mcs_fca_mdlconfig` → `FcaMdlConfigForm` |
| 视图配置 | 默认视图已按 PRD 追加 9 列：厂端授信参数编码、客户分类、客户等级、系数1、系数2、系数3、聚合方法、历史基准额度、编辑人、编辑时间 |
| 工具增强 | 1. `D365ToolCommon.Metadata.MetadataFieldService.SetDecimalDefaultValueAsync`（发现 SDK/Web API 均不支持后保留为诊断/兜底方法）<br>2. `MetadataTool.EntityManager.UpdateAttributeDefaultValue` 增加 `decimal` 重载<br>3. `MetadataTool.EntityManager.GetAttributeType` 查询字段类型<br>4. `MetadataTool.Program.cs update-field-default` 命令改为根据字段类型自动路由（Picklist/Boolean/Integer 走整数默认值；Decimal/Money/Double 走小数默认值） |
| 待完成 | `mcs_argid` 自动编号（其他 AI 负责） |
| 相关文件 | `Code/Customizations/WebResources/JS/mcs_fca_mdlconfig.js`<br>`Code/Tools/D365ToolCommon/Metadata/MetadataFieldService.cs`<br>`Code/Tools/MetadataTool/Services/EntityManager.cs`<br>`Code/Tools/MetadataTool/Program.cs` |


## 会话更新（2026-07-01）— mcs_fca_mdlconfig 字段描述补充

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 将 PRD 表设计中的「提示」补充到 `mcs_fca_mdlconfig` 三个系数字段的 Description |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`) |
| 更新字段 | `mcs_adjust1`：授信模型计算因子：财务能力=净资产 x 系数1<br>`mcs_adjust2`：授信模型计算因子：历史回款能力=近12个月客户回款月均值 x 系数2<br>`mcs_adjust3`：授信模型计算因子：按客户分类(S/A/B/C/I和经销商分级）维度人工维护值 x 系数3 |
| 工具增强 | `D365ToolCommon.Metadata.MetadataFieldService.UpdateDescription` — 按字段类型构造对应 AttributeMetadata 子类并更新 Description<br>`MetadataTool.EntityManager.UpdateAttributeDescription` — 封装调用<br>`MetadataTool.Program.cs` 新增 CLI 命令 `update-field-description <实体名> <字段名> <描述>` |
| 执行结果 | ✅ 三个字段描述已更新，实体 `mcs_fca_mdlconfig` 已发布 |
| 相关文件 | `Code/Tools/D365ToolCommon/Metadata/MetadataFieldService.cs`<br>`Code/Tools/MetadataTool/Services/EntityManager.cs`<br>`Code/Tools/MetadataTool/Program.cs` |


## 会话更新（2026-07-01）— mcs_fca_mdlconfig 完整 PRD 描述补充

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 通读 PRD 中 `mcs_fca_mdlconfig` 相关章节截图/表格，将所有涉及配置开发的描述补充到字段 Description |
| 来源 | `Documents/BusinessAnalysis/厂端授信PRD.docx` 表格 T8（保存逻辑/场景）、T23（表设计） |
| 已更新字段及描述 | `mcs_buyergrade`：客户分类。直销客户分级包括S,A,B,C,I（S/A/B是大客户），I是个人客户，C是其余普通客户。D1-D5代表经销商客户：D1-钻石、D2-铂金、D3-白银、D4-认证、D5-意向。<br>`mcs_creditgrade`：客户等级。数据字典：A0,A1,A2,A3,A4,ALL。ALL代表所有，用于配置无客户等级时的默认系数，以及配置历史基准额度。客户等级标准采用A0-A4（A0最高），评分卡如有信用分则覆盖：A0≥80分、A1≥70分、A2≥60分、A3≥50分、A4＜50分。<br>`mcs_countryname`：历史基准额度。当客户等级=ALL时用于配置历史基准额度，必须大于1000。客户等级非ALL时通常不配置（按客户分类+客户等级维度配置系数和聚合方法）。<br>`mcs_adjust1/2/3`：授信模型计算因子描述（见上一则更新）。<br>`mcs_aggfunc`：聚合方法。对财务能力、历史回款能力、历史基准额度三个模型因子采取的聚合方法，可选MAX（取最大）或MIN（取最小）。 |
| 配置场景（来自 T8 R4） | 场景一：按客户分类+客户等级维度，配置系数1/2/3和聚合方法，不配置历史基准额度。<br>场景二：按客户分类维度，客户等级=ALL，配置历史基准额度。<br>场景三：客户主数据客户等级为空时，可按客户分类维度、客户等级=ALL，配置系数1/2/3和聚合方法作为兜底默认值。 |
| 保存校验（来自 T8 R5） | 1. 客户等级=ALL 时历史基准额度必须大于1000（已用 JS 实现）<br>2. 客户分类+客户等级组合唯一，重复阻断保存（已用 JS 实现） |
| 发现矛盾 | PRD 原文 T8 R5 同时出现两句话："如果客户等级ALL，历史基准额度不允许输入，或放默认值为0" 与 "如果客户等级＝ALL，历史基准额度必须大于1000"。结合场景二和开发计划 V1.1，当前按 "客户等级=ALL 时历史基准额度必填且>1000" 实施。 |
| 执行结果 | ✅ 6 个字段描述已更新，实体 `mcs_fca_mdlconfig` 已发布 |
| 待处理 | PRD 中 `mcs_fca_proc`、`mcs_fca_quota`、`mcs_fca_records`、`mcs_fca_quotaapp` 等实体同样有大量配置/取数逻辑描述，可按同样方式补充到字段 Description |


## 会话更新（2026-07-01）— mcs_fca_mdlconfig 保存校验增强

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 根据 PRD 截图说明，补充「客户等级≠ALL 时，模型系数和聚合方法字段必填」的保存校验 |
| 更新文件 | `Code/Customizations/WebResources/JS/mcs_fca_mdlconfig.js` |
| 新增校验 | `onSave` 中新增 `validateFactorFieldsRequired`：<br>当 `mcs_creditgrade` 不为空且不等于 ALL（值 6）时，校验 `mcs_adjust1`、`mcs_adjust2`、`mcs_adjust3`、`mcs_aggfunc` 是否都有值；任一为空则 `preventDefault()` 并弹窗提示缺失字段名。 |
| 现有校验 | 1. 客户等级=ALL 时历史基准额度必须大于 1000<br>2. 客户分类+客户等级组合唯一 |
| 执行结果 | ✅ JS WebResource 已更新并重新发布，实体 `mcs_fca_mdlconfig` 已发布 |


## 会话更新（2026-07-01）— 通用自动编号配置与文档

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 1. 为 `mcs_fca_proc`（厂端授信模型计算表）配置 `mcs_doid` 自动编号（`FCM + YYYYMMDD + 4位序列号`）<br>2. 将通用自动编号框架整理成文档，方便后续复用 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`) |
| 关键发现 | `mcs_quote_main` 等已有配置均使用 `ms_useserialnoservice = true` + `{$serialno()}`，说明当前环境扩展序列号服务可用；之前 `mcs_fca_mdlversion` 带序列号失败是因为其他原因，不是机制不可用。 |
| MetadataTool 增强 | 1. `create-number-config` 命令新增可选参数 `[使用序列号服务true|false]`，默认 `true`<br>2. `test-number-config` 通用化，新增 `mcs_fca_proc` 测试记录构建逻辑（`mcs_accountid` 实际指向 `mcs_customermasterdata`） |
| 配置结果 | ✅ `mcs_fca_proc.mcs_doid` 自动编号配置已创建：`FCM{$datetimeformat(false,yyyyMMdd)}` + `{$prefix()}{$serialno()}`，序列号长度 4，使用序列号服务 true<br>✅ Plugin Step 已注册：`MSLibrary.D365.Common.Plugins.EntityValidateCreateForGenerateNumber` Create PreValidation of `mcs_fca_proc`（Assembly: `SanyD365.D365Extension`） |
| 验证结果 | ✅ `test-number-config mcs_fca_proc` 成功生成编号 `FCM202607010002`，测试记录已删除 |
| 文档 | ✅ 新增 `Documents/DevelopmentStandards/D365-通用自动编号使用指南.md`，含框架原理、配置字段、模板语法、MetadataTool 命令、配置示例、注意事项、已配置实体清单 |
| 已配置实体清单 | `mcs_fca_mdlversion.mcs_versionid`（`VYYYYMMDD`）<br>`mcs_fca_proc.mcs_doid`（`FCM + YYYYMMDD + 4位序列号`）<br>`mcs_quote_main.mcs_quote_no`（已有，他人配置） |
| 注意事项 | 后续为其他实体配置带序列号编号时，必须确保 `ms_useserialnoservice = true`；发布到 UAT/PROD 需通过 Solution 迁移或重新执行配置命令 |


## 会话更新（2026-07-01）— mcs_fca_proc 字段描述、默认值、视图配置

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 完成 `mcs_fca_proc`（厂端授信模型计算表）的字段描述补充、默认值设置、默认视图配置 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260629_peter` |
| 字段描述策略 | 按“字段含义 + 关键提示”原则更新，不再把完整取数逻辑/校验规则全塞进 Description；详细规则后续落到 JS/Plugin 代码注释中 |
| 已更新字段及描述 | `mcs_doid`：模型计算序列号，系统自动生成。<br>`mcs_accountid`：客户编码。<br>`mcs_custname`：客户名称。<br>`mcs_creditreject`：不予授信场景：1征信黑名单/2近3年≥100万坏账/3逾期账龄≥6个月且逾期金额/在外货款余额>50%/4集团黑名单。<br>`mcs_modelgrant`：第一步模型计算授信额度USD。<br>`mcs_initigrant`：第二步调整后初始化授信金额USD。<br>`mcs_validfrom`：计算日期。<br>`mcs_status`：计算状态：1授信校验/2模型计算/3生效启用/4退回计算。<br>`mcs_versionid`：模型版本。<br>`mcs_modeldesc`：模型描述。<br>`mcs_doproc`：计算日志。<br>`mcs_orgid`/`mcs_orgname`/`mcs_buid`/`mcs_buname`：归属组织/事业部信息。 |
| 默认值设置 | ✅ `mcs_status` = 1（授信校验）已通过 `update-field-default` 设置成功<br>⏸️ `mcs_validfrom`（DateTime）、`mcs_modelgrant`/`mcs_initigrant`（Money）元数据默认值不支持，后续通过 JS 在新建时填充 |
| 视图配置 | 在 `MetadataTool/Program.cs` 的 `update-view` 中新增 `mcs_fca_proc` case；默认视图追加 10 列：`mcs_doid`（模型计算序列号）、`mcs_accountid`（客户编码）、`mcs_custname`（客户名称）、`mcs_status`（计算状态）、`mcs_validfrom`（计算日期）、`mcs_versionid`（模型版本）、`mcs_modeldesc`（模型描述）、`mcs_initigrant`（调整模型额度USD）、`modifiedby`（编辑人）、`modifiedon`（编辑时间） |
| BPF/工作流 | 用户将手动配置 Business Process Flow：授信校验 → 模型计算 → 生效启用；配置完成后我再绑定 JS 事件 |
| 关键业务规则记录 | 授信校验阶段点击【下一步】时：若用户未选场景 3/4，系统自动校验并勾选；一旦判定不予授信，停留在授信校验阶段，并更新客户主数据【不予授信客户】= 1 |
| 执行结果 | ✅ 字段描述已更新<br>✅ `mcs_status` 默认值已设置<br>✅ 默认视图列已更新<br>⏸️ 实体发布因 DEV1 并发 Solution Import 暂时失败，待重试 |
| 相关文件 | `Code/Tools/MetadataTool/Program.cs`（新增 `mcs_fca_proc` update-view 映射） |
| 待完成 | 1. 等 DEV1 Import 结束后重试发布 `mcs_fca_proc`<br>2. 等用户手动配置 BPF 后，编写 `mcs_fca_proc.js`（默认值、保存校验、授信校验下一步逻辑、BPF 事件绑定） |


## 会话更新（2026-07-01）— mcs_fca_proc 表单脚本部署与生效启用视图

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 完成 `mcs_fca_proc`（厂端授信模型计算表）表单脚本编写、部署、BPF 绑定，并补齐「生效启用」视图 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260629_peter` |
| 逾期字段确认 | 通过 `list-fields mcs_outstanding` 确认：<br>- `mcs_overdurationdays`：逾期时长（天）<br>- `mcs_newoverdueamountrmb`：最新逾期金额（人民币）<br>- `mcs_newremainingamountrmb`：最新在外货款金额（人民币）<br>- `mcs_customermaster`：客户主数据 Lookup，可直接按 `_mcs_customermaster_value` 过滤 |
| JS 文件 | `Code/Customizations/WebResources/JS/mcs_fca_proc.js` |
| JS 功能 | 1. **onLoad**：新建时设置 `mcs_status=1`（授信校验）、`mcs_validfrom=当前时间`、`mcs_modelgrant=0`、`mcs_initigrant=0`<br>2. **onSave**：客户编码必填；异步校验重复记录（同客户存在状态≠3 的记录）与模型版本有效性<br>3. **BPF PreStageChange**：<br>   - 授信校验阶段点击「下一步」：若已选手动场景 3/4，直接判定不予授信；否则自动查询 `mcs_outstanding` 逾期数据和 `mcs_customermasterdata` 黑名单，触发则自动勾选场景并回写 `mcs_creditgrant=true`<br>   - 未触发则状态变为 2（模型计算）并推进<br>   - 模型计算阶段点击「下一步」：校验模型版本、额度已生成，状态变为 3（生效启用）<br>   - 生效启用阶段点击「上一步」：状态变为 4（退回计算） |
| 部署结果 | ✅ WebResource `mcs_fca_proc.js` 已更新（14938 bytes）<br>✅ 已绑定到 `mcs_fca_proc` 主窗体 `Information` 的 `onLoad`/`onSave` 及 BPF `PreStageChange`<br>✅ 已加入 Solution `entity_20260629_peter`<br>✅ 实体 `mcs_fca_proc` 已发布 |
| 视图配置 | 新增 Public 视图「生效启用」：复制默认视图列布局，按 `mcs_status = 3` 筛选，已加入 Solution `entity_20260629_peter` 并发布 |
| 工具增强 | `MetadataTool` 新增 `clone-view` 命令：`dotnet run clone-view <实体名> <新视图名> <过滤字段> <过滤值> <解决方案唯一名>`，用于基于默认 Public 视图快速创建带筛选条件的视图 |
| 相关文件 | `Code/Customizations/WebResources/JS/mcs_fca_proc.js`<br>`Code/Tools/MetadataTool/Services/EntityManager.cs`<br>`Code/Tools/MetadataTool/Program.cs` |
| 待完成 | 1. 状态=3（生效启用）时回写 `mcs_fca_quota`、生成 `mcs_fca_records` 台账，由后续/其他 AI 通过 Plugin/Workflow 实现<br>2. 重复记录、模型版本有效期等异步保存校验建议后续加后端 Plugin 兜底 |


## 会话更新（2026-07-01）— mcs_fca_proc 生效启用后端回写实现

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 实现 `mcs_fca_proc`（厂端授信模型计算表）状态=3（生效启用）时回写 `mcs_fca_quota`、生成 `mcs_fca_records` 台账 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260629_peter` |
| PRD 规则 | 生效启用时，把【调整模型额度USD】保存到【厂端授信额度表】的【厂端授信额度USD】和【厂端授信余额USD】；同时生成【厂端授信额度动态调整管理台账表】记录，流程环节=1厂端授信模型计算 |
| 字段/选项集确认 | - `mcs_fca_quota.mcs_isactive`：0=否，1=是<br>- `mcs_fca_records.mcs_proccess`：1=环节1（PRD 对应厂端授信模型计算）<br>- `mcs_fca_records.mcs_adjust`：1=初始化<br>- `mcs_fca_records.mcs_recordid`：PRD 规则 `FCR + YYYYMMDD + 5位序列号` |
| 自动编号配置 | 为 `mcs_fca_records.mcs_recordid` 创建自动编号：`FCR{$datetimeformat(false,yyyyMMdd)}` + `{$prefix()}{$serialno()}`，序列号长度 5，并注册通用自动编号 Plugin Step `MSLibrary.D365.Common.Plugins.EntityValidateCreateForGenerateNumber` |
| Plugin 实现 | 文件：`Code/Customizations/Plugins/FactoryCredit/ProcActivation/FcaProcActivationPlugin.cs`<br>命名空间：`SanyD365.Plugins.FactoryCredit`<br>触发：`mcs_fca_proc` Update PostOperation，筛选属性 `mcs_status`，PreEntityImage `PreImage` 含 `mcs_status`<br>逻辑：<br>1. 校验新状态=3 且旧状态≠3<br>2. 读取 `mcs_accountid`、`mcs_custname`、`mcs_initigrant`、`mcs_validfrom`、`mcs_doid`<br>3. 按客户维度查询 `mcs_fca_quota`，存在则更新，不存在则创建；设置 `mcs_sellergrant`/`mcs_sellerbalance`=`mcs_initigrant`，`mcs_isactive`=1<br>4. 创建 `mcs_fca_records`，`mcs_proccess`=1、`mcs_adjust`=1，`mcs_asisbalance`=0，`mcs_tobebalance`/`mcs_adjustamt`/`mcs_sellergrant`=`mcs_initigrant`，台账编号由自动编号服务生成 |
| DEV1 测试 | ✅ 本地独立 Assembly 注册测试通过：`test-fca-proc-activation` 创建 proc 状态=2 → 更新为状态=3，成功生成 `mcs_fca_quota` 额度记录和 `mcs_fca_records` 台账记录（编号 `FCR2026070100001`）<br>✅ 测试结束后已执行 `unregister-assembly SanyD365.Plugins.FactoryCredit` 注销 Step/Type/Assembly |
| 远程同步 | ✅ 代码已通过 `sync-plugin-to-remote.py` 同步到 `tx-windows` 远程主项目 `C:\Projects\D365\D365\SanyD365.D365Extension.Sales\Plugins\FactoryCredit\FcaProcActivationPlugin.cs`<br>✅ 远程 `msbuild` 编译通过（仅既有 warning，无新增错误）<br>✅ 远程 csproj 已添加 `Plugins\FactoryCredit\FcaProcActivationPlugin.cs` 引用 |
| 相关文件 | `Code/Customizations/Plugins/FactoryCredit/FactoryCreditPlugins.csproj`<br>`Code/Customizations/Plugins/FactoryCredit/ProcActivation/FcaProcActivationPlugin.cs`<br>`Code/Tools/sync-plugin-to-remote.py`<br>`Code/Tools/MetadataTool/Program.cs`（新增 `test-fca-proc-activation` 测试命令） |
| 待完成 | 1. 在远程服务器 `tx-windows` 推 Git 分支、建 PR、合并到 `uat`<br>2. 更新 DEV1 主 Assembly `SanyD365.D365Extension` 并注册 `FcaProcActivationPlugin` Step + PreEntityImage<br>3. `mcs_fca_quota.mcs_quotano` 额度编码规则 PRD 未明确，当前留空；如需编码需后续确认规则 |

## 会话更新（2026-07-02）— 内部历史交易指标 SalesAmount/ARAmount/ARAge 取值修复

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-02 |
| 目标 | 修复 `CofaceDataSyncPlugin` 中内部历史交易指标 `SalesAmount`/`ARAmount`/`ARAge` 取值后未写入客户信用标签的问题 |
| 问题根因 | `CofaceToD365Mapping` 字典中缺少 `SalesAmount`/`ARAmount`/`ARAge` 三个编码的映射，导致 `GetCofaceValue()` 永远返回 `null`，标签无法写入 |
| 修复文件 | `Code/Customizations/Plugins/CofaceIntegration/Plugin/CofaceDataSyncPlugin.cs` |
| 修改内容 | 在 `CofaceToD365Mapping` 中新增三个 identity 映射：`["SalesAmount"]="SalesAmount"`、`["ARAmount"]="ARAmount"`、`["ARAge"]="ARAge"` |
| Git 工作流 | 远程 `tx-windows` 创建分支 `uat-20260701-peter-fix-internal-historical-mapping` → push → 用户 Azure DevOps PR 合并到 `uat`（commit `5db3d24717`） |
| 远程编译 | ✅ `msbuild SanyD365.D365Extension.Sales.csproj` 通过（仅项目原有 warning） |
| DEV1 Assembly | ✅ 用合并后 `uat` 编译的 DLL 更新 `SanyD365.D365Extension.Sales`（ID: `9d6ff315-8c03-4d51-b641-ebeccf9e98b0`） |
| DEV1 测试数据 | ✅ 客户 `LTC客户-1` 已有 2 条销售订单（USD 50,000/条）和 1 条逾期在外货款（CNY 350,000，逾期 120 天） |
| DEV1 验证记录 | `SCO202607020006`（状态 13） |
| 验证结果 | • 历史采购金额：`100,000.00` ✅（预期 100,000 USD）<br>• 历史逾期金额：`49,978.22` ✅（预期 350,000 CNY ÷ 7.00305 ≈ 49,978 USD）<br>• 历史逾期账龄：`120.00` ✅（预期 120 天） |
| OverdueModel | 当前为人工复核输入项（表单字段 `mcs_overduerate` 在状态 12 开放编辑），非 `CofaceDataSyncPlugin` 自动取值；昨晚 `SCO202607010007` 的 30 分为人工输入的复核值 |
| 工具增强 | `MetadataTool` 新增命令：<br>• `create-credit-record <客户名称>`：为客户创建新的信用评估记录<br>• `set-cofaceid <评估编码> <cofaceId>`：设置评估记录的科法斯客户代码<br>• `diagnose-credit-record` 输出增加标签复核值字段 |
| 下一步 | 如需 OverdueModel 自动取值，需业务确认规则（从 `account.mcs_overduemodel` / `mcs_credit_record.mcs_overduerate` 读取，或按 ARAmount/ARAge 计算） |


## 会话更新（2026-07-01）— 厂端授信额度调整申请阶段 3.1/3.2/3.3

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 完成厂端授信额度调整申请单表 `mcs_fca_quotaapp` 的 3.1 表单/视图完善、3.2 初始化带数逻辑、3.3 调整后余额自动计算 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260629_peter` |
| 不涉及 | 申请编号 `mcs_grantid` 自动编号（用户要求自己配置） |
| 新增文件 | `Code/Customizations/WebResources/JS/mcs_fca_quotaapp.js` |
| JS 功能 | 1. **onLoad**：新建记录时默认审批状态=申请（1）、调整额度=0、调整后余额=0；注册客户编码/模型计算序列号/调整额度字段变更事件<br>2. **客户编码变更**：异步查询 `mcs_customermasterdata` 带出客户名称、客户等级（A0-A4）；查询 `mcs_fca_quota` 带出当前厂端授信额度和余额；清空模型计算序列号和模型计算额度；重新计算调整后余额<br>3. **模型计算序列号变更**：查询 `mcs_fca_proc` 带出 `mcs_initigrant`；校验所选计算记录的客户是否与当前申请单客户一致，不一致则阻断并清空<br>4. **调整额度变更**：自动计算 `mcs_tobebalance = mcs_tobegrant - mcs_sellergrant + mcs_sellerbalance`<br>5. **onSave**：客户编码必填、调整额度必填且≥0、账期（天）必须是30的倍数、当前额度为0时必须选择模型计算序列号 |
| 视图配置 | `MetadataTool/Program.cs` 新增 `mcs_fca_quotaapp` 默认视图列映射：申请编号、客户编码、客户名称、客户等级、审批状态、调整厂端授信额度、调整后厂端授信余额、厂端授信额度USD、厂端授信余额USD、申请人、申请日期 |
| 工具增强 | `MetadataTool/Services/EntityManager.cs` 的 `BindJsToForm` 增加 `mcs_fca_quotaapp` → `FcaQuotaAppForm` 映射 |
| DEV1 部署 | ✅ WebResource `mcs_fca_quotaapp.js` 已部署并发布<br>✅ 已绑定到 `mcs_fca_quotaapp` 主窗体 `Information` 的 `onLoad` 和 `onSave` 事件<br>✅ 已加入 Solution `entity_20260629_peter`<br>✅ 实体 `mcs_fca_quotaapp` 默认视图已更新并发布 |
| 待验证 | 在 DEV1 UI 中新建 `mcs_fca_quotaapp` 记录：选择已有额度的客户，确认客户名称/等级/额度/余额自动带出；选择模型计算序列号，确认模型计算额度自动带出；修改调整额度，确认调整后余额按公式实时计算 |
| DEV1 测试数据 | ✅ 已为 `LTC客户-1` 创建 `mcs_fca_quota` 额度测试记录（ID: `7a446e61-fa78-f111-ab0e-7ced8de4edac`）<br>• 客户主数据 ID: `edff63c8-325a-f111-a824-6045bd1bc963`<br>• 厂端授信额度 USD: `100,000`<br>• 厂端授信余额 USD: `80,000`<br>• 是否生效: `是`<br>• 模型计算序列号: `TEST202607010001` |
| 工具增强 | `MetadataTool/Program.cs` 新增命令 `create-fca-quota-testdata <客户名称>`，用于为客户创建厂端授信额度测试数据 |
| 下一步 | 1. 用户在 DEV1 UI 验证上述场景<br>2. 后续任务（3.4 附件 Uploader、3.5 BPP 提交、3.6 BPP 回调等）等待用户通知 |


## 会话更新（2026-07-01）— 修复 `mcs_fca_quotaapp` / `mcs_fca_proc` 表单打开报错

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 修复 `mcs_fca_quotaapp`、`mcs_fca_proc` 主窗体打开报错的问题 |
| 根因 | 1. 窗体 XML 中绑定的 JS handler 命名空间错误：`mcs_fca_quotaapp.js` 被绑定到 `ScoringCardForm.onLoad/onSave`，`mcs_fca_proc.js` 同样被绑定到 `ScoringCardForm.onLoad/onSave`，而这两个 JS 文件实际命名空间分别是 `FcaQuotaAppForm` 和 `FcaProcForm`，导致 D365 找不到 `ScoringCardForm` 对象而报错。<br>2. `MetadataTool.BindJsToForm` 命令存在缺陷：多次执行或切换命名空间时未清理旧的 handler，也未去重 `Library` 和 `event`，导致窗体 XML 中同时存在错误命名空间 handler、重复 library 和重复 event。 |
| 已修复 | 1. 导出两个实体的主窗体 XML，清理后保留唯一正确的 handler：<br>   • `mcs_fca_quotaapp`：仅保留 `FcaQuotaAppForm.onLoad` / `FcaQuotaAppForm.onSave`<br>   • `mcs_fca_proc`：仅保留 `FcaProcForm.onLoad` / `FcaProcForm.onSave`<br>2. 删除错误的 `ScoringCardForm` handler 和重复的 `Library` / `event` 元素。<br>3. 使用 `update-form-xml` 命令将清理后的 XML 写回 D365，并执行 `publish` 发布两个实体。 |
| 验证 | 重新导出两个实体的 formxml 确认：<br>• `mcs_fca_quotaapp`：只剩 1 个 `mcs_fca_quotaapp.js` Library 和 2 个 `FcaQuotaAppForm` handler<br>• `mcs_fca_proc`：只剩 1 个 `mcs_fca_proc.js` Library 和 2 个 `FcaProcForm` handler |
| 待验证 | 用户在 DEV1 UI 中打开 `mcs_fca_quotaapp` 和 `mcs_fca_proc` 主窗体，确认不再报错。 |
| 工具改进（已授权并修复） | 已修改 `Code/Tools/MetadataTool/Services/EntityManager.cs` 中 `BindJsToForm` 的清理逻辑：<br>1. 判断 Library 存在时使用大小写不敏感匹配，避免 `<Library name=...>` 被误判为不存在而重复添加。<br>2. 新增 `EnsureSingleLibrary`：清理重复的 `<Library>`，只保留一个。<br>3. 新增 `NormalizeFormEvents`：整体重建 `<events>`，保留其他 WebResource 的 handler，仅保留目标 WebResource 的正确命名空间 handler，删除错误命名空间和重复 event。<br>4. 编译通过，重复执行 `bind-js` 命令会正确输出 `⊘ JS已绑定，跳过`，不再产生重复 handler。 |
| 相关文件 | `Code/Tools/MetadataTool/Services/EntityManager.cs`<br>`Code/Customizations/WebResources/JS/mcs_fca_quotaapp.js`<br>`Code/Customizations/WebResources/JS/mcs_fca_proc.js` |


## 会话更新（2026-07-01）— mcs_fca_quotaapp 自动编号配置

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 为 `mcs_fca_quotaapp`（厂端授信额度申请单表）配置 `mcs_grantid` 自动编号（`FCA + YYYYMMDD + 4位序列号`） |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`) |
| 字段确认 | `mcs_fca_quotaapp.mcs_grantid`（申请编号，String，必填） |
| MetadataTool 增强 | `test-number-config` 新增 `mcs_fca_quotaapp` 测试记录构建：动态查询 `mcs_typename` 的 Lookup 目标实体并取第一条记录；其他 Money/Integer 必填字段填充测试值 |
| 配置结果 | ✅ 自动编号配置已创建：`FCA{$datetimeformat(false,yyyyMMdd)}` + `{$prefix()}{$serialno()}`，序列号长度 4，使用序列号服务 true<br>✅ Plugin Step 已注册：`EntityValidateCreateForGenerateNumber` Create PreValidation of `mcs_fca_quotaapp` |
| 验证结果 | ✅ `test-number-config mcs_fca_quotaapp` 成功生成编号 `FCA202607060001`，测试记录已删除 |
| 文档更新 | ✅ `Documents/DevelopmentStandards/D365-通用自动编号使用指南.md` 已更新已配置实体清单 |


## 会话更新（2026-07-01）— 重建 `mcs_fca_proc.mcs_creditreject` 字段

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 修复 `mcs_fca_proc` 表单上「不予授信客户」下拉值显示不出来的问题 |
| 根因 | 字段 `mcs_creditreject`（多选选项集）之前创建时类型/选项集关联异常，list-fields 显示为 `Virtual` 类型，下拉选项无法正常显示。 |
| 已执行 | 1. 从视图和主窗体移除字段（视图 0 个，窗体中不存在，均跳过）。<br>2. 删除字段 `mcs_fca_proc.mcs_creditreject`。<br>3. 使用 `MetadataTool create Definitions/mcs_fca_proc.json` 重新创建缺失字段（公共方法 `CreateMultiSelectPicklistField`），选项集值：场景1/2/3/4。<br>4. 更新字段显示名称：中文 `不予授信客户`，英文 `Credit Rejected Customer`。<br>5. 发布实体 `mcs_fca_proc`。 |
| 验证 | `list-fields` 确认字段已重建；`query-optionset` 确认选项集值正常：<br>• 值 1 → 场景1<br>• 值 2 → 场景2<br>• 值 3 → 场景3<br>• 值 4 → 场景4 |
| 待完成 | 用户自行在 `mcs_fca_proc` 主窗体中重新放置 `mcs_creditreject` 字段。 |
| 相关文件 | `Code/Tools/MetadataTool/Definitions/mcs_fca_proc.json`<br>`Code/Tools/MetadataTool/Services/EntityManager.cs`<br>`Code/Customizations/WebResources/JS/mcs_fca_proc.js`<br>`Code/Customizations/Plugins/FactoryCredit/Calculation/Services/CreditRejectCheckService.cs` |


## 会话更新（2026-07-01）— 更新 `mcs_creditreject` 选项集标签

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 将 `mcs_fca_proc.mcs_creditreject` 的选项标签从「场景1/2/3/4」改为业务描述文本 |
| 新增公共方法 | `D365ToolCommon.Metadata.MetadataFieldService.UpdateOptionSetLabels(entityName, fieldLogicalName, labels, languageCode)` — 更新局部选项集字段的选项标签，保留其他语言标签。 |
| 新增 CLI 命令 | `dotnet run update-optionset-labels <实体名> <字段名> <labels.json> [langId]` |
| 标签内容 | • 值 1 → `客户被列入征信黑名单`<br>• 值 2 → `近3年与我司产生>100万元人民币实质性坏账`<br>• 值 3 → `实际逾期账龄≥6个月且（逾期金额/在外货款余额）>50%`<br>• 值 4 → `集团黑名单客户` |
| DEV1 部署 | ✅ 已更新 `mcs_fca_proc.mcs_creditreject` 的中文（2052）选项标签<br>✅ 已发布实体 `mcs_fca_proc`<br>✅ `query-optionset` 验证标签已生效 |
| 相关文件 | `Code/Tools/D365ToolCommon/Metadata/MetadataFieldService.cs`<br>`Code/Tools/MetadataTool/Services/EntityManager.cs`<br>`Code/Tools/MetadataTool/Program.cs` |


## 会话更新（2026-07-01）— mcs_fca_quotaapp 自动编号配置同步到 UAT

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-01 |
| 目标 | 将 `mcs_fca_quotaapp.mcs_grantid` 的自动编号规则同步到 UAT，仅创建配置数据，不注册 Plugin Step |
| 环境 | UAT (`https://sany-uat.crm5.dynamics.com`) |
| 执行前检查 | UAT 上未找到 `mcs_fca_quotaapp` 的自动编号配置 |
| 执行命令 | `D365_URL=https://sany-uat.crm5.dynamics.com dotnet run -- create-number-config mcs_fca_quotaapp mcs_grantid 'FCA{$datetimeformat(false,yyyyMMdd)}' '{$prefix()}{$serialno()}' 4 1 true` |
| 执行结果 | ✅ UAT 上已创建自动编号配置，配置 ID: `9d73dac3-0879-f111-ab0e-7ced8de4a9bc`<br>✅ 未注册 Plugin Step（按用户要求） |
| 注意事项 | UAT 上需确保已存在通用自动编号 Plugin Step `EntityValidateCreateForGenerateNumber: Create of mcs_fca_quotaapp`，否则编号不会自动生成 |


## 会话更新（2026-07-07）— 厂端授信 FcaProcCalculationPlugin 归并主 Assembly 并 DEV1 端到端验证

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-07 |
| 目标 | 将本地独立开发的厂端授信模型计算 Plugin `FcaProcCalculationPlugin` 归并到远程主项目 `SanyD365.D365Extension.Sales`，更新 DEV1 主 Assembly，完成端到端验证 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，远程服务器 `tx-windows` (`C:\Projects\D365\D365\SanyD365.D365Extension.Sales`) |
| 涉及代码 | `Code/Customizations/Plugins/FactoryCredit/Calculation/FcaProcCalculationPlugin.cs`<br>`Code/Customizations/Plugins/FactoryCredit/Calculation/Services/*.cs`（10 个服务类）<br>`Code/Tools/sync-plugin-to-remote.py` |
| 同步工作 | ✅ 扩展 `sync-plugin-to-remote.py`：新增 `FactoryCredit` 命名空间映射、`FcaProcCalculationPlugin` 及 10 个 Service 类文件映射、IPlugin → PluginBase 转换分支<br>✅ 同步到 `tx-windows` 远程主项目 `Plugins\FactoryCredit\` 目录<br>✅ 远程 `SanyD365.D365Extension.Sales.csproj` 自动更新并编译通过（0 错误） |
| Git 工作流 | ✅ 远程 `tx-windows` 拉取最新 `uat`，创建分支 `uat-20260707-peter-fca-calculation` 并 push<br>✅ 用户合并 PR 到 `uat`<br>✅ 拉取合并后的 `uat` 重新编译成功 |
| DEV1 部署 | ✅ 主 Assembly `SanyD365.D365Extension.Sales` 已更新（ID: `9d6ff315-8c03-4d51-b641-ebeccf9e98b0`，modified=`2026/7/6 23:31:08`） |
| Plugin Step 注册 | ✅ 初始注册时类名写错（漏了 `.Calculation` 命名空间），修正为完整类名后重新注册：<br>`SanyD365.D365Extension.Sales.Plugins.FactoryCredit.Calculation.FcaProcCalculationPlugin`<br>✅ Step: `Update of mcs_fca_proc`，PostOperation/Sync，Filter=`mcs_status`（Step ID: `07192e40-9379-f111-ab0e-7ced8db4dda8`）<br>✅ PreEntityImage: `PreImage` / Alias=`PreImage`，字段=`mcs_status`（Image ID: `4876044a-9379-f111-ab0e-7ced8db4d7a7`） |
| DEV1 端到端验证 | ✅ 测试客户：五强集团（`mcs_customermasterdata`: `e8b28b63-2b8a-4516-9a79-e068efe481f0`）<br>✅ 为 C/ALL 客户分类补测试模型参数（`mcs_fca_mdlconfig`: `620d76fc-9379-f111-ab0e-7ced8db4d37f`），历史基准额度 USD 1,000,000<br>✅ 创建 `mcs_fca_proc` → 状态 1→2 → 模型计算成功：<br>   • 模型计算额度 USD: `1,000,000.00`<br>   • 调整模型额度 USD: `1,000,000.00`<br>   • 计算日志完整生成<br>✅ 状态 2→3 → 生效启用成功：<br>   • `mcs_fca_quota` 额度记录（ID: `8c0c1a03-9479-f111-ab0e-7ced8db4d37f`）：卖方额度/余额均为 1,000,000 USD，已生效<br>   • `mcs_fca_records` 初始化台账（ID: `8d0c1a03-9479-f111-ab0e-7ced8db4d37f`）：环节=1（厂端授信模型计算），调整类型=1（初始化），调整后余额=1,000,000 USD |
| 遗留测试数据 | 本次验证留下以下数据，未清理：<br>• `mcs_fca_mdlconfig` C/ALL 测试参数<br>• `mcs_fca_proc` 验证记录（ID: `630d76fc-9379-f111-ab0e-7ced8db4d37f`）<br>• `mcs_fca_quota` 额度记录<br>• `mcs_fca_records` 台账记录 |
| UAT 发布 | ✅ 已通过 n8n Release Tool 发布 `McsPlugin` 到 UAT（仅勾选 Plugin Assembly/Step，未勾选实体） |
| 状态 | ✅ `FcaProcCalculationPlugin` 已完成并合并到 `uat`，DEV1 主 Assembly 验证通过，UAT 已发布 |
| 待确认 | `mcs_fca_quota.mcs_quotano`（额度编码）是否需自动编号规则；PRD 未明确，待业务确认 |
| 下一步 | 如需 UAT 端到端复测，可创建测试 `mcs_fca_proc` 记录验证；额度编码规则待业务确认 |


## 会话更新（2026-07-07）— `FcaProcCalculationPlugin` UAT 发布完成

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-07 |
| 目标 | 将厂端授信模型计算 Plugin `FcaProcCalculationPlugin` 随 `McsPlugin` 发布到 UAT |
| 发布方式 | n8n Release Tool |
| 勾选内容 | `McsPlugin` 解决方案中的 Plugin Assembly / Plugin Step |
| 未勾选内容 | 实体/字段/WebResource（厂端授信实体已在 UAT 存在，无需重复发布） |
| 状态 | ✅ UAT 发布完成 |
| 待验证 | 如需确认 UAT 行为一致，可创建测试 `mcs_fca_proc` 记录执行端到端验证 |
| 待确认 | `mcs_fca_quota.mcs_quotano`（额度编码）自动编号规则，PRD 未明确 |


## 会话更新（2026-07-07）— `mcs_fca_quotaapp` 信保额度类型字段改造

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-07 |
| 目标 | 按 PRD 要求，将 `mcs_fca_quotaapp.mcs_applygenre`（信保额度类型）从单选 Picklist 改为 String，以支持从 `mcs_approvedquota` 读取并逗号组合显示 |
| PRD 依据 | 开发计划 4.6 节定义 `mcs_applygenre` 为文本类型；PRD 截图要求信保额度类型"按中信保买方代码匹配 CRM 客户主数据的客户编码查询"，且"同时存在非信用证和信用证场景时采用逗号组合显示" |
| 涉及文件 | `Code/Customizations/WebResources/JS/mcs_fca_quotaapp.js`<br>`Code/Tools/MetadataTool/Definitions/mcs_fca_quotaapp.json`<br>`Code/Tools/MetadataTool/Program.cs` |
| 已执行操作 | 1. 从 `mcs_fca_quotaapp` 主窗体移除原 `mcs_applygenre` 字段（`remove-fields-from-form`）<br>2. 从视图移除原 `mcs_applygenre` 字段（`remove-field-from-views`，实际视图中已无引用）<br>3. 删除原 Picklist 字段 `mcs_applygenre`<br>4. 新建 String 字段 `mcs_applygenre`（长度 20，显示名"信保额度类型"）<br>5. 更新 `mcs_fca_quotaapp.js`：`retrieveSinosureQuota` 中按 PRD 规则映射额度类型（1,2,3,7→非信用证；其余→信用证），同时存在时逗号组合<br>6. 更新 JSON 定义和 `MetadataTool` 的 `AddFieldsToEntity` / `RearrangeForm` 逻辑，避免后续把 `mcs_applygenre` 误判为 Picklist |
| DEV1 状态 | ✅ 字段重建完成，类型为 String<br>✅ `mcs_fca_quotaapp.js` 已部署并发布<br>✅ 实体 `mcs_fca_quotaapp` 已发布 |
| 待用户操作 | `mcs_applygenre` 字段已从主窗体和视图中移除，需要用户在 D365 UI 中重新放置到表单和视图 |
| 测试数据 | ✅ 已为 `LTC客户-1` 创建中信保限额测试数据：<br>• 客户主数据 `mcs_sinosurecode` = `TESTBUYER20260707084837`<br>• `mcs_approvedquota` 有效记录 3 条：<br>  - 非信用证（DP）: 额度 50,000 USD / 余额 45,000 USD<br>  - 信用证（OA）: 额度 80,000 USD / 余额 70,000 USD<br>  - 信用证（LC）: 额度 100,000 USD / 余额 90,000 USD<br>• 预期带出：信保额度类型=`非信用证,信用证` / 信保额度 USD=`230,000` / 信保余额 USD=`205,000` |
| 待验证 | 在 DEV1 新建 `mcs_fca_quotaapp` 记录，选择 `LTC客户-1`，确认信保额度类型/额度/余额自动带出，且逗号组合显示正常 |


## 会话更新（2026-07-07）— 找到 Active Layer 可靠检测方式并确认 UAT 3 个关键 WebResource 无 Active Layer

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-07 |
| 目标 | 1. 寻找可编程判断 D365 托管 Solution 组件是否存在 Active Layer 的可靠方式；2. 确认 UAT 上 `mcs_credit_profile.html`、`mcs_credit_wheel.html`、`mcs_credit_record.js` 已删除 Active Layer |
| 环境 | UAT (`https://sany-uat.crm5.dynamics.com`) |
| 官方方案 | **Microsoft Dataverse 虚拟实体 `msdyn_componentlayer`**：查询条件 `msdyn_solutionname = "Active"` + `msdyn_solutioncomponentname = "WebResource"` + `msdyn_componentid = "<WebResource ID>"`，解析 `msdyn_changes` JSON 判断是否存在有效变更属性。参考 Microsoft Docs [Component Layer (msdyn_componentlayer) table/entity reference](https://learn.microsoft.com/power-apps/developer/data-platform/reference/entities/msdyn_componentlayer) 及 PowerDataOps `Test-XrmComponentCustomization`。 |
| MetadataTool 增强 | 新增命令 `dotnet run check-webresource-active-layer <WebResource名称>`，可批量/单独检测 WebResource 的 Active Layer。 |
| UAT 检测结果 | ✅ `mcs_credit_profile.html`（ID: `b5ceb8cc-ac63-f111-ab0d-7ced8de5bbd1`）：未找到 Active Layer<br>✅ `mcs_credit_wheel.html`（ID: `920eb5d4-ac63-f111-ab0d-000d3aa33079`）：未找到 Active Layer<br>✅ `mcs_credit_record.js`（ID: `df109d68-d55f-f111-a826-000d3aa33079`）：未找到 Active Layer<br>⚠️ `mcs_credititem_value.js`（ID: `05346b6d-2662-f111-a826-000d3aa33d1b`）：存在 Active Layer，变更属性 `webresourceidunique`、`contentfileref` |
| 文档更新 | ✅ `Documents/DevelopmentStandards/Peter-组件-Solution-归属清单.md`：更新 Active Layer 检查说明，3 个关键 WebResource 状态改为「已确认无 Active Layer」。 |
| 待完成 | 等待用户通过 n8n 发布 `McsWebResource`，发布版本号必须高于当前 UAT 的 `1.6.0.921`；发布完成后强制刷新并验证 UAT 页面/JS 逻辑。 |


## 会话更新（2026-07-07）— 检查其余 5 个 WebResource 的 UAT Active Layer

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-07 |
| 目标 | 检测其余 5 个 WebResource 在 UAT 是否存在 Active Layer |
| 环境 | UAT (`https://sany-uat.crm5.dynamics.com`) |
| 检测命令 | `dotnet run check-webresource-active-layer <WebResource名称>` |
| 结果 | ✅ `mcs_credit_items.js`（ID: `ab56a07d-2162-f111-a826-7ced8de5bbd1`）：无 Active Layer<br>✅ `mcs_credit_scoringcard.js`（ID: `a3bfe3ae-cd5f-f111-a826-000d3aa333b3`）：无 Active Layer<br>⚠️ `mcs_credititem_value.js`（ID: `05346b6d-2662-f111-a826-000d3aa33d1b`）：**存在 Active Layer**，变更属性 `webresourceidunique`、`contentfileref`<br>✅ `mcs_credit_wheel_echarts.js`（ID: `b331c4f1-8d68-f111-ab0c-7ced8db4d37f`）：无 Active Layer<br>✅ `mcs_credit_wheel_vue.js`（ID: `7272fcea-8d68-f111-ab0c-7ced8de4efcf`）：无 Active Layer |
| MetadataTool 修复 | 修复 `msdyn_componentid` 返回 Guid 而非 String 时的转换错误；修复 `msdyn_changes` 中 `Attributes` 为 `[{"Key":"...","Value":"..."}]` 数组格式时的解析逻辑。 |
| 文档更新 | ✅ `Documents/DevelopmentStandards/Peter-组件-Solution-归属清单.md` 已更新各 WebResource 的 Active Layer 状态。 |
| 待处理 | `mcs_credititem_value.js` 发版前需在 D365 Maker Portal【解决方案层】删除 Active Layer。 |


## 会话更新（2026-07-07）— 复测 `mcs_credititem_value.js` Active Layer 已删除

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-07 |
| 目标 | 确认用户删除 `mcs_credititem_value.js` 的 Active Layer 后是否仍存在 |
| 环境 | UAT (`https://sany-uat.crm5.dynamics.com`) |
| 检测命令 | `D365_URL=https://sany-uat.crm5.dynamics.com dotnet run --no-build check-webresource-active-layer mcs_credititem_value.js` |
| 结果 | ✅ `mcs_credititem_value.js`（ID: `05346b6d-2662-f111-a826-000d3aa33d1b`）：未找到 Active Layer |
| 状态 | 全部 8 个 Peter 负责 WebResource 在 UAT 均已确认无 Active Layer，满足 `McsWebResource` 发版前置条件 |
| 文档更新 | ✅ `Documents/DevelopmentStandards/Peter-组件-Solution-归属清单.md` 已更新为最终状态 |


## 会话更新（2026-07-08）— 厂端授信额度调整申请 BPP 审核功能 DEV1 验证通过

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-08 |
| 目标 | 完成厂端授信额度调整申请单 `mcs_fca_quotaapp` 的 BPP 提交与回调处理功能，并在 DEV1 验证通过 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，独立临时 Assembly `SanyD365.Plugins.FactoryCredit` |
| 新增/修改文件 | `Code/Customizations/Plugins/FactoryCredit/Bpp/FcaQuotaAppBppIntegrationPlugin.cs`<br>`Code/Customizations/Plugins/FactoryCredit/Bpp/FcaQuotaAppBppCallbackPlugin.cs`<br>`Code/Customizations/Plugins/FactoryCredit/Bpp/Services/QuotaActivationService.cs`<br>`Code/Customizations/Plugins/FactoryCredit/Bpp/Services/QuotaRecordService.cs`<br>`Code/Customizations/WebResources/JS/mcs_fca_quotaapp.js`<br>`Code/Tools/sync-plugin-to-remote.py`（新增 Bpp 文件映射） |
| DEV1 注册状态 | ✅ `FcaQuotaAppBppIntegrationPlugin`: Update of `mcs_fca_quotaapp`, PostOperation, Sync, Filter=`mcs_bppstatus`, PreEntityImage `PreImage`（字段 `mcs_bppstatus`）<br>✅ `FcaQuotaAppBppCallbackPlugin`: Update of `mcs_fca_quotaapp`, PostOperation, Sync, Filter=`mcs_bppstatuscode` |
| 本地编译 | ✅ `FactoryCreditPlugins.csproj` 0 警告 0 错误 |
| DEV1 验证场景 | **提交审批**：更新 `mcs_bppstatus=2` → Plugin 调用 `mcs_bppstartapi` → `mcs_bppstatuscode` 自动变为 `Submitted` ✅<br>**审批通过**：更新 `mcs_bppstatuscode=Approved` → `mcs_bppstatus` 变为 `3` → 更新 `mcs_fca_quota` 额度表 → 创建 `mcs_fca_records` 台账 ✅<br>**审批驳回**：更新 `mcs_bppstatuscode=Rejected` → `mcs_bppstatus` 变为 `4` ✅ |
| 验证数据 | 客户 `LTC客户-1`（`edff63c8-325a-f111-a824-6045bd1bc963`）：<br>• 调整前额度 100,000 USD / 余额 80,000 USD<br>• 调整后额度 120,000 USD / 余额 100,000 USD<br>• 台账编号 `FCR2026070800001` / `FCR2026070800002`，环节=2（额度调整申请），调整类型=1（初始化） |
| 修复问题 | 1. `FcaQuotaAppBppCallbackPlugin` 中 `mcs_doid` 在 `mcs_fca_quotaapp` 为 Lookup 类型，直接 `GetAttributeValue<string>` 抛 `InvalidCastException`；已改为先取 `EntityReference`，再查询 `mcs_fca_proc.mcs_doid` 字符串序列号<br>2. `QuotaActivationService.GetOrCreateQuota` 查询未排序，可能取到旧测试记录；已增加 `createdon` 降序，且更新时始终回填 `mcs_accountid` 与 `mcs_custname` |
| 待确认事项 | 1. BPP 团队回调字段：当前按 `mcs_bppstatuscode`（String）监听；如 BPP 实际写 `mcs_bppstatus`（Picklist），需调整回调 Plugin 的 Step Filter 与状态解析<br>2. 台账动作类型 `mcs_fca_records.mcs_adjust`：当前用 `1`（初始化），业务上额度调整申请通过是否应改为 `4`（释放）或其他值，需业务确认<br>3. `mcs_fca_quotaapp` 表单上【提交审批】App Action 按钮尚未创建；创建后需将 JS 函数设为 `FcaQuotaAppForm.submitToBpp`，参数 `[{"type":5}]` |
| 下一步 | 1. 用户创建【提交审批】按钮并协调 BPP 团队配置流程<br>2. 同步代码到远程主项目 `SanyD365.D365Extension.Sales` 并走 PR（需用户在 `tx-windows` 上推送/合并）<br>3. 主 Assembly 更新后注销 DEV1 临时 Assembly `SanyD365.Plugins.FactoryCredit`<br>4. 确认回调字段与台账动作类型后做最终端到端验证 |


## 会话更新（2026-07-08）— 厂端授信 BPP 代码已同步远程并推送分支

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-08 |
| 操作 | 将 `FactoryCredit/Bpp` 代码同步到 `tx-windows` 远程主项目，基于最新 `origin/uat` 编译并推送分支 |
| 远程服务器 | `tx-windows` (`C:\Projects\D365\D365\SanyD365.D365Extension.Sales`) |
| 同步命令 | `python3 Code/Tools/sync-plugin-to-remote.py --pull-dll Code/Customizations/Plugins/FactoryCredit/bin/Release/net462/SanyD365.Plugins.FactoryCredit.dll` |
| 同步结果 | ✅ 4 个 Bpp 文件 + `SanyD365.D365Extension.Sales.csproj` 已同步到远程<br>✅ 远程首次编译通过（仅项目原有 warning，无新增错误） |
| 拉取最新代码 | ✅ `git fetch origin` 确认 `origin/uat` 有更新（`11fa5652b0..d1d60c5e27`）<br>✅ 使用 `git stash` + `git checkout -b uat-20260708-peter-fca-bpp origin/uat` + `git stash pop` 基于最新 uat 恢复改动 |
| 二次编译 | ✅ `msbuild SanyD365.D365Extension.Sales.csproj /p:Configuration=Release /p:Platform=AnyCPU /clp:ErrorsOnly` 通过，无错误 |
| Git 提交 | ✅ Commit `e6b5fac77a` — `feat(factorycredit): 厂端授信额度调整申请BPP提交与回调处理`<br>✅ 分支 `uat-20260708-peter-fca-bpp` 已推送到 Azure DevOps (`https://dev.azure.com/SanyGlobalCRM/D365/_git/D365`) |
| 已清理 | 已重置工作目录中之前残留的非本次 Coface/CustomerTag/FactoryCredit 行尾符改动，避免混入本次 PR |
| 下一步 | 用户在 Azure DevOps 创建 PR 并合并到 `uat`；合并后需在 `tx-windows` 拉取最新 `uat` 重新编译并更新 DEV1 主 Assembly `SanyD365.D365Extension.Sales` |


## 会话更新（2026-07-08）— 主 Assembly 已更新并验证通过

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-08 |
| 操作 | PR 合并后，在 `tx-windows` 拉取最新 `uat` 编译，更新 DEV1 主 Assembly `SanyD365.D365Extension.Sales`，注册 BPP Plugin Steps |
| PR 合并 | ✅ Azure DevOps PR #4938 已合并到 `uat`（commit `1726153be0`） |
| 远程编译 | ✅ `tx-windows` 拉取最新 `uat` 后 `msbuild` 通过，无错误 |
| DEV1 主 Assembly | ✅ 已更新 `SanyD365.D365Extension.Sales`（ID: `9d6ff315-8c03-4d51-b641-ebeccf9e98b0`） |
| Plugin Step 注册 | ✅ `FcaQuotaAppBppIntegrationPlugin`: Update of `mcs_fca_quotaapp`, PostOperation/Sync, Filter=`mcs_bppstatus`, PreEntityImage `PreImage`（ID: `9906fff0-717a-f111-ab0e-6045bd1c0925`）<br>✅ `FcaQuotaAppBppCallbackPlugin`: Update of `mcs_fca_quotaapp`, PostOperation/Sync, Filter=`mcs_bppstatuscode` |
| 临时 Assembly 清理 | ✅ 已注销 DEV1 独立 Assembly `SanyD365.Plugins.FactoryCredit`（Step/Type/Assembly） |
| DEV1 端到端验证 | ✅ 记录 `42f237c8-fd78-f111-ab0e-7ced8de4edac`：<br>• `mcs_bppstatus=2` → `mcs_bppstatuscode=Submitted`<br>• `mcs_bppstatuscode=Approved` → `mcs_bppstatus=3`，额度表更新，台账新增 `FCR2026070800003` |
| 状态 | 厂端授信额度调整申请 BPP 审核功能已完整归并到主 Assembly，DEV1 验证通过 |
| 下一步 | 1. 用户在 D365 UI 创建【提交审批】App Action 按钮<br>2. 协调 BPP 团队配置流程，确认回调字段（`mcs_bppstatuscode` vs `mcs_bppstatus`）<br>3. 业务确认台账动作类型 `mcs_adjust` 值（当前 `1`） |


## 会话更新（2026-07-10）— 厂端授信 `FcaProcCalculationPlugin` 场景3/4 测试工具修复与 DEV1 复测通过

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-10 |
| 目标 | 修复 `FactoryCreditTest` 测试工具，重新验证 `FcaProcCalculationPlugin` 场景3（实质性逾期）和场景4（集团黑名单客户） |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，测试客户 `LTC客户-1`（`mcs_customermasterdata`: `edff63c8-325a-f111-a824-6045bd1bc963` / `account`: `3c67d74c-445a-f111-a825-7ced8de5b9c3`） |
| 涉及文件 | `Code/Tools/FactoryCreditTest/Program.cs`<br>`Code/Tools/FactoryCreditTest/TestBlacklist.cs`<br>`Code/Tools/FactoryCreditTest/TestNormal.cs` |
| 测试工具修复 | 1. **增强 `TestOverdueScenario`**：在更新逾期场景前重置客户主数据 `mcs_blacklist=false`，避免场景3与场景4同时触发；统一通过 `ResetOutstanding` 更新/创建 `mcs_outstanding` 人民币字段（`mcs_newoverdueamountrmb`、`mcs_newremainingamountrmb`）。<br>2. **增强 `TestBlacklist`**：设置 `mcs_blacklist=true` 后，将 `mcs_outstanding` 重置为非逾期状态（逾期天数=30、逾期金额=0、余额=500000），确保只触发场景4。<br>3. **增强 `TestNormal`**：重置 `mcs_blacklist=false` 与 `mcs_outstanding` 非逾期状态，避免历史逾期/黑名单数据干扰正常三因子计算。<br>4. **新增 `Program.ResetOutstanding`**：按 Account 查询已有的 `mcs_outstanding` 记录并更新；不存在则新建，保证测试数据始终可用。<br>5. **增强 `CheckResult`**：查询并打印 `mcs_creditreject`（不予授信场景数组）、关联客户主数据的 `mcs_creditgrant`（不予授信标志）和 `mcs_blacklist`。 |
| DEV1 主 Assembly | ✅ 当前 Assembly 已包含 PRD 修正后的逻辑，`FcaProcCalculationPlugin` 使用 `mcs_outstanding` 人民币字段计算逾期比例，并回写 `mcs_customermasterdata.mcs_creditgrant`。 |
| 场景3验证 | ✅ 命令：`dotnet run test-overdue edff63c8-325a-f111-a824-6045bd1bc963 600000 500000`<br>• 逾期天数=200，逾期金额=600,000，在外货款余额=500,000，比例=120%<br>• `mcs_modelgrant=0.00` / `mcs_initigrant=0.00`<br>• `mcs_creditreject=[3]`<br>• 客户主数据 `mcs_creditgrant=true`<br>• 计算日志：`触发不予授信场景3：实际逾期账龄≥6个月且逾期金额/在外货款余额>50%` |
| 场景4验证 | ✅ 命令：`dotnet run test-blacklist edff63c8-325a-f111-a824-6045bd1bc963`<br>• `mcs_blacklist=true`，在外货款为非逾期状态<br>• `mcs_modelgrant=0.00` / `mcs_initigrant=0.00`<br>• `mcs_creditreject=[4]`<br>• 客户主数据 `mcs_creditgrant=true`<br>• 计算日志：`触发不予授信场景4：集团黑名单客户` |
| 正常场景复测 | ✅ 命令：`dotnet run test-normal edff63c8-325a-f111-a824-6045bd1bc963`<br>• `mcs_modelgrant=1,800,000.00` / `mcs_initigrant=1,800,000.00`<br>• `mcs_creditreject=(空)`<br>• 计算日志：`未触发系统自动判断的不予授信场景` |
| 状态 | ✅ 场景3/4 DEV1 复测通过；测试工具输出已增强，可一次性确认额度、`mcs_creditreject`、客户主数据 `mcs_creditgrant` 与 `mcs_blacklist`。 |
| 遗留说明 | 1. `mcs_fca_proc.js` 已通过 MetadataTool 直接部署到 DEV1，但未找到其在远程主项目仓库的对应路径，未随本次 PR 提交；如需纳入版本控制需后续确认归属 Solution/目录。<br>2. 正常场景复测后，客户主数据 `mcs_creditgrant` 仍为 `true`（由之前场景3/4写入）。当前 Plugin 逻辑仅在触发不予授信时设置 `mcs_creditgrant=true`，未触发时不主动清空；这是否需要业务上“非拒绝时重置为 false”，待用户确认。 |


## 会话更新（2026-07-10）— `mcs_fca_proc` 事业部/归属组织从当前用户带出

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-10 |
| 目标 | 在 `mcs_fca_proc`（厂端授信模型计算表）新建记录时，自动从当前系统用户带出事业部和归属组织名称 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260629_peter` |
| 涉及字段 | `mcs_buid`（事业部）、`mcs_buname`（事业部名称）、`mcs_orgid`（归属组织）、`mcs_orgname`（归属组织名称） |
| 修改文件 | `Code/Customizations/WebResources/JS/mcs_fca_proc.js` |
| 核心改动 | 1. `setDefaultValues` 中新增 `loadCurrentUserOrgInfo` 调用<br>2. 通过 `Xrm.Utility.getGlobalContext().userSettings.userId` 获取当前用户 ID<br>3. 使用 `Xrm.WebApi.retrieveRecord` 查询 `systemuser` 并 expand `businessunitid` / `organizationid` 读取名称<br>4. 将名称回填到 `mcs_buname` / `mcs_orgname`；`mcs_buid` / `mcs_orgid` 同步回填同名，避免为空 |
| DEV1 部署 | ✅ WebResource `mcs_fca_proc.js` 已更新并发布 |
| 待验证 | 在 DEV1 新建 `mcs_fca_proc` 记录，确认「事业部」「归属组织」字段自动显示当前登录用户的 businessunit / organization 名称 |


## 会话更新（2026-07-10）— `mcs_fca_proc` 事业部/归属组织从当前用户带出（修正版）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-10 |
| 目标 | 解决 `mcs_fca_proc` 新建记录时「事业部」「归属组织」未自动带出的问题 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260629_peter` |
| 根因 | 1. 表单上可见字段实际为 `mcs_buid` / `mcs_orgid`（非 `mcs_buname` / `mcs_orgname`）<br>2. 这两个字段在窗体上 `disabled="true"`，且 `mcs_buid`/`mcs_orgid` 最大长度仅 20，原名称可能超长导致 setValue 异常或被截断<br>3. 禁用字段默认提交行为可能导致 JS 设置值后未随表单保存 |
| 修改文件 | `Code/Customizations/WebResources/JS/mcs_fca_proc.js` |
| 核心改动 | 1. 查询方式改为先查 `systemuser` 取 `_businessunitid_value` / `_organizationid_value`，再分别查 `businessunit` / `organization` 名称<br>2. `setStringValue` 增加按字段最大长度截断（`mcs_buid`/`mcs_orgid` 截断到 20）<br>3. 设置值前调用 `attr.setSubmitMode("always")`，确保禁用字段也能随表单提交<br>4. 同时回填 `mcs_buid`/`mcs_orgid`（可见字段）和 `mcs_buname`/`mcs_orgname` |
| DEV1 部署 | ✅ WebResource `mcs_fca_proc.js` 已更新并发布<br>🔄 实体 `mcs_fca_proc` 发布因环境正在执行 PublishAll 暂时阻塞，待重试 |
| 待验证 | 在 DEV1 新建 `mcs_fca_proc` 记录，强制刷新浏览器（Ctrl+F5 / Cmd+Shift+R）后确认事业部/归属组织自动显示 |


## 会话更新（2026-07-10）— `mcs_fca_proc` 事业部/归属组织从当前用户带出（最终修正）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-10 |
| 目标 | 解决 `mcs_fca_proc` 新建记录时事业部/归属组织未带出的问题，并修复控制台报错 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260629_peter` |
| 根因 | 1. `systemuser` Web API 查询使用了错误的 `$select=_businessunitid_value,_organizationid_value`，正确应为 `$select=businessunitid,organizationid`<br>2. 未处理 Web API 返回 Lookup 的多种格式（`{id,name}` / `string` / `_field_value`）<br>3. `mcs_outstanding` 查询误用 `mcs_account eq <Guid>`，正确应为 `_mcs_account_value eq <Guid>` |
| 修改文件 | `Code/Customizations/WebResources/JS/mcs_fca_proc.js` |
| 核心改动 | 1. `loadCurrentUserOrgInfo` 改为 `?$select=businessunitid,organizationid`<br>2. 新增 `getLookupGuidFromResult` 兼容三种 Lookup 返回格式<br>3. `checkAutoRejectScenes` 中逾期查询改为 `_mcs_account_value eq `<br>4. 保留 `setSubmitMode("always")` 和字段长度截断 |
| DEV1 部署 | ✅ WebResource `mcs_fca_proc.js` 已更新并发布<br>✅ 实体 `mcs_fca_proc` 已发布 |
| 待验证 | 在 DEV1 强制刷新后新建 `mcs_fca_proc` 记录，确认事业部/归属组织自动带出，且控制台无逾期查询报错 |


## 会话更新（2026-07-10 续）— `mcs_fca_proc` 事业部/归属组织自动带出最终修复

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-10 |
| 根因 | 1. 之前 JS 取数链路错误：直接查 `systemuser.businessunitid/organizationid`，但 PRD 要求链路是 `systemuser → mcs_useraccount → mcs_org → mcs_bu`<br>2. DEV1 中 `# 邱正卫` 对应的 `mcs_useraccount.mcs_orgid` 为空，无数据可带出 |
| 数据修复 | 用户已在 D365 UI 中为 `# 邱正卫` 的 `mcs_useraccount` 维护 `mcs_orgid = India v2`（ORG-001270），对应事业部 `India v2`（BU-1023） |
| JS 修改 | `Code/Customizations/WebResources/JS/mcs_fca_proc.js` 中 `loadCurrentUserOrgInfo` 改为按 PRD 链路查询：<br>1. `retrieveMultipleRecords("mcs_useraccount", ...)` 按 `_mcs_systemuserid_value` + `statecode eq 0` 查当前用户的用户账号<br>2. 取 `mcs_orgid` lookup GUID<br>3. `retrieveRecord("mcs_org", ...)` 查 `mcs_name` + `mcs_buid`<br>4. `retrieveRecord("mcs_bu", ...)` 查 `mcs_name`<br>5. 回填 `mcs_buid`/`mcs_orgid`/`mcs_buname`/`mcs_orgname` |
| DEV1 部署 | ✅ WebResource `mcs_fca_proc.js` 已更新并发布<br>✅ 实体 `mcs_fca_proc` 已发布 |
| 待验证 | 在 DEV1 强制刷新后新建 `mcs_fca_proc` 记录，确认事业部/归属组织自动带出 `India v2` |


## 会话更新（2026-07-10 续）— LTC客户-1 逾期数据设置（触发不予授信场景 3）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-10 |
| 目标 | 为 `LTC客户-1` 设置后台逾期数据，使其在 `mcs_fca_proc` 新建/客户选择时自动触发不予授信场景 3 |
| 场景 3 条件 | 逾期账龄 ≥ 6 个月（180 天）且 逾期金额 / 在外货款余额 > 50% |
| 修改记录 | `mcs_outstanding` 记录 `d815647f-0f7c-f111-ab0e-7ced8db4dd60`（客户 `LTC客户-1`） |
| 设置值 | `mcs_overdurationdays = 200`<br>`mcs_newoverdueamountrmb = 100000`<br>`mcs_newremainingamountrmb = 100000`<br>`mcs_overdue = true` |
| 逾期占比 | 100%（满足 > 50% 条件） |
| 预期效果 | 新建 `mcs_fca_proc` 并选择 `LTC客户-1` 后，JS 的 `checkAutoRejectScenes` 会自动将【不予授信】字段设置为选项 3（实际逾期账龄 ≥ 6 个月且逾期金额/在外货款余额 > 50%） |
| 待验证 | 在 DEV1 新建 `mcs_fca_proc`，选择客户 `LTC客户-1`，确认【不予授信】字段自动勾选选项 3 |


## 会话更新（2026-07-10 续）— LTC客户-1 黑名单设置（触发不予授信场景 4）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-10 |
| 目标 | 将 `LTC客户-1` 改为触发不予授信场景 4（集团黑名单客户），并清除场景 3 的逾期数据 |
| 修改记录 1 | `mcs_customermasterdata` 记录 `edff63c8-325a-f111-a824-6045bd1bc963`（对应客户 `LTC客户-1`）的 `mcs_blacklist` 设为 `true` |
| 修改记录 2 | `mcs_outstanding` 记录 `d815647f-0f7c-f111-ab0e-7ced8db4dd60` 的逾期数据已清零，避免同时触发场景 3 |
| 清零字段 | `mcs_overdurationdays = 0`<br>`mcs_newoverdueamountrmb = 0`<br>`mcs_newremainingamountrmb = 0`<br>`mcs_overdue = false` |
| 预期效果 | 新建 `mcs_fca_proc` 并选择 `LTC客户-1` 后，只自动勾选选项 4（集团黑名单客户） |
| 待验证 | 在 DEV1 新建 `mcs_fca_proc`，选择客户 `LTC客户-1`，确认【不予授信】字段只自动勾选选项 4 |


## 会话更新（2026-07-10 续）— BPF 阶段客户字段设为只读

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-10 |
| 目标 | 将 BPF 流程阶段中显示的客户字段设为只读，防止误改 |
| 修改文件 | `Code/Customizations/WebResources/JS/mcs_fca_proc.js` |
| 核心改动 | 1. 新增 `setBpfStageFieldsReadOnly(formContext)`：当 `mcs_accountid` / `mcs_custname` 有值时，调用 `control.setDisabled(true)` 设为只读；无值时保持可编辑（便于新建时选择）<br>2. 在 `onLoad` 时初始调用并注册 `addOnStageChange` 事件，阶段切换时刷新只读状态<br>3. 在 `onAccountChange` 客户选择完成后调用，确保选择后自动变为只读 |
| DEV1 部署 | ✅ WebResource `mcs_fca_proc.js` 已更新并发布<br>✅ 实体 `mcs_fca_proc` 已发布 |
| 待验证 | 在 DEV1 强制刷新后新建/打开 `mcs_fca_proc` 记录，选择客户后确认 BPF 阶段中的 Customer Name / 客户编码字段变为只读 |


## 会话更新（2026-07-10 续）— BPF 阶段字段永远只读

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-10 |
| 目标 | 将 BPF 流程侧窗格中的字段永久设为只读，参考 `mcs_credit_record.js` 实现 |
| 修改文件 | `Code/Customizations/WebResources/JS/mcs_fca_proc.js` |
| 核心改动 | 1. 将 `setBpfStageFieldsReadOnly` 改为 `lockBpfFields`，遍历 `formContext.ui.controls`，将所有名称以 `header_process_` 开头的 BPF 字段控件直接 `setDisabled(true)`<br>2. 不再判断字段是否有值，BPF 阶段字段始终只读<br>3. 在 `onLoad` 时调用，并注册 `addOnStageChange` 事件，阶段切换后重新锁定（BPF 侧窗格会重新渲染）<br>4. 客户选择完成后仍调用，确保渲染后状态正确 |
| DEV1 部署 | ✅ WebResource `mcs_fca_proc.js` 已更新并发布<br>✅ 实体 `mcs_fca_proc` 已发布 |
| 待验证 | 在 DEV1 强制刷新后打开 `mcs_fca_proc` 记录的 BPF 阶段侧窗格，确认所有 BPF 字段（含 Customer Name）均为只读 |


## 会话更新（2026-07-10 续）— 修复 `mcs_fca_proc` 客户编码取值错误

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-10 |
| 目标 | 修复 `mcs_fca_proc` 表单上选择客户后，「客户编码」字段带出 `TEST0210000677` 而非客户主数据表上显示的 `BMW0001` 的问题 |
| 根因 | `mcs_fca_proc.mcs_accountid` 的 Lookup 目标实体是 `mcs_customermasterdata`（见 `MetadataTool/Definitions/mcs_fca_proc.json`），但 `onAccountChange` 却用该 ID 去 `account` 表按 `_mcs_customermasterdata_value` 反查，取 `account.mcs_sapnumber`。`LTC客户-1` 关联 account 的 `mcs_sapnumber` 为 `TEST0210000677`，而其客户主数据的 `mcs_sapnumber` 才是 `BMW0001`，导致带出错误 |
| 修改文件 | `Code/Customizations/WebResources/JS/mcs_fca_proc.js` |
| 核心改动 | 1. 客户编码改为直接从 `mcs_customermasterdata.mcs_sapnumber` 取值<br>2. 保留通过 `_mcs_customermasterdata_value` 反查 `account` 的逻辑，仅用于缓存真实 `accountid`，供逾期数据查询使用<br>3. 两个查询使用 `Promise.all` 并行执行，任一失败不影响另一部分逻辑 |
| DEV1 部署 | ✅ WebResource `mcs_fca_proc.js` 已更新并发布（33873 bytes）<br>✅ 实体 `mcs_fca_proc` 已发布 |
| 待验证 | 在 DEV1 新建/重新选择 `mcs_fca_proc` 的客户 `LTC客户-1`，确认「客户编码」显示为 `BMW0001`（与客户主数据表一致） |
| 后续注意 | `mcs_fca_proc` 字段显示名当前与 JSON 定义不一致：`mcs_accountid` 实际显示为「客户名称」、`mcs_custname` 实际显示为「客户编码」。如需要统一回 JSON 定义，需重新调整字段标签/表单布局 |


## 会话更新（2026-07-10 续）— `mcs_fca_quota` 字段显示名修复

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-10 |
| 目标 | 修复 `mcs_fca_quota` 上两个字段的显示名：Lookup 字段 `mcs_accountid` 显示为「客户名称」、文本字段 `mcs_custname` 显示为「客户编码」 |
| 根因 | `MetadataTool` 的 `update-field-displayname` 使用 SDK `UpdateAttributeRequest`，无法正确更新组织基础语言（2052 简体中文）的本地标签；英文（1033）标签已更新，中文标签未生效 |
| 修复方式 | 改用 `MetadataTool` 的 `set-field-label` 命令，通过 Web API PUT 直接更新 `AttributeMetadata.DisplayName.LocalizedLabels`，同时写入 2052 和 1033 标签 |
| 当前状态 | ✅ `mcs_fca_quota.mcs_accountid` → 客户名称 / Customer Name<br>✅ `mcs_fca_quota.mcs_custname` → 客户编码 / Customer Code<br>✅ `mcs_fca_records.mcs_accountid` → 客户名称 / Customer Name<br>✅ `mcs_fca_records.mcs_custname` → 客户编码 / Customer Code<br>✅ 实体 `mcs_fca_quota`、`mcs_fca_records` 已发布 |
| 注意事项 | D365 窗体/视图可能仍有客户端缓存，建议在 DEV1 打开 `mcs_fca_quota` 记录或窗体设计器前强制刷新浏览器（Ctrl+F5 / Cmd+Shift+R），或在隐私窗口中验证 |
| 待完成 | 1. 用户确认 DEV1 UI 中显示名已正确切换<br>2. 按需进行 `mcs_fca_proc` 模型计算流程的 DEV1 功能验证<br>3. 按需通过 n8n Release Tool 发布到 UAT |


## 会话更新（2026-07-11）— `mcs_fca_quotaapp` 审批链接字段修复

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-11 |
| 目标 | 修复 `mcs_fca_quotaapp` 保存时报错“审批链接：此字段最多可包含 100 个字符，您已超出此限制” |
| 根因 | JS `mcs_fca_quotaapp.js` 的 `generateRecordUrl` 在表单加载时把 D365 记录链接（约 119 字符）写入 `mcs_fca_quotaapp_url`，但该字段为 String(100)；同时该字段业务用途应为 BPP 审批链接，而非 CRM 记录链接 |
| 修改文件 | 1. `Code/Customizations/WebResources/JS/mcs_fca_quotaapp.js`<br>2. `Code/SanyD365Project/Service/SanyD365.Main/Entities/BPP/BPPHandlerServices/BPPHandlerServiceForFcaQuotaApp.cs` |
| 核心改动 | 1. 删除 JS 中 `onLoad` 对 `generateRecordUrl` 的调用，并删除 `generateRecordUrl` 函数，避免新建/打开记录时写入超长的 CRM 链接<br>2. 后端 `PreStart` 清空旧链接时，将 `mcs_bpplink` 改为 `mcs_fca_quotaapp_url`<br>3. 后端 `UpdateEntityStatusForStart` 写入 BPP 审批链接时，将 `mcs_bpplink` 改为 `mcs_fca_quotaapp_url`，使流程发起后表单“审批链接”字段显示真正的 BPP 审批链接 |
| DEV1 部署 | ✅ WebResource `mcs_fca_quotaapp.js` 已更新并发布 |
| 待完成 | 1. 在远程服务器 `tx-windows` 上编译并发布 Service 项目 DLL，使后端改动生效<br>2. DEV1 验证：新建 `mcs_fca_quotaapp` 记录可正常保存；提交 BPP 审批后“审批链接”字段显示 BPP 链接而非 CRM 链接<br>3. 如 BPP 链接实际长度超过 100，需扩大 `mcs_fca_quotaapp_url` 字段长度 |


## 会话更新（2026-07-11 续）— FCA 审批链接后端分支已推送

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-11 |
| 远程服务器 | `tx-windows` (`122.51.232.70`) |
| 编译结果 | ✅ `SanyD365.Main.csproj` Release 编译通过（0 错误） |
| Git 分支 | `uat-20260711-peter-fcaquotaapp-link` |
| Commit | `fix(fca_quotaapp): BPP审批链接回写字段从mcs_bpplink改为mcs_fca_quotaapp_url` |
| 推送状态 | ✅ 已推送至 Azure DevOps `https://dev.azure.com/SanyGlobalCRM/D365/_git/D365` |
| 待用户操作 | 1. 在 Azure DevOps 创建 PR → 合并到 `uat`<br>2. 通知我“PR 已合并” |
| 合并后下一步 | 1. 在 `tx-windows` 拉取最新 `uat` 重新编译 `SanyD365.Main`<br>2. 通过 n8n Release Tool 发布 `Messagehandler`（BPP Consumer）到 DEV/UAT |


## 会话更新（2026-07-11 续）— FCA 审批链接 PR 已合并并重新编译

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-11 |
| PR 状态 | ✅ 已合并到 `uat` |
| 远程服务器 | `tx-windows` (`122.51.232.70`) |
| 拉取/编译 | ✅ `git checkout uat && git pull origin uat` 成功<br>✅ `SanyD365.Main.csproj` Release 重新编译通过（0 错误） |
| 待完成 | 通过 n8n Release Tool 发布 `Messagehandler` 到目标环境 |


## 会话更新（2026-07-13）— 融资管理模块实体/字段在 DEV1 创建完成

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-13 |
| 目标 | 为「融资管理」模块在 DEV1 创建 3 个实体及全部字段 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260713` |
| PRD | `Documents/PRD/融资管理需求说明.docx` |
| 涉及实体 | `mcs_fsm_resource`（融资资源管理表）<br>`mcs_fsm_data`（融资管理表）<br>`mcs_fsm_detail_data`（融资落实表） |
| 实体定义文件 | `Code/Tools/MetadataTool/Definitions/mcs_fsm_resource.json`<br>`Code/Tools/MetadataTool/Definitions/mcs_fsm_data.json`<br>`Code/Tools/MetadataTool/Definitions/mcs_fsm_detail_data.json` |
| 字段设计要点 | • 附件与 BPP 审批复用现有机制，不复建新实体<br>• 金融产品多选使用 `Memo` 字段存逗号分隔代码，前端自定义控件实现<br>• 业务币种金额用 `Money`（币种 = `mcs_fsm_currency`），带 `USD` 字样的固定 USD 金额用 `Decimal`<br>• 客户字段：`mcs_customer_id` 保留文本存 SAP 编码；`mcs_customer_name` 为 Lookup → `mcs_customermasterdata`<br>• `mcs_fsm_data` 共 33 个字段；`mcs_fsm_resource` 共 18 个字段；`mcs_fsm_detail_data` 共 6 个字段 |
| MetadataTool 修复 | `EntityManager.CreateLookupField` 自动生成 1:N Relationship 名称时，将 `SchemaName` 从 `{target}_{entity}_{field}` 改为 `mcs_{entity}_{target}_{field}`，解决标准实体（`transactioncurrency`/`systemuser`/`salesorder`）Lookup 创建失败的问题 |
| 创建结果 | ✅ `mcs_fsm_resource`：实体 + 全部 18 个字段创建成功<br>✅ `mcs_fsm_data`：实体 + 全部 33 个字段创建成功（含 `mcs_fsm_currency` 重试后成功）<br>✅ `mcs_fsm_detail_data`：实体 + 全部 6 个字段创建成功（含首次失败的 `mcs_order_id` Lookup → `salesorder` 补建成功） |
| 状态 | ✅ DEV1 实体/字段创建全部完成 |
| 待完成 | 1. 表单/视图由用户自行在 D365 UI 中配置<br>2. 后续按业务需求开发 Plugin / JS / 工作流 |
| 相关文件 | `Code/Tools/MetadataTool/Services/EntityManager.cs`<br>`Code/Tools/MetadataTool/Definitions/mcs_fsm_*.json`<br>`Documents/Planning/融资管理数据表定义_v1.md` |


## 会话更新（2026-07-13 续）— 融资管理模块中英双语标签更新

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-13 |
| 目标 | 为融资管理 3 个实体及全部字段设置中英双语显示名 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`) |
| 涉及实体 | `mcs_fsm_resource`（融资资源管理 / Financing Resource）<br>`mcs_fsm_data`（融资管理 / Financing Management）<br>`mcs_fsm_detail_data`（融资落实 / Financing Implementation） |
| 问题发现 | `MetadataTool` 的 `update-entity-displayname` / `update-field-displayname` 使用 SDK `UpdateEntityRequest` / `UpdateAttributeRequest`，在 Device Code Flow 认证下只能正确更新英文（1033）标签，简体中文（2052）标签不持久化 |
| 工具增强 | 1. `D365ToolCommon.Metadata.MetadataFieldService` 新增 `SetEntityDisplayNameAsync`：通过 Web API PUT 直接更新 `EntityMetadata.DisplayName` / `DisplayCollectionName` 的 `LocalizedLabels`，同时写入 2052 和 1033<br>2. `MetadataTool` 新增 CLI 命令 `set-entity-label <实体名> <中文> <英文>`<br>3. 已有 `set-field-label <实体名> <字段名> <中文> <英文>` 命令用于字段双语标签 |
| 更新方式 | • 实体显示名：使用 `set-entity-label`<br>• 字段显示名：使用 `set-field-label`<br>• 批量脚本：`Code/Tools/MetadataTool/update_fsm_labels.py` |
| 字段覆盖 | 3 个实体全部 57 个自定义字段 + 3 个主字段均已更新中英双语标签 |
| 发布状态 | ✅ `mcs_fsm_resource`、`mcs_fsm_data`、`mcs_fsm_detail_data` 已逐个发布完成 |
| 验证结果 | `get-entity-displayname` 确认：实体显示名同时具备 `LCID=1033` 和 `LCID=2052`；抽样字段均同时具备英文和中文标签 |
| 状态 | ✅ 中英双语标签更新完成，DEV1 中文/英文用户均可看到对应语言显示名 |
| 待完成 | 1. 选项集字段的选项标签目前为中文，如需要中英双语可继续处理<br>2. 表单/视图由用户自行在 D365 UI 中配置 |
| 相关文件 | `Code/Tools/D365ToolCommon/Metadata/MetadataFieldService.cs`<br>`Code/Tools/MetadataTool/Program.cs`<br>`Code/Tools/MetadataTool/update_fsm_labels.py`<br>`Code/Tools/MetadataTool/Definitions/mcs_fsm_*.json` |


## 会话更新（2026-07-13 续）— 融资资源管理金融产品字段改为多选选项集

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-13 |
| 目标 | 将 `mcs_fsm_resource.mcs_fsm_institution_products` 从多行文本改为多选选项集 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`) |
| 字段 | `mcs_fsm_resource.mcs_fsm_institution_products`（金融产品名称 / Financial Products） |
| 旧类型 | `Memo`（多行文本，存逗号分隔代码） |
| 新类型 | `MultiSelectPicklist`（多选选项集） |
| 选项值编码 | 按用户确认方案：**银行产品 1-11，保险产品 101-104** |
| 银行产品（11 个） | 1 Non-recourse Accounts Receivable Factoring<br>2 Recourse Accounts Receivable Factoring<br>3 Purchase Loan Financing<br>4 Inventory Financing<br>5 Dealer Financing<br>6 Retail Factoring<br>7 Leasing<br>8 Consortium<br>9 Investment Loan<br>10 wholesale<br>11 Others |
| 保险产品（4 个） | 101 特险<br>102 项目险<br>103 投资险<br>104 贸易险 |
| 已执行操作 | 1. 从主窗体/视图移除旧字段<br>2. 删除旧 Memo 字段<br>3. 修改 `Definitions/mcs_fsm_resource.json`：类型改为 `multiselectpicklist`，配置 15 个选项<br>4. 使用 `create Definitions/mcs_fsm_resource.json` 重建字段<br>5. 使用 `set-field-label` 更新中英双语显示名<br>6. 发布实体 `mcs_fsm_resource` |
| 验证结果 | `query-optionset mcs_fsm_resource mcs_fsm_institution_products` 确认 15 个选项值及标签正确；`list-fields` 确认字段显示名具备 `LCID=1033` 和 `LCID=2052` |
| 状态 | ✅ 字段改造完成并发布 |
| 待完成 | 1. 前端 JS 按机构类型（银行/保险）筛选可选项<br>2. 表单/视图由用户自行在 D365 UI 中配置 |
| 相关文件 | `Code/Tools/MetadataTool/Definitions/mcs_fsm_resource.json`<br>`Documents/Planning/融资管理数据表定义_v1.md`（已同步更新字段类型、选项值及编码说明） |


## 会话更新（2026-07-13 续）— 创建融资资源管理测试记录

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-13 |
| 目标 | 创建一条 `mcs_fsm_resource` 测试记录，便于在未加入导航时查看表单界面 |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`) |
| 新增命令 | `dotnet run create-fsm-resource-testdata` |
| 实现位置 | `Code/Tools/MetadataTool/Program.cs`：`CreateFsmResourceTestData` |
| 测试记录 ID | `c051f7ce-8c7e-f111-ab0e-7ced8db4d37f` |
| 记录链接 | `https://dev1.crm5.dynamics.com/main.aspx?appid=&pagetype=entityrecord&etn=mcs_fsm_resource&id=c051f7ce-8c7e-f111-ab0e-7ced8db4d37f` |
| 记录内容 | 机构名称：测试融资机构；机构类型：银行；金融产品：1, 2（Non-recourse / Recourse Accounts Receivable Factoring） |
| 状态 | ✅ 测试记录创建成功 |


## 会话更新（2026-07-17）— 融资管理 BPP 审批开发（立项审批 + 融资方案审批）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-17 |
| 目标 | 实现 `mcs_fsm_data`（融资管理）两种顺序 BPP 审批：立项审批（状态2→3）与融资方案审批（状态3→4） |
| 依据 | 《融资管理PRD.docx》、《融资管理数据表定义_v1.md》、《融资管理BPP审批开发方案.md》（2026-07-16 最终确认版） |
| 环境 | DEV1 (`https://dev1.crm5.dynamics.com`)，Solution `entity_20260713`；远程 `tx-windows` (`C:\Projects\D365`) |
| 关键决策 | 1. 当前审批人回写**复用 `mcs_bppapprover`**，不新增 `mcs_nextapprover`（用户确认）<br>2. 方案 5.2.1 笔误修正：融资管理编号取 `mcs_fsm_no`（`mcs_fsm_managment_no` 已删除合并）；BPP 表单变量 key 仍按模板 Excel 用 `mcs_fsm_managment_no`<br>3. TemplateCode DTO `BPP_WorkFlowTemplateCodeEntity` 实际定义在远程 `DTO\BPP\BPPFormData.cs`，在其中追加属性<br>4. 融资管理 3 个实体的自动编号由用户用**通用自动编号插件**配置，不开发专门 AutoNumberPlugin（用户明确） |
| 元数据变更 | ✅ DEV1 `mcs_fsm_data` 新增 `mcs_approve_type`（Picklist：1 立项审批 / 2 融资方案审批，中英双语），字段 ID `3ab08456-6281-f111-ab0e-7ced8de4eab4`；实体已发布；`Definitions/mcs_fsm_data.json` 已同步 |
| 新增文件 | `Code/Customizations/Plugins/FinancingManagement/Bpp/FsmDataBppIntegrationPlugin.cs`<br>`Code/Customizations/Plugins/FinancingManagement/Bpp/FsmDataBppCallbackPlugin.cs`<br>`Code/Customizations/Plugins/FinancingManagement/FinancingManagementPlugins.csproj`<br>`Code/Customizations/WebResources/JS/mcs_fsm_data.js`（DEV1 WebResource ID `d1a14571-6381-f111-ab0e-6045bd1c0e3b`，已发布）<br>`Code/SanyD365Project/Service/SanyD365.Main/Entities/BPP/BPPHandlerServices/BPPHandlerServiceForFsmData.cs`（本地同步副本） |
| 修改文件 | 远程 `Service/SanyD365.Main/DTO/BPP/BPPFormData.cs`（`BPP_WorkFlowTemplateCodeEntity` 追加 `FsmDataInitiation`/`FsmDataProject`）<br>远程 `Service/SanyD365.Main/StartupHelper.cs`（注册 `Handler["mcs_fsm_data"]`）<br>远程 `SanyD365.D365Extension.Sales.csproj`（加入 2 个 Plugin 文件）<br>`Code/Tools/sync-plugin-to-remote.py`（新增 FinancingManagement 命名空间/文件映射）<br>`Code/Tools/MetadataTool/Program.cs`（新增 `test-fsm-bpp` 全场景验证命令）<br>`Code/INDEX.md`（第 5 章 BPP 集成追加融资管理条目） |
| Service 端编译 | ✅ `SanyD365.Main.csproj` 远程编译通过（0 错误）。注意：首次编译报 PdfSharpCore/IAppRcsService 错误为 restore/工作区残留问题，`dotnet restore` + `git checkout -- IAppRCSService.cs` 后消失；`record.GetDateTimeValue` 不存在，CrmEntity 时间扩展方法为 `GetTimeStringOrDefault(name, format, default)` |
| 主项目编译 | ✅ `SanyD365.D365Extension.Sales.csproj` 远程编译通过（仅项目原有警告） |
| DEV1 验证 | ✅ 临时 Assembly `SanyD365.Plugins.FinancingManagement` 注册后执行 `test-fsm-bpp`，4 场景全部通过：<br>1. 立项审批通过：`fsm_status` 2→3，`can_initiated`=0，`bppstatus`=3<br>2. 方案审批通过：`fsm_status` 3→4，`is_valid`=true，`can_project`=0，`bppstatus`=3<br>3. 方案审批驳回：`fsm_status` 保持 3，`can_project`=1，`bppstatus`=4<br>4. 撤回：`bppstatus`=1，`bppid`/`bppapprover` 清空<br>测试记录：`FT0717062823`（`abc4e3a3-6581-f111-ab0e-6045bd1c0925`） |
| 临时 Assembly 清理 | ✅ 已注销 `SanyD365.Plugins.FinancingManagement`（Step×2 / Type×2 / Assembly） |
| Git 分支 | `uat-20260717-peter-fsm-bpp` → 已推送 Azure DevOps（commit `dc5eb8d0a08`，6 文件 +890 行）；PR #5592 **已合并到 `uat`**（merge commit `06b464898d0`） |
| 主 Assembly 部署 | ✅ 合并后 `uat` 重编通过（Sales + Service 均 0 错误）<br>✅ DEV1 主 Assembly `SanyD365.D365Extension.Sales` 已更新（ID: `9d6ff315-8c03-4d51-b641-ebeccf9e98b0`，8350 KB）<br>✅ 正式 Step 已注册：Integration（ID: `da70d1e1-6781-f111-ab0e-6045bd1c0925`，Filter=`mcs_bppstatus`）+ PreImage（ID: `ce59e64e-6881-f111-ab0e-6045bd1d22ee`）；Callback（ID: `f17f2752-6881-f111-ab0e-7ced8db4dda8`，Filter=`mcs_bppstatuscode`）<br>✅ 主 Assembly 端到端复测 `test-fsm-bpp` 4 场景全部通过<br>⚠️ 已知问题：远程工作区 `IAppRCSService.cs` 反复物理缺失（git tracked 但 checkout/pull 不写出），需 `git checkout -- <文件>` 恢复后 Service 才能编译，原因待查 |
| BPP 模板 | `Documents/BPP/融资立项审批_BPP模板配置.xlsx`、`融资方案审批_BPP模板配置.xlsx` 已交付 BPP 团队；**TemplateCode 待返回**，返回后需更新 `ms_systemconfiguration.BPPWorkFlowTemplateCode` JSON 的 `FsmDataInitiation`/`FsmDataProject` |
| 待完成 | 1. ~~用户合并 PR 到 `uat`~~ ✅ 已完成（PR #5592）<br>2. ~~更新 DEV1 主 Assembly + 注册正式 Step + 端到端复测~~ ✅ 已完成（4 场景通过）<br>3. ~~创建 2 个 App Action 按钮~~ ✅ 已通过 DeployTool `fsmbuttons` 命令创建（`AppActionDeployer.DeployFsmDataButtons`）：【提交立项审批】ID `a1471e63-6b81-f111-ab0e-6045bd1d22ee`、【提交融资方案审批】ID `a9471e63-6b81-f111-ab0e-6045bd1d22ee`，Solution `entity_20260713`，参数 PrimaryControl，图标均为勾号 CheckMark，sequence 100100040/100100041（两按钮相邻，位于保存并关闭之后）。**注意**：按钮当前为始终显示，JS 内部校验兜底；显隐规则需用户在 Command Designer 手动配置 Power Fx（状态=2且can_initiated / 状态=3且can_project）<br>4. ~~BPP TemplateCode 返回后配置~~ ✅ 已完成（2026-07-17）：BPP 团队返回两个模板 Code，已按「读取→备份→追加→写回→全量校验」流程更新 `ms_systemconfiguration.BPPWorkFlowTemplateCode`：**DEV1**（185 原有 0 变更 + 追加 2 → 187）与 **UAT**（224 原有 0 变更 + 追加 2 → 226），`FsmDataInitiation`=`794612913352237105`（立项审批）、`FsmDataProject`=`794612841038241880`（方案审批）；备份 `/tmp/bpp_templatecode_backup_20260717.json`（DEV1）、`/tmp/bpp_templatecode_uat_backup_20260717.json`（UAT）。**注意**：DEV1/UAT 真实联调还需 n8n 发布 `Messagehandler`（用户等定时发布窗口），发布后 MessageHandler 才能找到 `Handler["mcs_fsm_data"]`<br>5. n8n 发布：`McsPlugin` + `Messagehandler`（建议连同 `ClientAPI`）<br>6. ⏸️ 按钮发布后用户在 DEV1 `Sany CRM Sales` App 的 `mcs_fsm_data` 表单上验证按钮可见、点击校验提示正常 |


## 会话更新（2026-07-17 续）— "幽灵删除"事件调查（IAppRCSService.cs 等 3 文件）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-17 |
| 现象 | 多个工作区（`tx-windows` `C:\Projects\D365`、同事 `D:\WorkSpace\三一重工\SourceCode`）反复出现：**git 索引正常 tracked、远程存在，但工作区物理缺失，VS/Git 显示为 D（删除）** |
| 涉及文件 | 1. `Service/SanyD365.Main/Application/RCS/AppRCSService.cs`<br>2. `Service/SanyD365.Main/Application/RCS/IAppRCSService.cs`<br>3. `Service/SanyD365.Main/Entities/DAL/Service/NegativeFeedBackOrderRepository.cs` |
| 已确认事实 | 1. 全库 `git ls-files` 检查**无大小写重复条目**（排除 case-collision 假设）<br>2. `ad244e13257`（YangJianHui，2026-07-16 17:40）commit message「还原被删除的文件」：重新创建 `IAppRCSService.cs`（+16 行）——**说明幽灵删除已被人误提交推送过一次，文件被真删后刚还原**<br>3. `1585016f6c8`（gw_zhangx158，2026-07-15）「修改空格」触碰 `AppRCSService.cs`<br>4. tx-windows 上两次复现：`git checkout/pull` 后 `IAppRCSService.cs` 物理缺失导致 Service 编译失败（CS0246 找不到 IAppRcsService），`git checkout -- <文件>` 恢复后编译通过 |
| 危害路径 | 工作区出现幽灵删除 → 有人 `git add .` 或全量提交 → 删除被 push 到远程 → 其他人 pull 后文件真消失 → `AppRCSService.cs` 引用 `IAppRcsService` 编译失败，Service 项目全毁 |
| 根因 | 未 100% 定位。疑似 Windows 杀毒/文件锁在 checkout 写入时拦截，或特定工作区索引状态异常。防范比根因更紧迫 |
| **防范措施（AI 必守）** | 1. commit 前必查 `git status --porcelain`，**只 `git add` 显式列出的本次任务文件**（本次融资管理 PR 即如此操作）<br>2. **严禁 `git add .` / `git add -A` / `git commit -a`**<br>3. 看到上述 3 个文件显示 D 时，一律 `git checkout -- <文件>` 恢复，**绝不 stage**<br>4. 编译 Service 项目前先确认 `Service/SanyD365.Main/Application/RCS/IAppRCSService.cs` 物理存在 |
| tx-windows 当前状态 | ✅ 工作区干净、RCS 2 文件完整、分支 `uat` |
| 同事工作区处置 | 截图中 3 个删除**不要提交**，执行 `git checkout -- <3个文件路径>` 撤销（用户已在群内通知） |


## 会话更新（2026-07-17 续）— `mcs_QueryTradeStPayTerm` 实现插件迁移至 D365ExtensionApi.Sales

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-17 |
| 起因 | 同事导出 `McsCustomAPI` 解决方案时报「缺少必需的非托管组件：QueryTradeStPayTerm 插件类型」，点「添加到解决方案」报错 `Entity 'pluginassembly' With Id = c16279d7... Does Not Exist` |
| 根因 | `QueryTradeStPayTermPlugin` 错放在业务插件 Assembly `SanyD365.D365Extension.Sales`（归属 McsPlugin 解决方案）。项目惯例：**所有 Custom API 实现插件必须放在 `SanyD365.D365ExtensionApi.*` 系列项目**（这些 Assembly 均在 McsCustomAPI 解决方案内，依赖闭环）；唯独本插件放错位置，导致 Custom API（在 McsCustomAPI）跨包依赖 Assembly（在 McsPlugin） |
| 证据 | 抽查 5 个 Custom API（含 mcs_CofaceSearchCompany）实现插件全部在 `D365ExtensionApi.*` 且 Assembly 在 McsCustomAPI 包内；`SanyD365.D365Extension.Sales`(9d6ff315) 不在 McsCustomAPI 包内 |
| 迁移内容 | 1. `QueryTradeStPayTermPlugin.cs` + `TradeStPayTermQueryService.cs` 从 `SanyD365.D365Extension.Sales\Plugins\TradeStPayTerm\` 迁至 `SanyD365.D365ExtensionApi.Sales\Apis\TradeStPayTerm\`，命名空间 `...Sales.Plugins.TradeStPayTerm` → `SanyD365.D365ExtensionApi.Sales.Apis.TradeStPayTerm`<br>2. DEV1：更新 Api.Sales Assembly → 手动创建新 plugintype(`5bcb6bdb`) → Custom API 换绑（`PluginTypeId@odata.bind`）→ 删除旧 plugintype(`c16279d7`) → 更新 Sales Assembly（移除旧类）<br>3. 业务插件（AutoNumber/Validation/Sync）留在原 Assembly 不动 |
| 迁移结果 | ✅ Custom API 调用验证通过（status=1，入参/出参签名不变，调用方无感知）<br>✅ 依赖闭环：Custom API → 新 plugintype → D365ExtensionApi.Sales(3aa32db6) **已在 McsCustomAPI 包内**<br>✅ 旧 plugintype c16279d7 已删除，业务 Steps 完整 |
| 迁移中踩坑 | 1. plugintype 备用键是 `name` 字段：旧 type 占用 "QueryTradeStPayTermPlugin"，新 type 须先用临时 name 创建，删旧 type 后改回<br>2. `update-assembly` 更新 Assembly 时校验「已注册 plugintype 必须存在于 DLL」，故须先删旧 type 再更新<br>3. Custom API 换绑后，原 implementation Step 由系统自动重建指向新 type |
| Git 分支 | `uat-260717-peter-tradestpayterm-api-move`（commit `fa9180c231`，git 识别为 rename 97%/99%，**已合并**）<br>`uat-260717-peter-tradestpayterm-api-stub`（commit `88de9f97c11`，恢复空壳类，**已合并**） |
| 本地工具扩展 | `Code/Tools/sync-plugin-to-remote.py` 扩展双项目同步：新增 `REMOTE_PROJECT_DIR_API` + `FILE_MAP_API`（TradeStPayTerm.Api → Api.Sales 项目），NAMESPACE_MAP 新增 `SanyD365.Plugins.TradeStPayTerm.Api` 映射；`update_csproj`/`build_remote` 增加 csproj 名参数；dry-run + 命名空间转换 + 远程 diff 验证通过 |
| 后续进展 | 1. ✅ UAT `McsCustomAPI` 发布成功（Custom API 换绑新 type 5bcb6bdb，同 DEV1 ID）<br>2. ❌ UAT `McsPlugin` 首次发布失败 `80048071`（Existing plug-in types have been removed）：托管环境 UAT 仍注册旧 type c16279d7，包里 DLL 已不含该类，且 Assembly 版本号未提升不允许移除 plugintype<br>3. ✅ 依客户惯例「插件不删、代码留空」：Sales 项目恢复 `QueryTradeStPayTermPlugin` 空壳类（Obsolete 标记、Execute 空实现）<br>4. ✅ DEV1 用 Web API **按原 ID 重建 plugintype**（POST plugintypes 显式指定 `plugintypeid=c16279d7`，name 用 `QueryTradeStPayTermPlugin_Legacy 避开备用键冲突）——无需 PRE 导包<br>5. ✅ 用户亲自导出 McsCustomAPI 验证：**缺失组件提示消失，依赖项问题正式闭环**<br>6. ✅ UAT `McsPlugin` 重新发布**成功**（Assembly ModifiedOn=2026-07-17 02:57，旧 type c16279d7 保留为空壳，Custom API 端到端调用验证通过）——**全部收尾完成** |
| 规范沉淀 | **新建 Custom API 时，实现插件一律归并到 `SanyD365.D365ExtensionApi.*` 对应项目**（参照 Coface），严禁放入 `D365Extension.*` 业务插件项目<br><br>**插件废弃惯例（客户要求）**：不使用的 plugintype **不删除**，代码保留空壳类（注释/空实现），避免托管环境版本号校验问题<br><br>**环境知识**：UAT 的 McsPlugin/McsCustomAPI 均为**托管**（ismanaged=true），直接在 UAT 删托管组件会产生 Active 层，严禁；托管包更新 Assembly 时若移除 plugintype 必须提升 major/minor 版本号（错误 80048071）<br><br>**PRE 环境**：`pre.crm5.dynamics.com`（（勿动）用于生产环境bug修复），生产镜像，组件 ID 与生产/UAT 一致，组件为**非托管**，可用于取回同 ID 组件<br><br>**plugintype 备用键是 `name` 字段**：同名类（不同命名空间）共存时需用不同 name<br><br>**Web API 可按指定 GUID 创建 plugintype**（POST body 带 plugintypeid），用于跨环境同 ID 恢复组件<br><br>**tx-windows 编译/发布前必须先 `git pull`**：本次曾基于落后 17 个提交的本地 uat 编译（幸无交集），教训已固化 |



## 会话更新（2026-07-17 续）— 成交条件样板库「待审批/生效」全表单只读

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-17 |
| 需求 | 成交条件样板库（`mcs_trade_stpayterm`）审批通过后应只读；用户确认**待审批（1）+ 生效（2）**两种状态都要全表单只读（防止审批中内容被改导致审批不一致） |
| 修改 | `Code/Customizations/WebResources/JS/mcs_trade_stpayterm.js` 的 `TradeStPayTermForm.setReadOnlyFields`：`mcs_status` 为 1/2 时遍历 `formContext.data.entity.attributes` 禁用全部控件；未生效（0）维持原逻辑（仅禁用 `mcs_trade_stpaytermname` 编码 + `mcs_status`） |
| 部署 | `DeployTool dotnet run tradestpayterm-wr` 更新并发布 DEV1 ✅ |
| 验证 | ✅ 生效记录 TC26062502：20 个控件全部禁用<br>✅ 未生效记录 TC26071202：业务字段均可编辑，仅编码/创建时间禁用 |
| 遗留（用户自行处理） | header 上的「生效状态」控件（`header_mcs_status`）在未生效记录中可编辑，存在**手动改状态绕过审批**的漏洞；原 JS 用 `getControl("mcs_status")` 未覆盖 header 控件。用户决定**在表单设计器手动设置该字段只读** |
| 备注 | WebResource 不入远程 git 仓库，本地 `Code/Customizations/WebResources/` 即源 |



## 会话更新（2026-07-17 续）— 目录结构整理

| 项目 | 内容 |
|---|---|
| 删除 | 根目录 9 张会话截图、`.kimi/` 6 个会话迁移脚本、`.playwright-mcp/`（41 个浏览器日志）、11 个 `.DS_Store` |
| 移动 | `update_csproj_scoringcard.ps1` → `Backups/Code/`<br>`entity_20260603_peter_1_0_0_1.zip` → `Backups/Solutions/`<br>根目录 `DailyReports/`（5 个日报）→ `Documents/DailyReports/`（**根目录 DailyReports 已不存在**） |
| .gitignore 新增 | `.playwright-mcp/`、`.kimi/`、`.venv_excel/` |
| 本地 git 状态 | `Code/Tools`、`Code/Customizations` 等存在大量历史未提交修改，**用户决定暂不处理、不提交**，后续会话请勿擅自 commit |

**环境差异补充（2026-07-17 用户确认）**：BPP 审批链路**只能在 UAT 测试**。DEV1 未注册 BPP 消息处理服务，信用评估记录推进到「审核申请」阶段时报「找不到消息类型为 mcs_credit_record 的消息处理服务（BPPHandlerServiceMain）」属**预期行为**，非缺陷；DEV 上 BPF 流程最多只能验证到「信用分计算」阶段，「审核申请→审批通过」须到 UAT 验证。


## 会话更新（2026-07-17 续）— UAT 厂端授信 BPP 测试（进行中）+ 附件组件排障

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-17 |
| 任务 | UAT 验证厂端授信额度申请单（`mcs_fca_quotaapp`）BPP 审批：新建申请单 → 提交 BPP → 用户审批 |
| 测试数据 | ✅ 客户 `Kedai Kek`（368f8b3e，编码 AIN202602260000，等级 A3/分类 C）<br>✅ 额度记录 `ce7c363f`：厂端授信额度 500,000 / 余额 320,000 USD，2026-07-01 生效<br>✅ 模型参数补齐：`mcs_fca_mdlconfig` 客户分类 C 全组合 6 条（A0-A4+ALL，梯度对齐 S/A/B） |
| 申请单 | `FCA202607170003`（5986fca2）：调整后额度 600,000 / 调整后余额 420,000，产品类型=燃油牵引车，支付=赊销，账期=60 天 |
| 环境配置补齐 | 1. ✅ UAT `mcs_fca_proc.mcs_doid` 自动编号配置（FCM+日期+4位序列）+ 通用编号 Step 注册，验证生成 `FCM202607170001`<br>2. ✅ UAT `UploadFileTypeMapping` 追加 `mcs_fca_quotaapp` 映射（**001-004+099，Max=10，与 DEV1 严格一致**；曾误按 mcs_credit_record 模板加成 9 种，已修正） |
| 附件组件排障 | 现象：附件 Tab「加载数据失败！」<br>根因：UAT `mcs_customer_file` 实体**缺 `mcs_fca_quotaappid` Lookup 字段**（Uploader 组件按 `entityName=mcs_fca_quotaapp + entityId` 经此字段查附件）<br>处置：DEV1 确认有该字段（ID `963555b6`，关系 `mcs_fca_quotaapp_mcs_customer_file_mcs_fca_quotaappid`）；用户经 `entity_20260716_peter` 解决方案**仅勾选该单字段**发布 UAT（最小增量） |
| 重要经验 | 1. **通用附件组件（Uploader.html）依赖**：`UploadFileTypeMapping` 配置（ms_systemconfiguration.ms_content，注意 30000 字符上限、只追加不覆盖）+ `mcs_customer_file` 上指向业务实体的 Lookup 字段，两者缺一不可<br>2. **实体 OTC 码**：`mcs_fca_quotaapp` 在 DEV1/UAT 均为 13799（解决方案导入保留），静态 URL 参数不受影响<br>3. **后台改数据用 get-token + curl**：避免浏览器会话过期/跨域问题；改公共配置前必须本地备份 + 逐 key 比对校验 |
| 待办 | 1. ⏸️ `entity_20260716_peter`（含 mcs_fca_quotaappid 字段）导入 UAT 后验证附件组件加载<br>2. ⏸️ 提交 BPP 并观察 `mcs_bppstatuscode`/工作流ID/审批链接，用户审批后验证回调<br>3. ⏸️ **融资管理发布 UAT 前**：需先在 DEV1 补建附件关联字段（`mcs_customer_file.mcs_fsm_dataid` Lookup → `mcs_fsm_data`，如需再加 `mcs_fsm_detail_dataid`/`mcs_fsm_resourceid`），随 `entity_20260713` 一次发齐——UAT 当前无任何 `mcs_fsm_*` 实体 |
| 存量脏数据 | UAT `mcs_fca_proc` 有一条 doid=null、initigrant=0 的测试残留（68099d80），建议后续删除 |


## 会话更新（2026-07-17 续）— UAT 厂端授信 BPP 提交无回写根因定位与修复（幽灵字段）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-17 |
| 现象 | UAT 厂端授信申请单（`FCA202607170004`）提交 BPP 后：`mcs_bppstatuscode=Submitted`，但 BPP工作流ID/当前审批人/审批链接**无任何回写** |
| 链路定位 | `mcs_bppstatuscode=Submitted` 由 ExtensionApi（`mcs_bppstartapi`）写入 ✅；工作流ID/审批人/链接由 MessageHandler 消费后回写 → **断点锁定在 MessageHandler 消费段** |
| AI 日志排查 | AI-uat（Application Insights）查到框架异常：`"Position":"BPP GetBppFormData 执行失败，实体：mcs_fca_quotaapp"` → MessageHandler 是新版、Handler 已注册并执行，失败在 `GetBppFormData` 内部；框架 `Message` 只写 `logger name is loggerfile...txt`（服务器文件指针），详情不进 AI |
| **根因** | `BPPHandlerServiceForFcaQuotaApp.GetBppFormData` 的 FetchXML 引用了**从未创建的字段** `mcs_applicant`/`mcs_applydate`（DEV1/UAT 实体均无，`Definitions/mcs_fca_quotaapp.json` 34 字段中也没有）→ CRM 查询直接报"字段不存在"→ BPP 发起失败 |
| DEV1 为何未暴露 | 2026-07-08 DEV1"验证通过"只走到 `mcs_bppstartapi` 返回 Submitted（ExtensionApi 层）+ 手工模拟回调；**DEV1 无 MessageHandler 消费，`GetBppFormData` 从未真实执行**。UAT 是该链路首次真实执行 |
| 验证方法 | MetadataTool `get-token` + curl Web API 逐字段验证 `EntityDefinitions(LogicalName='mcs_fca_quotaapp')/Attributes(...)`：UAT 缺 `mcs_applicant`/`mcs_applydate`，其余 8 个 FetchXML 字段均在 |
| 修复（方案A） | FetchXML 移除 2 个幽灵字段改查 `createdby`/`createdon`；`mcs_applicant` ← `createdby` 名称、`mcs_applydate` ← `createdon`（`GetTimeStringOrDefault("createdon","yyyy-MM-dd","")`，对齐 FsmData 模式）；BPP 模板变量名不变，平台侧无感知 |
| Git | 分支 `uat-20260717-peter-fca-bppfield-fix`（单 commit `a0cb19e3124`，+5−4）→ **PR !5656 已合并**（merge `383418db1bf`）；远程 Release 编译通过 |
| 次要疑点（未决） | 外层 catch 应回写错误到 `mcs_bpperrormsg`（截断 1000 字符），但 UAT 记录该字段为 null——回写被静默吞（`WriteErrorToQuotaAppAsync` 内 catch{}），待修复后复测时观察 |
| 待办 | 1. ⏸️ 等 `IAppRCSService.cs` 恢复（见下条）→ 远程拉最新 uat 重编 Service 验证<br>2. ⏸️ n8n 发布 `Messagehandler`（连同 `ClientAPI`）到 UAT<br>3. ⏸️ UAT 重新提交 `FCA202607170004`，验证工作流ID/审批人/审批链接回写，顺带观察 `mcs_bpperrormsg` |

### 排障经验沉淀（BPP/MessageHandler 类问题）

1. **链路二分定位**：`mcs_bppstatuscode=Submitted` = ExtensionApi 层 OK；工作流ID 空 = MessageHandler 层断。
2. **AI 查询方法**：exceptions + traces（severityLevel>=3，覆盖 `LoggerMainHelper.LogError` 盲区）；结果多用 `summarize count() by cloud_RoleName, substring(outerMessage,0,150)` 分组；KQL **列名不能用中文**（字符串值可以）；时间窗用 UTC。
3. **框架日志只给文件指针**：`"Message":"logger name is loggerfileYYYYMMDD/日期/GUID.txt"` → 详情在 MessageHandler 服务器本地文件；但我们代码的 `LoggerMainHelper.LogInformation` **不进 AI traces**。
4. **最快验证路径**：`get-token` + curl 直接读 UAT 记录的 BPP 字段值 + 逐字段验证元数据存在性，比翻服务器日志快。
5. **"DEV1 验证通过"≠ BPP 链路真实通过**：DEV1 无 MessageHandler 消费，`GetBppFormData` 类代码的真实执行只能在 UAT 暴露。凡 FetchXML/字段引用，开发时应先对照 `Definitions/*.json` 或 `list-fields` 核实字段真实存在。

## 会话更新（2026-07-17 续）— IAppRCSService.cs 幽灵删除第三次：已随 PR 真实合入 uat

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-17 |
| 事件 | 拉取最新 uat（fast-forward 116 commits）时发现 `Service/SanyD365.Main/Application/RCS/IAppRCSService.cs` 被**真实删除并合入 uat**（非工作区幽灵态） |
| 删除 commit | `217b17bb14f` "fix"，作者 **zhoujia**（914608403@qq.com），今天 17:09，分支 `uat-zj`，经 PR 5649/5650 合入 |
| commit 完整改动 | `AppSaleOrder.cs`（+2−2 正常业务修复）+ `IAppRCSService.cs`（−16 误删）→ **不能 revert 整个 commit**（会连带回滚业务修复），只能单独恢复文件 |
| 影响 | `AppRCSService.cs` 仍 `: IAppRcsService` → **uat 上 Service 项目编译必败（CS0246）**，全项目组编译 Service 均受影响；n8n 发布 Messagehandler 被硬阻塞 |
| 处置 | 1. 已在 `uat-20260717-peter-fca-bppfield-fix` 分支用 `git checkout 217b17bb14f~1 -- <文件>` 恢复（commit `7866f94c960`）并验证编译通过<br>2. 用户决定先由 zhoujia/他人处理，故已将该 commit 从分支移除（reset + cherry-pick BPP commit + force push），当前分支仅含 BPP 修复并已合并<br>3. **若 zhoujia 迟迟不恢复，可随时重提单文件恢复 PR（内容 = 删除前版本，零风险）** |
| 关键认知 | **无 PR 编译门禁**（PR !5656 秒合验证）；提前合并 BPP PR 无害但发布仍被堵，殊途同归 |
| 防范重申 | commit 前必查 `git status --porcelain`；严禁 `git add .`；RCS 3 文件（AppRCSService.cs / IAppRCSService.cs / NegativeFeedBackOrderRepository.cs）显示 D 一律 `git checkout --` 恢复，绝不 stage |


## 会话更新（2026-07-17 续）— 第二个幽灵字段 mcs_nextapprover 修复 + fsm 字段全面排查

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-17 |
| 起因 | 用户要求确认融资管理是否也存在幽灵字段问题；对 FsmData/FcaQuotaApp 两个 BPP Handler 做全字段静态排查 |
| 排查方法 | 提取 Handler 代码中全部 FetchXML attribute + `Attributes.Add`/`Get*` 字段引用 → curl Web API 逐字段验证 DEV1/UAT 元数据 |
| fsm 结论 | ✅ `mcs_fsm_data` 13 个 BPP 相关字段 DEV1 全部存在，无幽灵字段（UAT 实体随 `entity_20260713` 发布时带齐） |
| fca 新发现 | ❌ **`mcs_nextapprover` 在 DEV1/UAT 均不存在**——位于 `UpdateEntityStatusForStart`（发起成功回写链接）和 `CallBack`（回调回写审批人）的同一个 Update 中，会导致**链接/审批人/回调状态全部回写失败**。若只修第一个幽灵字段，今晚发布后会"实例创建成功但依然无回写" |
| 修复 | 两处 `mcs_nextapprover` → `mcs_bppapprover`（DEV1/UAT 均存在，与 FsmData 复用决策一致）；分支 `uat-20260717-peter-fca-nextapprover-fix`（commit `51d25923acd`，+4−4）→ **PR !5659 已合并**（merge `24929bdee35`）；编译通过（临时恢复 RCS 文件编译后已清理） |
| fca 最终字段状态 | FetchXML（createdby/createdon 修复）+ 回写路径 9 字段，DEV1/UAT 全部存在 ✅ |
| 单元测试讨论 | 结论：纯单测防不住幽灵字段（mock CRM 后暴露不了）；建议分层：① MetadataTool 静态字段校验命令（推荐，投入产出比最高，待做）② SanyD365.Test 纯逻辑单测 ③ DEV 手动触发 GetBppFormData 诊断命令（治本） |
| 待办 | 1. ⏸️ 等 20:00 周佳发版（MessageHandler 组 16，发布同时带 fsm Handler；发版前 IAppRCSService.cs 必须恢复，否则编译失败）<br>2. ⏸️ 发布后 UAT 重新提交 FCA202607170004 验证回写<br>3. ⏸️ 后续补 MetadataTool `validate-bpp-handler-fields` 静态校验命令 |


## 会话更新（2026-07-19 凌晨续）— UAT 验证受阻：MessageHandler 服务停摆（非代码问题）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-18 深夜 ~ 2026-07-19 凌晨 |
| 验证动作 | 用户新建 UAT 申请单 `FCA202607190001`（ID `d0773982-f082-f111-ab0f-0022485add74`），05:34（北京）提交 BPP |
| 现象 | `mcs_bppstatuscode=Submitted`（ExtensionApi ✅），但 1 小时后仍零回写；AI 日志 UTC 21:30 后零 fca 记录；**全服务（15/16 组）UTC 20:50:55 后整体静默** |
| 关键排除 | 1. ❌ "没重新提交"：新单已提交<br>2. ❌ "16 组没发"：群机器人显示 **liun47 已于 7/18 11:26 部署 `LocalMessageHandle16-uat` 完成**（部署代码含两个幽灵字段修复）<br>3. ❌ "日志摄入延迟"：1 小时后仍零记录<br>4. ✅ 结论：**消息未被消费，MessageHandler 服务停摆**（15 组 LTC 消息同样无人消费，影响面超出本模块） |
| 架构认知 | **每个消息组是独立 App Service**（服务名如 `LocalMessageHandle16-uat`），按组独立部署 |
| 待查疑点 | liun47 部署通知中夹有一条 n8n「McsEntity 执行错误」（executions/32952），虽最终显示"部署完成"，部署完整性存疑 |
| 当前阻塞 | 用户**无 App Service 权限**，已群内 @周佳 @liun47 请求检查并启动服务 |
| 服务恢复后 | 1. 积压消息自动消费（含修复代码，**无需重新提交**）<br>2. 查 `FCA202607190001` 的 `mcs_bppid`/`mcs_bppapprover`/`mcs_fca_quotaapp_url` 回写<br>3. 若消费后再次失败 → 查 AI 新异常接着排 |
| 经验 | 1. D365 字段查询是**实时**的，无回写=未成功处理；AI 日志有分钟级摄入延迟，凌晨排障先以字段为准<br>2. "Submitted 后无回写"排查顺序：记录 modifiedon（是否重新提交）→ AI 日志（有无消费痕迹）→ 服务状态（是否停摆）→ 队列积压 |


## 会话更新（2026-07-19 下午续）— 根因最终定位：框架统一回写 mcs_workflowid/mcs_nextapprover，fca 实体缺字段

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-19 下午 |
| 突破 | 用户新建 `FCA202607190003`（f25076eb）提交，16 组消费日志抓到完整错误链 |
| 关键日志 | 框架（BPPHandlerServiceMain）发起成功后统一回写：`PATCH mcs_fca_quotaapps(...)` Body 含 **`mcs_workflowid`="866682370435215360"** 和 **`mcs_nextapprover`**="gw_huangwy4,lij105" → **400**（两字段在 fca 实体均不存在）→ 框架 catch 写 `mcs_bpperrormsg` 的 PATCH 里也嵌套了同样错误 → 再 400 → 错误信息永远写不进 |
| 最终根因 | **框架约定每个接入 BPP 的实体必须有 `mcs_workflowid` + `mcs_nextapprover` 字段**；fca 实体两个都没有；信用评估正常正因为 `mcs_credit_record` 上两字段齐全 |
| 重要修正 | 昨日 PR !5659（业务代码 `mcs_nextapprover`→`mcs_bppapprover`）**方向错误，需还原**：框架写的就是 `mcs_nextapprover`，正确做法是**实体补建字段对齐框架约定**（与 CreditRecord 老模块一致），而非改代码 |
| 附带发现 | 1. `GenericContextBuilder: 未找到实体 mcs_fca_quotaapp 的配置数据`——框架通用上下文配置缺失，独立问题<br>2. BPP 平台实际已成功创建流程实例（workflowId 866682370435215360、审批人 gw_huangwy4/lij105），只是回写失败<br>3. `QuotaApplyFeedback` 毒消息（ChooseDead key 格式错误）仍在 16 组刷屏，需找框架同事处理 |
| 三条测试单 | `FCA202607190001/0002/0003` 均 Submitted 无回写；0001 凌晨消息未即时消费应为毒消息阻塞或 AI 摄入延迟 |
| 修复计划 | 1. 还原业务代码两处为 `mcs_nextapprover` → PR<br>2. DEV1 补建 `mcs_nextapprover`/`mcs_workflowid`（String，中英标签）→ `entity_20260629_peter`<br>3. Solution 最小增量发 UAT<br>4. 发版窗口重发 16 组 → 重新提交验证 |


## 会话更新（2026-07-19 下午续）— BPP 字段框架模板确认：以 mcs_credit_record 为准的 8 写字段

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-19 |
| 模板确认 | `BPPHandlerServiceForCreditRecord`（唯一真实跑通的老模块）的 WRITE 字段 = `mcs_bppstatus`(String) / `mcs_workflowid`(String 100) / `mcs_nextapprover`(String 100) / `mcs_bpplink`(String 500) / `mcs_bpprejectreason`(Memo) / `mcs_bpperrormsg`(Memo) / `mcs_approvedate`(DateTime) / `statecode`；**框架（BPPHandlerServiceMain）另统一写 `mcs_workflowid` + `mcs_nextapprover`** |
| 关键认知 | 1. `mcs_credit_record` 上**没有** `mcs_bppstatuscode`——fca/fsm 用的 `mcs_bppstatuscode`+`mcs_bppid`+`mcs_bppapprover` 命名是另一套（或与 BPP 平台回调 DTO 兼容的另一约定，fsm 的"bppid 清空"在 UAT 回调验证过）<br>2. 用户拍板：**字段按模板一模一样补齐，业务代码不动**（fca 写 `mcs_bppapprover`/fsm 同理保留；链接各写各的 `mcs_*_url`） |
| fca/fsm 需补建 | `mcs_nextapprover`(String 100) + `mcs_workflowid`(String 100) + `mcs_bpplink`(String 500)，×2 实体，标签对齐模板 |
| 类型差异（不动） | fca/fsm 的 `mcs_bpprejectreason`/`mcs_bpperrormsg` 为 String，模板为 Memo——保留现状（业务代码截断 1000 字符内） |
| 待办 | 1. DEV1 补建 6 个字段（公共方法）<br>2. 加入 Solution 发 UAT<br>3. 16 组重发版（含昨日两次修复）<br>4. 重新提交验证 |


## 会话更新（2026-07-19 续）— 🎉 厂端授信 BPP 提交链路全通（里程碑）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-19 |
| 字段建设 | ✅ DEV1 为 `mcs_fca_quotaapp`/`mcs_fsm_data` 各补建 `mcs_nextapprover`(String 100)/`mcs_workflowid`(String 100)/`mcs_bpplink`(String 500)（Definitions JSON 追加 + `create` 幂等创建 + `set-field-label` 补双语 + 发布）；用户将 fca 3 字段经 Solution 发布 UAT |
| UAT 端到端验证 | ✅ `FCA202607190004`（a7bd3bae）提交后：**全部字段回写成功**——`mcs_workflowid`=866703565134131200（框架）、`mcs_nextapprover`/`mcs_bppapprover`=gw_huangwy4,lij105（框架/业务）、`mcs_fca_quotaapp_url`=审批链接（业务）、`mcs_bpperrormsg` 为空 |
| 最终根因总结 | ① FetchXML 幽灵字段 `mcs_applicant`/`mcs_applydate`（PR !5656）② 业务回写幽灵字段 `mcs_nextapprover`（PR !5659，后按用户决策保留=`mcs_bppapprover` 写法）③ **框架统一回写 `mcs_workflowid`/`mcs_nextapprover` 而实体缺字段**（本次补齐）——三层字段问题全部解决 |
| 关键认知 | 1. **接入 BPP 的实体必须备齐框架约定字段**：`mcs_workflowid`/`mcs_nextapprover`（框架写）+ `mcs_bppstatus`/`mcs_bpplink`/`mcs_bpprejectreason`/`mcs_bpperrormsg`/`mcs_approvedate`（Handler 写，对齐 mcs_credit_record 模板）<br>2. 框架回写失败会嵌套打爆错误回写（`mcs_bpperrormsg` 永远写不进）——"无回写+无错误信息"时应怀疑框架 PATCH 400<br>3. 排障路径回顾：AI 日志分组 → 记录字段直查 → 元数据逐字段验证 → 对照老模块模板 |
| 待办 | 1. ⏸️ 用户在 BPP 平台审批 `FCA202607190004`，验证回调：mcs_bppstatuscode=Approved → mcs_bppstatus=3 → 额度表更新 + FCR 台账（可顺带测驳回）<br>2. ⏸️ fsm 的 BPP：实体随 `entity_20260713` 发 UAT 后验证（字段已备齐）<br>3. ⏸️ `QuotaApplyFeedback` 毒消息（ChooseDead key 格式）找框架同事处理——fca 回调若是此消息类型必须上线前解决<br>4. ⏸️ `GenericContextBuilder: 未找到 mcs_fca_quotaapp 配置数据` 观察<br>5. ⏸️ 清理：3 条旧测试单（FCA202607190001/0002/0003）及 BPP 平台孤儿流程实例；fca 旧冗余字段（mcs_bppid/mcs_bppapprover/mcs_fca_quotaapp_url）后续按需清理 |


## 会话更新（2026-07-19 续）— fca 提交按钮 JS 状态校验失效修复

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-19 |
| 现象 | `FCA202607190004` 已是审批中（mcs_bppstatus=2），点【提交】仍弹"审批已提交"，JS 状态校验未拦截 |
| 根因 | `mcs_bppstatus` 字段未放在表单上（审批 Tab 放的是 `mcs_bppstatuscode`），`formContext.getAttribute("mcs_bppstatus")` 返回 null → 校验跳过。**教训：getAttribute 只能读表单上的字段，状态判断应 retrieveRecord 查服务端** |
| 后端验证 | Plugin 双重防重复兜底生效（旧状态=审批中跳过 + `mcs_bppid` 进行中跳过），误点未造成 BPP 重复流程，`workflowid` 未变 |
| 修复 | `mcs_fca_quotaapp.js` `submitToBpp`：先 `Xrm.WebApi.retrieveRecord(..., "?$select=mcs_bppstatus")` 读最新状态，再按 1/4 放行、其他拦截 |
| 部署 | ✅ DEV1 已更新并发布；⏸️ UAT 随 `McsWebResource` 发布（用户操作） |
| 三层防护 | 1. 按钮显隐 Power Fx（用户 Command Designer 配置中）2. JS 服务端校验 3. Plugin 双重防重复 |


## 会话更新（2026-07-20）— 发版 Solution 完整性自检命令 check-release

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-20 |
| 背景 | 客户发版（DEV1→UAT）多次因**漏加组件到 Solution**（实体/字段/WebResource/Plugin Step/Custom API/App Action）导致发布中断，需补加后重发 |
| 目标 | 发版前跑一次**只读**自检：本次发版涉及的组件是否都在三一开发手册 4.4 规定的对应 Solution 里，缺什么列出来，**由用户自己在 D365 UI 添加，AI 不做任何改动**，全绿后等发布 |
| 命令 | `cd Code/Tools/MetadataTool && dotnet run --no-build -- check-release <清单.json> [--with-fields]` |
| 清单 | JSON 放 `Documents/Planning/Releases/`；字段：`name` / `entitySolution` / `entities` / `webresources` / `pluginTypes` / `customApis` / `appActions`（缺的组省略） |
| 核对规则（手册 4.4） | 实体/字段/App Action → 清单 `entitySolution`（entity_XX）；js/html/json → `McsWebResource`；Plugin Assembly+Step → `McsPlugin`（**Type 不单独入包属正常惯例**，实测 McsPlugin 中 componenttype=90 记录为 0、91 有 8、92 有 2699）；Custom API（含请求参数/响应属性）→ `McsCustomAPI` |
| 实现 | `Code/Tools/MetadataTool/Program.cs` 新增 `CheckRelease`（L2330 分发、L10672 实现）：按类型解析组件 ID → 查 `solutioncomponent`（solutionid+objectid）是否存在；全部只读零写操作 |
| 两个误报已修复 | 1. 实体以「包含所有子组件」入包时（solutioncomponent `rootcomponentbehavior=0`）字段隐式随包，`--with-fields` 下不再误报字段缺失；`rcb=1/2` 才逐字段核对，且排除 lookup 伴生 `*name` 虚拟属性（`AttributeOf != null` 过滤）<br>2. Plugin Type 本体不入包降级为 ℹ️ 备注，不计 ❌ |
| 清理 | 删除上一会话未提交的深度版 `Services/ReleaseCheckService.cs`（连 UAT 五项深度检查，超出用户需要的范围）及旧格式清单 `release-20260720-fsm.json` |
| DEV1 实测 | 用 `entity_20260713` 清单验证：✅ 路径（实体含全部子组件 / WR / Assembly+Step / CustomAPI 含子组件 / App Action）、❌ 路径（缺失组件）、⚠️ 路径（环境中不存在）均正确输出；编译 0 错误 |
| 文档同步 | ✅ `Code/INDEX.md` 第 10 章追加条目<br>✅ `/skill:d365-tools` 命令速查追加 `check-release`<br>✅ `/skill:d365-deploy` 新增 5.1「发版前 Solution 完整性自检（必做，只读）」章节 |
| 使用流程 | 1. 用户告知发版范围（或 AI 按 Memory 起草清单，用户确认）→ 2. 跑 check-release → 3. 有 ❌ 用户自己添加 → 4. 重跑全绿 → 5. 等 n8n 发布 |
| 现存清单 | `Documents/Planning/Releases/release-20260722-fsm.json`（fsm 示例，可改名复用） |


## 会话更新（2026-07-20 续）— AllComponent 主清单 Solution 建立并填充完成

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-20 |
| 背景 | 用户无法逐一列举每次发版组件，改为建立**主清单 Solution**「AllComponent_Peter_整理_禁止导入UAT」（唯一名 `AllComponent_Peter_NoUAT`，solutionid `7e4361e6-5d7a-f111-ab0e-6045bd1c0cde`，非托管）：放入我们所有模块用到的全部组件，**永不导入 UAT，仅供发版时对照**——发版前核对该 Solution 里的组件是否都在发版包中 |
| 用户确认的范围 | 1. 实体先放 20 个（信用评估 8 / 成交条件 3 / 厂端授信 6 / 融资管理 3）<br>2. `mcs_custcredit`（旧表）**不放**<br>3. 共享实体（`account` / `mcs_customermasterdata` / `mcs_customer_file`）**不放**（即使我们加过字段） |
| 已加入组件（94 项） | • 实体 20 个（含全部子组件，rootcomponentbehavior=0）<br>• WebResource 22 个（19 JS + 3 HTML）<br>• Plugin Step 33 个（23 个 Plugin 类的全部 Step，只加 Step 不加 Type 本体，限定 `SanyD365.D365Extension.Sales` / `SanyD365.D365ExtensionApi.Sales` 两个 Assembly）<br>• Custom API 1 个（`mcs_QueryTradeStPayTerm` 本体 + 5 请求参数 + 3 响应属性）<br>• App Action 10 个 |
| 组件源清单 | `Documents/Planning/Releases/allcomponent-peter.json`（新模块上线后追加，重跑命令幂等补齐） |
| 新增命令 | `dotnet run --no-build -- add-manifest-to-solution <清单.json> <Solution唯一名>`（Program.cs L2349 分发、L11270 实现）：查重 → 缺失才添加，幂等；appaction componenttype 反查现有 solutioncomponent 记录（实测 10156），不硬编码 |
| 两个待决策项 | 1. ⚠️ `CustomerFileAutoNumberPlugin`：DEV1 中注册在独立 Assembly `SanyD365.Plugins.CustomerFile`（不在两个主 Assembly 内），按规则未纳入——该插件是否已归并远程主项目？是否放宽 Assembly 限制纳入？<br>2. ⚠️ `CofaceSearchCompanyPlugin`：Type 匹配成功但无 Step（其实现挂在 Custom Action `mcs_CofaceSearchCompany` 的 sdkmessage 上，未随 Step 纳入）；该 Custom Action 本体（Process/sdkmessage）也未跟踪 |
| 发版时用法 | 发版前用 `check-release` 或以 `AllComponent_Peter_NoUAT` 为源做对照：该 Solution 中组件是否都在本次发版的 entity_XX / McsWebResource / McsPlugin / McsCustomAPI 中（对照命令待实现，本次未做） |


## 会话更新（2026-07-20 续）— AllComponent 主清单两个待决策项结论

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-20 |
| `CustomerFileAutoNumberPlugin` | 用户确认**不放入**主清单（独立 Assembly `SanyD365.Plugins.CustomerFile`，不随主 Assembly 发版） |
| `mcs_CofaceSearchCompany` 形态确认 | ✅ 用户记忆正确：**已从老式 Custom Action 改为 Custom API**。DEV1 实测：`customapi` 表有记录（ID `19a93b06-c775-f111-ab0e-6045bd1c0e3b`），实现 Plugin Type 为 `CofaceSearchCompanyApi`，位于 `SanyD365.D365ExtensionApi.Sales`（3aa32db6），Custom API 本体已在 `McsCustomAPI` 中 |
| 主清单补齐 | ✅ `mcs_CofaceSearchCompany` Custom API（本体 + 请求参数 CompanyName/CountryCode + 响应属性 ResultJson）已加入 `AllComponent_Peter_NoUAT`；执行结果：✅ 新加入 1 / ⊘ 已存在 97 / ⚠️ 1（CustomerFileAutoNumberPlugin 为有意排除） |
| 主清单最终规模 | 98 项组件：实体 20 + WebResource 22 + Plugin Step 33 + Custom API 2（含子组件 13）+ App Action 10 |
| 组件源清单 | `Documents/Planning/Releases/allcomponent-peter.json`（已追加 mcs_CofaceSearchCompany） |


## 会话更新（2026-07-22）— 融资资源管理表单脚本 mcs_fsm_resource.js（银行带出 + 产品筛选 + 切换清空）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-22 |
| 需求 | 1. 机构类型=银行时，选择银行自动带出机构代码（`mcs_bank.mcs_bankno`）/机构名称（`mcs_bank.mcs_name`）<br>2. 金融产品名称多选按机构类型筛选：银行 1-11 / 保险 101-104 / 其他仅 Others(11)（用户确认）<br>3. 从银行切换到其他类型时，同时清空 Bank/机构代码/机构名称三个字段（用户补充） |
| 新增文件 | `Code/Customizations/WebResources/JS/mcs_fsm_resource.js`（DEV1 WebResource ID `5e9d5ca7-6f85-f111-ab0f-7ced8db4d37f`，已发布） |
| 表单绑定 | `mcs_fsm_resource` 主窗体（ID `4c099276-46ea-4ff4-8bba-17d3036fb928`）已通过 update-form-xml 加入 formLibraries + onLoad 事件（`FsmResourceForm.onLoad`），实体已发布；字段 onChange 在 onLoad 中程序化注册 |
| 行为规则 | 银行：Bank 显示+必填、代码/名称只读自动带出；保险/其他：Bank 隐藏+非必填+清空、代码/名称可编辑手工输入；类型未选：产品选项不筛选 |
| 🚨 关键经验 | 1. `setRequiredLevel`/`getRequiredLevel` 是**属性**方法，控件上调用会抛 "is not a function" 并中断 onLoad 后续逻辑<br>2. FluentUI 多选选项集控件（multiselectoptionset）的 `addOption` 必须用**对象签名** `addOption({text, value})`，标准 `addOption(text, value)` 会抛 "Required parameter is null or undefined: optionSetItem.value"；代码用 `addOptionCompat` 先标准后对象兜底<br>3. 该控件 `control.getOptions()` 不可靠（可为空数组），验证选项筛选必须打开下拉 UI 看实际条目<br>4. Playwright 验证时：UCI 强缓存 WebResource，需 CDP `Network.setCacheDisabled + clearBrowserCache` 后 reload；表单脚本在 `uclient/blank.htm` iframe 中执行，`window.FsmResourceForm` 要在对应 frame 里查；控制台 `attr.setValue` 经 Xrm.Page 代理可能不触发 addOnChange 注册的处理器，类型切换类验证必须走真实 UI 点击 |
| 待办 | 1. ✅ `mcs_fsm_resource.js` 已加入主清单 `AllComponent_Peter_NoUAT`（solutioncomponent ID `8b71fcab-7685-f111-ab0f-6045bd1d22ee`），组件源清单 `allcomponent-peter.json` 已同步追加（WebResource 22→23）<br>2. ⏸️ UAT 发布时随 `McsWebResource` 发版 |


## 会话更新（2026-07-23）— 生产导入失败字段类型冲突排查（80041A06）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-23 |
| 背景 | 上次生产大版本 2026-07-03 23:00；本次 `entity_20260722` 批次导入生产失败：`Attribute mcs_applygenre is a Picklist, but a String type was specified`（80041A06）。导入在第一个冲突字段即中断，需列出全部冲突字段通知三一从生产删除 |
| 排查方法 | 1. **UAT `importjob` 历史全量扫描**（127 条失败记录；⚠️ UAT 导入历史仅保留到 06-21，更早被平台清理）：MetadataTool 新增 `list-failed-imports [条数] [起始日期]` 命令（只读，查 progress<100 的 importjob，解析 data 日志提取 root status + 正则扫描 `Attribute x is a Y, but a Z type was specified` 字段冲突）<br>2. **本地三份历史快照 vs DEV1 逐字段 diff**（6/3 XML 3 实体、6/8 pac 解包 8 实体、6/21 XML 15 实体，脚本 `Backups/Tests/field_type_audit_20260723/diff_field_types*.py`）<br>3. 用户截图实锤 |
| 🚨 必须从生产删除的字段（3 个） | 1. `mcs_fca_quotaapp.mcs_applygenre`（信保额度类型）：生产=Choice → 新包=String（07-07 删建，截图+生产报错+UAT 7/8 同错三重实锤）<br>2. `mcs_trade_stpayterm.mcs_buyergrade`（客户分类代码）：生产=String → 新包=MultiSelectPicklist（06-21~23 变更，截图实锤，UAT 6/25 曾报 `OptionSetId cannot be null for existing attribute mcs_buyergrade`）<br>3. `mcs_trade_stpayterm.mcs_creditgrade`（客户等级）：生产=String → 新包=Picklist（同批次变更，UAT 6/22 曾两次报同错） |
| 不阻塞但建议清理 | `mcs_trade_stpayterm.mcs_nation` / `mcs_trade_pttype`（06-25 废弃的旧 Lookup，update 模式导入不报错不删除） |
| 6/1~6/21 期间新增发现（快照 diff） | 4. `mcs_credit_scoringcard.mcs_listvalue`（定性项目值）：String→Lookup（6/3~6/8 变更），⚠️ 需三一生成核对，若生产为旧 String 版则同法删除<br>5. `account.mcs_creditgrade`：String→Picklist（6/8~6/15 变更），共享实体不在本次包内，低风险，顺手核对 |
| 已排除 | `mcs_fca_proc.mcs_creditreject`（07-01 删建，已随 7/3 成功进生产）；`mcs_fsm_*`（07-13 新建实体，生产不存在）；`mcs_weight`/`mcs_sellerbalance` 等仅为范围调整（可导入） |
| 其他团队同类问题 | `mcs_shipping_factory`（String→Lookup，CPQ 包，UAT 7/18 失败），若同批发生产需对应团队处理 |
| 经验教训 | 1. **「删字段重建」= 生产导入炸弹**：D365 不允许导入改变已存在字段类型；今后字段类型调整需求，优先考虑新建字段（新架构名）替代，或在发版清单中显式标注「需先在目标环境删除字段」<br>2. 导入报错 `OptionSetId cannot be null for existing attribute xxx` 也是同名字段 String→Picklist 变更的典型表现<br>3. `importjob` 实体只保留近期历史（约 1 个月），排查要趁早 |
| 产出文档 | `Documents/Planning/Releases/生产导入失败_字段类型冲突清单_20260723.md`（含给三一的删除操作步骤）<br>原始数据：`Backups/Tests/field_type_audit_20260723/`（DEV1 二十实体字段清单 + UAT 失败导入全文） |
| 工具扩展 | MetadataTool 新增 `list-failed-imports`（只读诊断命令） |
| 下一步 | 用户将清单转交三一删除生产 3 个字段 → 重新导入 `entity_20260722` 批次 → 若再报错按报错点名字段同样处理 |


## 会话更新（2026-07-23 续）— Plugin 字段依赖核对（配合生产删字段）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-23 |
| 背景 | 三一今晚从生产 Solution 临时删除我方类型冲突字段，担心 Plugin 引用这些字段导致导入/运行报错，要求核对全部 Plugin 依赖并列出 |
| 用户关键疑问 | Plugin 代码里的字段引用是否受影响？还是注册时绑定的字段才有影响？ |
| 两层结论 | 1. **注册绑定层（Step 筛选属性 + PreEntityImage）**：会被平台依赖系统跟踪。实查 DEV1 我方 33 个 Step 筛选属性仅 `mcs_status`/`mcs_bppstatus`/`mcs_bppstatuscode`/`mcs_itemintvalue2` 等，4 个 PreImage 仅 `mcs_status`/`mcs_bppstatus`；全环境 2472 个镜像无一引用被删字段 → **McsPlugin 导入不会因删字段报错**<br>2. **代码引用层**：不影响导入，但运行时 `ColumnSet`/ConditionExpression/`update["字段"]` 硬引用已删字段会抛异常（"entity doesn't contain attribute"）；`Contains`/`GetAttributeValue` 容错不抛 |
| 代码依赖明细 | • `mcs_applygenre`：Plugin/BPP Service 零引用，仅 JS 读取（容错）✅<br>• `mcs_buyergrade`/`mcs_creditgrade`（trade_stpayterm）：`TradeStPayTermValidationPlugin`（重复校验 ColumnSet）+ `TradeStPayTermQueryService`（Custom API）硬引用 ⚠️ 过渡期保存/调用报错<br>• `mcs_listvalue`（scoringcard）：`ScoreCalculator` + `CreditScoringCardValidationPlugin` 硬引用 ⚠️（属"需核对"字段，若生产已是 Lookup 则不用删）<br>• `mcs_nation`/`mcs_trade_pttype`：零引用 ✅<br>• **同名不同字段不受影响**：account/customermasterdata/fca_mdlconfig 的 `mcs_creditgrade`、`mcs_credititem_value.mcs_listvalue` 均独立字段不在删除范围 |
| 关键结论 | 按「删字段 → 导入 entity_20260722」顺序执行后字段以新类型重建，Plugin 已适配新类型（DEV1 验证过），无需重发 McsPlugin；过渡期不要保存成交条件记录、不要调 `mcs_QueryTradeStPayTerm` |
| 产出文档 | `Documents/Planning/Releases/Plugin字段依赖核对_20260723.md` |
| 工具用法沉淀 | Step 筛选属性/镜像核对：`query-plugin-steps <类名>`（输出含 Filter 行）+ `query-records sdkmessageprocessingstepimage name,attributes,imagetype,sdkmessageprocessingstepid 5000`（TopCount 上限 5000，超限报错 `Expected value between 0 and 5000`） |


## 会话更新（2026-07-23 续）— 方案C试运行：发版包字段对比机制（提前预警类型冲突）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-23 |
| 目标 | 验证「对比上次发版包 vs 本次发版包的实体字段，提前发现类型差异」的可行性（全程只读） |
| 方案A试点（暂缓） | `MetadataTool delete-field` 已加删除警告+YES交互确认（`--force` 跳过），在 `mcs_coface_exchange_rate` 临时字段上验证拦截/确认均有效；用户决定**先不推广**，代码保留在本地 |
| 方案C 试运行结论 | 1. **机制有效**：6/21 真实历史快照 vs 本次发版内容，精准抓出 `mcs_buyergrade`(String→多选)、`mcs_creditgrade`(String→Picklist) 两个真实冲突<br>2. **关键前提**：必须留存「发版时刻」的包；今天补导出的 `entity_20260703` 拿到的是**当前元数据**，对比显示"无冲突"是假象（`mcs_applygenre` 变化被时间抹平）<br>3. `mcs_listvalue` 变更发生在 6/3~6/8，早于 6/21 快照所以未出现；fca 实体不在 6/21 快照中所以 `mcs_applygenre` 未覆盖 |
| 附带发现 | `entity_20260703` 大包（186 实体）含 fca 6 实体，但**不含**信用/成交条件/fsm 实体 → 实锤生产的成交条件实体停留在 6 月初版本（String 字段） |
| 工具改动 | `D365ToolCommon.D365ConnectionFactory` 设备码连接的 `MaxConnectionTimeout` 10→30 分钟（大包导出 11MB 需要 ~15 分钟）；`entity_20260722` 30 分钟仍超时 → **可用 DEV1 `list-fields` 清单替代包内容做对比**（已验证等效） |
| 对比脚本（只读） | `Backups/Tests/field_type_audit_20260723/diff_solution_packages.py`（包 vs 包）<br>`diff_package_vs_dev1.py`（包 vs DEV1 清单目录）<br>输出三段：🚨同名类型不一致 / ➖已删字段 / ➕新增字段实体 |
| 类型映射坑 | Solution XML 中多选选项集导出为 `multiselectpicklist`，list-fields 显示 `Virtual`，映射表需统一，否则误报 |
| 落地建议（待用户确认） | 1. 每次发版导出 zip 归档到 `Backups/Solutions/Releases/`（发版时刻快照）<br>2. 下次发版前跑 `diff_solution_packages.py <上次包> <本次包>`，🚨段非空=需先处理目标环境字段<br>3. 后续可固化进 `check-release` 流程 |


## 会话更新（2026-07-23 续）— 方案C正式落地：发版包字段快照与对比机制

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-23 |
| 用户决策 | 按方案C落地，**全部逻辑只允许查询/导出，不涉及修改动作** |
| 归档目录 | `Backups/Solutions/Releases/`（含 README 命名规范）：`entity_20260603_peter_snapshot20260621.zip`（6/21 真实快照）、`entity_20260701_peter_exported20260723.zip`、`entity_20260703_exported20260723.zip`（⚠️ 内容为导出时刻元数据非 7/3 原貌）、`entity_20260722_fieldsnapshot_20260723/`（20 实体 list-fields 清单）、**`entity_all_0720_peter_exported20260724.zip`（2026-07-24 补归档：本次发版内容备份，9 实体=6 fca+3 fsm，与生产大包中我方实体一致，已验证字段类型为新版）** |
| 工具固化 | `Code/Tools/release-diff/`：`diff_solution_packages.py`（包vs包）、`diff_package_vs_dev1.py`（包vs清单目录）、README（标准流程）；已登记到 `Code/INDEX.md` 第 9 节 |
| 规范固化 | `/skill:d365-deploy` 新增 **5.2 发版包字段快照与对比（防 80041A06，只读）**：每次发版必须归档快照（export 或 list-fields 清单）；下次发版前必须跑对比，🚨段非空=禁止直接发版，先通知三一删目标环境旧字段 |
| 验证 | 新位置脚本复跑正常：6/21 快照 vs 0722 清单精准抓出 buyergrade/creditgrade 类型冲突 |
| 方案A状态 | delete-field 警告确认代码保留在本地未推广（用户决定先不加） |


## 会话更新（2026-07-23 续）— PRE 环境核实：当前是 DEV1 镜像而非生产镜像

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-23 |
| 背景 | 用户提出 PRE 环境（`pre.crm5.dynamics.com`）理论上是生产 Copy，可用于直接核对实体差异（只读） |
| 核实结论 | ❌ **PRE 当前 = DEV1 镜像，不是生产镜像**。三重证据：<br>1. PRE 已有 7/22 刚在 DEV1 创建的 `mcs_usedsellerbalance`，且字段 ID（`79f5557d-6185-f111`）与 DEV1 **完全一致**（UAT 同名字段 ID 不同）<br>2. PRE importjob 近 200 条记录中**没有**我方任何发版包（entity_all_0720_peter / entity_20260722 / entity_20260713 / McsPlugin 等）的导入记录 → 新字段不是通过 Solution 导入进去的，是整环境刷新<br>3. PRE 的 `mcs_applygenre`=String、`mcs_buyergrade`=Virtual、fsm 三实体均存在且为新版 |
| 全量比对（只读） | PRE vs DEV1 二十实体全字段 diff：**零差异**（类型/字段增减均无），清单存于 `Backups/Tests/field_type_audit_20260723/pre/` |
| 影响 | 1. PRE **不能**用作生产基线核对——用它 diff 本次发版会显示"无差异"，掩盖生产真实冲突（生产的 buyergrade/creditgrade/applygenre 仍为旧类型）<br>2. ⚠️ 需向三一确认 PRE 刷新机制：Memory 记录 PRE 用于生产 bug 修复（生产镜像、组件非托管可取回同 ID 组件），若它刚被 DEV1 刷新覆盖，可能影响其原有用途 |
| 后续 | 若三一确认 PRE 恢复为生产镜像（生产→PRE Copy），方案C 可升级为「发版包 vs PRE 实时 diff」，比留快照更可靠；当前仍按归档快照机制执行 |
| 用户确认（2026-07-24） | ✅ 已证实：刘泞群公告「Pre环境克隆完了，可以开发了」——PRE 是刚从 DEV 克隆的新环境，与生产现状完全脱节，**不能用作生产基线核对**。生产字段核对仍只能依赖：发版快照对比机制（方案C）+ 三一在生产实际查看 |


## 会话更新（2026-07-24）— 发版检查清单成型 + entity_all_0720_peter 归档

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-24 |
| entity_all_0720_peter 定位澄清 | 用户确认：该包是我方自建的**全实体归纳 Solution**（9 实体=6 fca+3 fsm），生产实际导入的是三一侧大包，但我方实体内容一致 → 可作为本次发版内容基准。此前截图中 entity_all_0720_peter 的导入记录实为 **UAT** 历史（importjob UTC 时间 +8 后完全吻合；applygenre 报错属于 7/8 entity_20260708_peter 失败记录） |
| 本次发版备份 | ✅ `entity_all_0720_peter` 已导出归档（185KB，2 分钟）：`Backups/Solutions/Releases/entity_all_0720_peter_exported20260724.zip`，已验证 9 实体完整、`applygenre=nvarchar`、`fsm_institution_products=multiselectpicklist`、`usedsellerbalance=money` 均为新版 |
| 发版检查清单 | ✅ 已成型：`Documents/Planning/Releases/发版检查清单.md`（阶段 0-7：范围确认 → 主清单/依赖核对 → 字段类型冲突/Plugin 字段依赖 → Plugin/Custom API → 多语言/配置数据 → UAT 发布与导入历史 → 生产交接 → 归档收尾），附「历史事故↔检查项」对照表；使用时新建副本逐项打勾 |
| 规范固化 | `/skill:d365-deploy` 新增 5.3 节指向检查清单（每次发版必过） |


## 会话更新（2026-07-24）— 成交条件创建时「名称→GUID」自动解析（Excel 导入兼容）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-24 |
| 背景 | 用户通过 Excel 模板导入成交条件到 UAT，模板只填国家/产品分类**名称**，但自定义多选控件（`mcs_common.control.lookup.multiplechoice`，saveattribute=`mcs_countries`、entity=`mcs_country`、attribute=`mcs_name`）只按 GUID 显示 → 导入后表单国家不显示。UAT 实查：导入记录 `mcs_countryname`/`mcs_typename` 有值，`mcs_countries`/`mcs_trade_type`/`mcs_countrycode`/`mcs_typeid` 全空 |
| 方案（用户已确认） | `TradeStPayTermValidationPlugin`（Create/Update PreOp）在校验前新增 `ResolveLookupNameToGuid`：GUID 字段为空且名称字段非空时，按名称精确匹配（Trim+忽略大小写，内存匹配，不用 In 查询）解析回填 GUID+编码；分隔符兼容 `,`，`，` `、`；NA 视为通配；**匹配不到 → 抛错阻断创建**（导入日志行级可见）；直传 GUID 不覆盖。国家：`mcs_countryname`→`mcs_countries`+`mcs_countrycode`（匹配 `mcs_country.mcs_name`）；产品分类：`mcs_typename`→`mcs_trade_type`+`mcs_typeid`（匹配 `mcs_trade_pttype.mcs_trade_pttypename`） |
| 本地改动 | `Code/Customizations/Plugins/TradeStPayTerm/Validation/TradeStPayTermValidationPlugin.cs`（+解析方法；**命名空间已从远程改回本地 `SanyD365.Plugins.TradeStPayTerm`**——与 sync-plugin-to-remote.py 第 53 行 NAMESPACE_MAP 约定一致，同步时自动转远程命名空间，同时避免临时 Assembly 与主 Assembly 类型全名冲突） |
| DEV1 部署 | 临时 Assembly `SanyD365.Plugins.TradeStPayTerm`（ID `2f7a602c-1187-f111`）已更新为正式版代码；Type `name=TradeStPayTermValidationPlugin_Temp`（避开主 Assembly 同名备用键 2601）；Create/Update PreOp Steps 已注册（Step `507984c1`/`537984c1`）。**⚠️ 测试结束后必须注销（红线）** |
| 工具扩展 | MetadataTool 新增：`register-tradestpayterm-temp`（临时 Type 带后缀注册，绕 2601）、`test-tradestpayterm-resolve`（5 场景端到端测试+自动清理） |
| 测试状态 | 用户接管测试（"测试我来做"）。已验证：TC2 未知名称阻断 ✅、TC3 直传 GUID 不覆盖 ✅、TC5 NA 通配 ✅；TC1/TC4（南非+科特迪瓦多值）待复测 |
| ⚠️ 数据怪象（待用户找基础数据团队确认） | DEV1 记录 `mcs_country` `548f16fe`（KT）：工具 RetrieveMultiple 读到 `科特迪瓦`，但插件管线内 Retrieve 读到 `Kertdiva`（6/25 旧成交条件记录 countryname 也是 Kertdiva）。同一记录两条读路径结果不一致，疑似 MDM 同步回写/读副本延迟。测试请避开该条或先用南非等稳定名称 |
| 测试注意 | 1. 主 Assembly 的 Validation Steps 仍在跑（老逻辑无解析），两个 Assembly 同时触发；用**独立 BU 编码**测试，避免主插件空维度通配误报重复<br>2. 重复校验在解析之后执行，导入重复数据会按解析后 GUID 交集拦截 |
| 下一步 | 1. 用户 DEV1 验证（含模拟 Excel 导入）<br>2. 通过后：sync-plugin-to-remote.py 同步远程 → 用户授权推分支 → PR 合并 → 重编译更新 DEV1 主 Assembly → 注销临时 Assembly<br>3. UAT 存量导入数据回填（`mcs_countries`/`mcs_trade_type` 等 4 字段，需用户单独授权） |
| 远程同步（2026-07-24） | ✅ sync-plugin-to-remote.py 同步+远程编译通过（仅原有警告）；DLL 拉回 `/tmp/SanyD365.D365Extension.Sales.dll`<br>✅ 分支 `uat-20260724-peter-tradestpayterm-name-resolve` 已推送（commit `3c2aa1539f6`，仅 1 文件 +121 行；推送前已核实其他改动均为落后本地 uat 的假象及他人新提交，未纳入）<br>✅ **PR 6024 已合并到 `uat`**（merge commit `767c0925d8f`）<br>✅ 已拉最新 uat 重编译（拉取前丢弃了两个 csproj 的本地改动=过时同步残留；注意本地 uat 落后 439 个提交，教训：同步前先 pull）<br>✅ DEV1 主 Assembly `SanyD365.D365Extension.Sales` 已更新（ModifiedOn `2026-07-24 04:27:19`，无类型差异错误）<br>✅ 主 Assembly 端到端验证通过（阻断/不覆盖/NA 通配均正确；TC1/TC4 仅受“科特迪瓦/Kertdiva”数据怪象影响）<br>✅ **临时 Assembly `SanyD365.Plugins.TradeStPayTerm` 已注销**（Step×2/Type/Assembly 全清，红线执行完毕）<br>⏸️ 待办：UAT 存量数据回填（需授权）；用户 n8n 发布 `McsPlugin` 到 UAT |


## 会话更新（2026-07-24 续）— 开发要求新增：新增组件必须主动通知用户

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-24 |
| 用户要求 | **每次代码新增了任何组件，必须通知用户，由用户加到对应的包里，以便后续发版时不会忘记** |
| 已固化位置 | 1. `AGENTS.md` 第 4 节红线（2026-07-24 新增条）<br>2. `/skill:d365-dev` 新增第 8.3.2 节（每次写代码必查）<br>3. 本 Memory 记录 |
| 执行要点 | AI 在完成汇报中显式列出新增组件清单（类型+名称+建议归属 Solution），提醒用户加包；漏报 = 发版漏组件 = 严重失误 |


## 会话更新（2026-07-24 续）— 国家名称双字段匹配 + 编码扩容 4 位序列号

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-24 |
| 问题1：UAT 导入按中文名匹配失败 | UAT `mcs_country` 为 MDM 同步基础数据，**部分记录 `mcs_name` 为英文**（Web API 实锤：`mcs_name="United Nations"` 而 `mcs_chinesename="联合国"`；欧盟/北约/科索沃/库拉索岛两个字段均为中文）。插件按 `mcs_name` 匹配中文名失败阻断；JS 同步文本字段也显示英文 |
| 修复1（小改动） | `TradeStPayTermValidationPlugin.ResolveLookupNameToGuid` 匹配字典改为 **`mcs_name` + `mcs_chinesename` 双字段都是匹配键**（新增 `altNameField` 参数，产品分类传 null 不变）。DEV1 验证原有场景不受影响（"科特迪瓦"怪数据除外，属环境问题） |
| 问题2：当天编码超 99 条 | AutoNumber 规则 `TC+YYMMDD+2位`=10 位（字段 MaxLength 也是 10），当天上限 99，UAT 导入测试撞破 |
| 修复2（用户确认 4 位） | `TradeStPayTermAutoNumberPlugin`：序列号 D2→**D4**（上限 9999/天），解析改为取「TC+日期(8位)」之后部分并内存取 max（避免字符串排序 2/4 位混排坑），兼容存量 2 位编码；字段 `mcs_trade_stpaytermname` MaxLength **10→12**（DEV1 已改并发布；Definitions JSON 已同步） |
| 工具扩展（用户批准） | `D365ToolCommon.Metadata.MetadataFieldService.UpdateStringMaxLength`（照 UpdateMoneyRange 模式，MergeLabels=true）+ MetadataTool `update-field-maxlength <实体> <字段> <长度>` |
| 远程同步 | ✅ 编译通过；分支 `uat-20260724-peter-tradestpayterm-v2` 已推送（commit `c5e5bfdb626`，2 文件 +46/-34）<br>✅ **PR 6025 已合并到 `uat`**（merge commit `2659ffacfde`）<br>✅ 拉 uat 重编译 + DEV1 主 Assembly 已更新（ID `9d6ff315`，ModifiedOn 2026-07-24）<br>✅ DEV1 验证 **5/5 全过**：4 位编码 `TC2607240013~0016`（从存量 2 位序号连续递增）；双字段匹配连“科特迪瓦”也经 `mcs_chinesename` 匹配成功（顺带解决了 DEV1 怪数据）<br>✅ **临时 Assembly 已注销**（Step×2/Type/Assembly 全清，红线执行完毕）<br>⏸️ 待用户发 UAT：实体包（字段长度）+ McsPlugin |
| 下一步 | PR 合并 → 拉 uat 重编译 → 更新 DEV1 主 Assembly → 验证（名称解析 + 4 位编码）→ 注销临时 Assembly → 用户发 UAT：**实体包（字段长度）+ McsPlugin** 都要发 |
| 环境知识沉淀 | `mcs_country`（MDM 同步）：`mcs_name` 可能为英文，**中文名锚点字段是 `mcs_chinesename`**（UAT 282 条仅 12 条为空）；读取路径间存在不一致（SDK RetrieveMultiple 与 Web API 结果可能不同），涉及该实体名称匹配一律双字段兜底 |
| 可选小改（暂缓） | JS/Custom API 显示优先取 `mcs_chinesename`（用户决定先不做，流程优先） |


## 会话更新（2026-07-24 续）— UAT WebResource 层排查 + 语言包 key 丢失（挂起待处理）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-24 |
| 起因 | 用户发现 UAT `mcs_trade_stpayterm.js` 存在 Active（非托管）层，询问来源 |
| 排查结论 | 1. Active 层由集成账号 `# D365AppUserForD365ClientAPI` 于 7/24 04:22:23 直写产生（与 04:22 McsWebResource 导入几乎同时）；**仅这一个文件**<br>2. 其余 22 个自有 JS/HTML 内容与 DEV1 全部一致；`mcs_trade_stpayterm.js` 当前生效内容也已恢复与 DEV1 一致（14:17 重发 McsWebResource 后）<br>3. ⚠️ 流程性信号：7/22 07:38-07:40 `# D365 Admin` 直写了全部 21 个 WebResource（时间对不上任何导入）→ UAT 存在"直连 API 直写 WebResource"的环节，是产生 Active 层、遮盖托管发版的根源，待与三一确认 |
| 发现：UAT 语言包第 4 次被覆盖 🚨 | UAT `ms_languagefile_1033/2052`（5604/5593 keys）中**我们的 352 个 key 全部丢失**（与之前三次同因：其他 Solution 整包覆盖）。当前 UAT 内容已备份至 `/tmp/uat_wr/ms_languagefile_1033/2052` |
| 恢复预案（待用户授权） | 以本地 `Code/Customizations/WebResources/Language/1033.json`/`2052.json`（352 keys）为准，**只追加不覆盖**；执行后逐条核对 |
| 状态 | ⏸️ 用户决定暂不处理，后续再回复 |


## 会话更新（2026-07-24 续）— 融资管理 Bug 修复（级联带出 + 必填修正 + 字段关联纠错）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-24 |
| Bug 来源 | UAT 测试反馈：1. 选线索/报价单/合同后页面其它字段不带出（要求级小的代入上级、其它信息按选择代入、可修改）<br>2. 新建保存被「融资产品/产品名称/回购条件/其它条件」必填报错阻断（PRD：融资需求阶段必填仅 编号/状态/子公司/客户编码/客户名称） |
| 🚨 字段关联纠错（2 处） | 1. **报价单**：`mcs_quote_id` 错关联标准 `quote`，实际应为配置报价体系的 **`mcs_quoter`**（报价单，编号 `BL-Q-...-NN`；其父 `mcs_quote_main` 配置报价器含线索/客户/国家）<br>2. **线索**：`mcs_lead_id` 错关联标准 `lead`（L 前缀），业务实际用 **`mcs_leadmain`**（LDCRM 前缀；报价单链/合同链的线索均指向它，与标准 lead 无任何关联字段） |
| 新红线（已固化） | **「后续所有有问题的字段都不允许删除，只能新建」**——已追加到 `/skill:d365-dev` 第 10 节（含背景：80041A06 生产导入事故）。本次 2 个错关联字段均保留不删，新建替代字段，旧字段由用户后续从表单移除 |
| 新建字段 | `mcs_fsm_data.mcs_leadmain_id`（线索编号→mcs_leadmain）<br>`mcs_fsm_data.mcs_quoter_id`（报价单编号→mcs_quoter）<br>均已 `set-field-label` 双语、发布；`entity_20260713` 与主清单均为 rcb=0（含全部子组件），**自动随包无需手动加** |
| 关键元数据事实（实查） | • `mcs_quoter`→`mcs_quote_main`（`mcs_quote_mainid`）→线索（`mcs_leadmainid`→mcs_leadmain）、客户、国家；quoter 自带 `mcs_customercode`（客户编号）<br>• `mcs_contract`：线索=`mcs_leadmain`、大区=`mcs_region`、国家=`mcs_country`、事业部=`mcs_bu`、买方=`mcs_customermaster`（→mcs_customermasterdata）；**合同上无国区字段、无报价单关联**<br>• `mcs_leadmain`：国家=`mcs_countryid`、事业部=`mcs_buid`、客户=`mcs_customermasterdataid`、客户编码=`mcs_accountnumber`；大区（`mcs_regionbuid`→mcs_regionbu）/国区（`mcs_regionid2`→mcs_region）与 fsm 目标实体不匹配<br>• 主数据：`mcs_region`=大区、自营区；`mcs_nationalregion`=国区；`mcs_regionbu`=大区事业部（三套不同概念） |
| 级联带出（已实现） | `mcs_fsm_data.js` 新增 `FsmDataForm.onLoad`：三来源 onChange **全量重算**派生字段（优先级 合同>报价单>线索）：<br>• 选线索：国家/事业部/客户名称/客户编码（`mcs_accountnumber`，sapnumber 兜底）<br>• 选报价单：**代入线索** + 国家/客户名称/客户编码（`mcs_customercode`）<br>• 选合同：**代入线索** + 大区/国家/事业部/客户名称（买方）/客户编码（买方→sapnumber）<br>• 报价单/合同弹窗按已选线索过滤（addPreSearch+addCustomFilter，quoter 经 quote_main 关联过滤）<br>• 清空联动：来源清空时派生字段全清；代入的线索用 `_lastDerivedLeadId` 跟踪，用户手工改过的线索不覆盖/不清除<br>• 按用户决策**不带出**：国区/子公司/设备台数/产品名称/融资金额（来源不确定）；合同→报价单留空（无关联字段） |
| 保存校验（已实现） | 1. 三来源至少填一个（否则拦截）<br>2. **重复性校验**：三者任一相同存在即重复（onSave preventDefault → 异步查询 → 无重复 `_saveApproved` 放行，避免死循环；提示已有记录编号） |
| 必填修正（已实现） | • 20 个后续阶段字段降 RequiredLevel=None（融资金额/币种/期限/首付比例/利率/融资产品/设备台数/产品名称/融资经理/是否立项/2 个可提交标记/金融产品名称/授信金额/授信USD/贴息/融资费用/回购条件/其它条件/是否有效）<br>• 补齐 3 个缺失必填：`mcs_fsm_no`/`mcs_fsm_status`/`mcs_customer_name`（DEV1 原为 None，与 Definitions 不一致）→ 现 5 个必填=编号/状态/子公司/客户编码/客户名称，与 PRD 一致<br>• 提交前置校验：立项审批=融资六要素 8 字段必填+**融资经理自动取登录人**；方案审批=全部字段+合同号必填（缺字段用控件 label 提示，自动随 UI 语言） |
| 修改文件 | `Code/Customizations/WebResources/JS/mcs_fsm_data.js`（+级联/校验约 280 行）<br>`Code/Tools/MetadataTool/Definitions/mcs_fsm_data.json`（+2 字段，required 同步）<br>本地语言包 `1033.json`/`2052.json`（+3 key：FsmData_RequireOneSource/DuplicateSource/RequiredMissing）<br>DEV 语言包 `ms_languagefile_1033/2052`（导出→只追加 3 key→更新，5604→5607/5593→5596）<br>DEV1 表单 XML（formLibraries+onLoad 绑定+新行插入 2 个新字段控件；旧字段控件未动）<br>`Code/INDEX.md`（融资管理章节追加功能点）<br>`/skill:d365-dev`（第 10 节新红线） |
| 已验证（只读） | ✅ 2 新字段创建+双语标签 ✅ 必填 5 个 ApplicationRequired、20 个 None ✅ 表单 XML 含 1 formLibrary+1 onLoad Handler+新字段控件 ✅ JS node 语法检查 ✅ 实体已发布 ✅ JS 已更新发布（24164 bytes） |
| 待用户验证（DEV1 UI） | 1. 选线索/报价单/合同的带出与代入 2. 弹窗按线索过滤 3. 清空联动 4. 全空/重复保存拦截 5. 保存不再弹 4 个误必填 6. 表单上新旧两对字段并存，旧字段（`mcs_lead_id`/`mcs_quote_id`）由用户验证后自行移除 |
| UAT 发布内容（待用户 n8n） | `entity_20260713`（2 新字段+必填变更+表单）+ `McsWebResource`（mcs_fsm_data.js + 语言包） |
| 下一步 | 1. 用户 DEV1 UI 验证（上述 6 项）<br>2. 验证通过后用户移除旧字段表单控件、按需 n8n 发 UAT<br>3. 国区/子公司/设备台数/产品名称/融资金额带出待业务确认来源后补充 |


## 会话更新（2026-07-24 续）— 融资管理级联带出联调排障 + 国区映射打通（里程碑：级联全通）

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-24 |
| 最终状态 | ✅ **级联带出全部打通**（用户实测确认：选合同后 线索代入/大区/国家/国区/事业部/客户名称/客户编码 全部带出）；仅「子公司」因无来源字段待业务确认 |
| 🚨 排障 1：onLoad 根本不触发（最大坑） | 根因：`mcs_fsm_data` 原表单在**附件 WebResource 控件的 cell 里**自带一个空 `<events><event name="onload" active="false"/></events>`（单元格级事件），我首次绑定时把 `FsmDataForm.onLoad` Handler **误写进了这个 cell 级 events**（路径 `/tabs/.../row/cell/events`），表单级 onLoad 从未触发。<br>正确结构（对齐 `mcs_fsm_resource`）：`<formLibraries>` 之后、`<tabs>` 之前放**表单级** `<events>`（`<form>` 直接子节点）。<br>**教训：改表单 XML 事件绑定前，必须先用 DOM 解析确认 events 块的父节点层级（form 级 vs cell 级），不能只做字符串替换。** |
| 排障 2：代入线索显示「（无名称）」 | setValue lookup 时 name 传空串导致；修复：代入时取源记录的格式化名称（`_xxx_value@OData.Community.Display.V1.FormattedValue`，Xrm.WebApi.retrieveRecord 默认返回） |
| 排障 3："改完后什么都带不出" | 虚惊：用户测试时选的是旧脏数据合同 `SY20230029`（大区/事业部/买方/线索全 null），只有国家有值→只带出了国家；控制台 `[FSM]` 诊断日志证明链路正常。教训：**DEV1 旧记录大多缺字段，测试必须用专门造的全字段数据** |
| 国区解决方案（用户提示后台有关联配置） | 实体 **`mcs_bunationalcountry`（大区-国区-地区 / BU-National-Country）**：`mcs_countryareacode`（国家 2 位代码）→ `mcs_nationalregioncode`（国区编码）→ `mcs_nationalregion`（国区主数据）。JS 新增 `deriveNationalRegion`：任何来源带出国家后自动推导国区（`statecode=0` 生效记录），国家清空时国区联动清空。实查验证：阿尔及利亚 DZ → N020 → 阿尔及利亚国区 ✅ |
| 主数据速查 | `mcs_country.mcs_countrycode`=2 位国家代码、`mcs_code3`=3 位；`mcs_bunationalcountry` 共 193 条生效映射；`mcs_nationalregion.mcs_nationalregioncode`=国区编码 |
| 子公司（待业务确认） | 线索/客户主数据/合同全字段排查**均无"子公司名称"字段**；唯一候选=「大区-国区-地区」映射的 国区对应组织名称/大区对应组织名称（组织编码 99990001/52706884 系列，疑似法人公司）。已给用户 3 个选项：A=映射组织名称推导 / B=指出具体字段 / C=手工填写 |
| 测试数据（DEV1，FSMTEST 前缀） | 线索 `LDCRM202607240001`（23c8c005，国家/事业部/客户/编码全）<br>报价主表 `FSMTEST-QM-01`（0f25a519）→ 报价单 `FSMTEST-QM-01-01`（97546931，国家/客户/编码）<br>合同 `FSMTEST-合同-01`（a7546931，线索/大区/国家/事业部/买方全）<br>均为 LTC客户-1（BMW0001）；造数时发现 `mcs_leadmain` 有 AutoNumberOnLeadMain 插件（强制线索渠道 picklist 429700000=D365 等、LDCRM 重命名）、`mcs_quoter` 自动按「主表编号-序号」重命名 |
| 收尾 | ✅ 诊断 `console.log` 已清理，正式版已发布（26673 bytes）；console.error 异常日志保留 |
| 遗留验证项 | 1. 保存校验：三来源全空拦截、重复性校验拦截（用户未测）2. 提交立项/方案审批的分阶段必填校验（用户未测）3. 子公司来源确认后补充带出 |


## 会话更新（2026-07-24 续）— 融资管理 BPF 创建并入包 + 报价单弹窗过滤修复

| 项目 | 内容 |
|---|---|
| 日期 | 2026-07-24 |
| BPF（用户创建） | 名称「融资管理」，uniquename=`mcs_fmprocess`，主实体 `mcs_fsm_data`，category=4，已启用<br>• BPF 流程定义 workflow ID `18c5e92c-5587-f111-ab0f-7ced8db4d37f`<br>• BPF 实体 `mcs_fmprocess`，MetadataId `99a3769a-5687-f111-ab0f-7ced8de4eab4` |
| 入包（用户授权 AI 代加） | ✅ BPF 流程定义（type 29）→ `entity_20260713` + 主清单<br>✅ BPF 实体（type 1）→ `entity_20260713` + 主清单<br>✅ 组件源清单 `allcomponent-peter.json` entities 20→21（追加 mcs_fmprocess；⚠️ 清单工具暂不支持 workflows 组，BPF 流程定义靠本次直加记录在此） |
| 报价单弹窗过滤修复 | 问题：选线索后开报价单弹窗报 `0x80041103 查询生成器错误`——lookup 控件 `addCustomFilter` 对 link-entity 跨实体联接兼容差（标准 Web API fetch 有效但控件内失败）<br>修复：选线索时后台查出其报价主表 ID 列表缓存（`_quoteMainIdsForLead`），弹窗过滤改用扁平 `mcs_quote_mainid in (...)` 条件，避开 link-entity；无线索报价主表时显示空结果<br>✅ 已发布并验证扁平 fetch 有效（过滤后正好返回 FSMTEST-QM-01-01） |
| 经验沉淀 | **lookup addCustomFilter 避免使用 link-entity**：改用「先查 ID 列表缓存 + 扁平 in 条件」模式 |
| 状态 | ✅ 级联带出/弹窗过滤/清空联动/重复校验/分阶段必填/BPF 全部就绪；仅剩「子公司」带出待业务确认来源 |

## 会话更新（2026-07-26）— mcs_fsm_resource 融资资源编号自动编号配置（DEV1）

| 项目 | 内容 |
|---|---|
| 背景 | 融资资源管理表 `mcs_fsm_resource` 的融资资源编号 `mcs_fsm_resource_no`（规则 FSMR+YYYYMMDD+4位序列号）一直未配置自动编号；此前仅 `mcs_fsm_data.mcs_fsm_no`（FSM 前缀）由用户配置 |
| 执行前检查 | `list-number-configs mcs_fsm_resource` 确认 DEV1 无配置；全仓无 FSMR 编号生成代码 |
| 配置（AI 经用户授权执行） | ✅ 编号配置 `ms_numbergenerateconfiguration` ID `313cbcfc-c088-f111-8077-7ced8de4eab4`：前缀模板 `FSMR{$datetimeformat(false,yyyyMMdd)}`、数字模板 `{$prefix()}{$serialno()}`、4 位、起始 1、用序列号服务<br>✅ 通用编号 Step `EntityValidateCreateForGenerateNumber: Create of mcs_fsm_resource`（PreValidation/同步，Assembly `SanyD365.D365Extension`）Step ID `bb057e03-c188-f111-8077-7ced8db4dda8` |
| 验证 | `test-number-config mcs_fsm_resource` 生成 `FSMR202607260001` ✅（测试记录已删）；为此给 MetadataTool `BuildTestEntity`/`GetDefaultNumberAttribute` 增加了 mcs_fsm_resource 用例 |
| 入包（用户授权 AI 代加，2026-07-26） | ✅ Step（type 92）→ 主清单 `AllComponent_Peter_NoUAT`（solutioncomponent `fdee9fd6-c188-f111-8077-7ced8de4edac`）<br>✅ Step（type 92）→ `McsPlugin`（solutioncomponent `158bbad7-c188-f111-8077-6045bd1c0cde`，符合「McsPlugin 只放 Plugin/Step」红线）<br>✅ `check-solution-deps McsPlugin`：PluginType `MSLibrary.D365.Common.Plugins.EntityValidateCreateForGenerateNumber` 判定为共享系统组件（⏭️ 可忽略），导入 UAT 无依赖风险 |
| UAT 配置（AI 执行，2026-07-26） | ✅ UAT 已创建编号配置 `05d247e3-c188-f111-8077-7ced8de50adc`（参数与 DEV1 完全一致）；**UAT 未手动注册 Step**（按 fca_quotaapp 先例，Step 随 McsPlugin 发布自动就位） |
| 待办 | 用户通过 n8n Release Tool 发布 `McsPlugin` 到 UAT 后，即可在 UAT 测试融资资源编号自动生成 |

## 会话更新（2026-07-26 续）— 厂端授信台账/模型版本 UAT 自动编号补齐

| 项目 | 内容 |
|---|---|
| 背景 | 用户发现厂端授信管理台账 UAT 无自动编号、DEV1 有；排查确认 `mcs_fca_records`（FCR+日期+5位）和 `mcs_fca_mdlversion`（V+日期无序列号）两项配置 2026-06-30/07-01 仅建在 DEV1，Step 未入主清单/未入 McsPlugin，历次发版从未带到 UAT |
| UAT 配置（AI 执行，用户授权） | ✅ `mcs_fca_records.mcs_recordid`：配置 `0975e381-c788-f111-8077-7ced8de4a9bc`（FCR+日期+5位，起始1，走序列号服务，参数与 DEV1 严格一致）<br>✅ `mcs_fca_mdlversion.mcs_versionid`：配置 `527bab80-c788-f111-8076-00224858fd02`（V+日期，序列号长度0，数字模板 `{$prefix()}`） |
| Step 入包（AI 执行，用户授权） | ✅ `Create of mcs_fca_records`（`11de51c5-f674-f111-ab0e-6045bd1c0cde`）→ 主清单（`f7423e8a`）+ McsPlugin（`e6f3c98a`）<br>✅ `Create of mcs_fca_mdlversion`（`0cde4e91-dd74-f111-ab0e-7ced8db4d37f`）→ 主清单（`4e0f858b`）+ McsPlugin（`1e14478a`）<br>依赖检查：PluginType 为共享系统组件可忽略，导入 UAT 无风险 |
| 经验沉淀 | **通用编号配置三要素缺一不可**：① 配置记录（数据，DEV/UAT 各建一次）② Step 注册 ③ Step 入 McsPlugin+主清单随包发布。早期配置只做了①②导致 UAT 缺失；今后配置编号时按 2026-07-26 fsm_resource 模式一次做全 |
| 待办 | 用户 n8n 发布 McsPlugin 到 UAT 后，fca_records / fca_mdlversion / fsm_resource 三个编号在 UAT 同时生效 |

## 会话更新（2026-07-26 续）— mcs_fca_proc.mcs_creditreject 选项标签修复（2052 一直是场景1-4）

| 项目 | 内容 |
|---|---|
| 现象 | 用户发现「不予授信」多选字段显示场景1/2/3/4，手动在 Maker 门户改完保存又"变回"场景1-4 |
| 根因 | 2026-07-01 的标签更新实际写入了 **1033**（Memory 当时误记为 2052），**2052 从字段重建起一直是场景1-4**；Maker 门户编辑选项标签写的是环境基础语言 1033，所以用户手动改只动了 1033，中文 UI 读 2052 永远显示场景1-4——不是被还原，是 2052 从未改成功 |
| 修复 | ✅ `update-optionset-labels mcs_fca_proc mcs_creditreject ... 2052` + 发布：1客户被列入征信黑名单 / 2近3年与我司产生≥100万元人民币实质性坏账 / 3实际逾期账账龄≥6个月且（逾期金额/在外货款余额）>50% / 4集团黑名单客户<br>✅ `Definitions/mcs_fca_proc.json` options 同步改为业务文本，防止重建回退 |
| 经验沉淀 | **选项标签更新后必须用 langId=2052 验证**（query-optionset-labels 默认显示可能误导）；Maker 门户编辑选项标签只写基础语言 1033，改中文标签必须用工具显式指定 2052 |
| 待办 | 1. UAT 的该字段选项同样是场景1-4，随下次实体包（entity_20260726_peter 或后续）发布带过去；1033 目前存的是中文文本（用户手动改的措辞与截图略异：大于等于/大于），如需英文翻译另行确认 |

## 会话更新（2026-07-27）— 成交条件批量按钮弃用现代副本，回归 SDK 经典按钮（含勾选消失修复）

| 项目 | 内容 |
|---|---|
| 背景 | 用户发布 `entity_20260726_peter` 时通过门户「添加现有命令」加按钮，被平台拖入 330+ 组件（组件库/应用/站点地图/324 表/14 仪表板）——坐实 2026-07-25 结论：Command Designer 现代副本依赖 App 级 DefaultCommandLibrary，不可跨环境打包。用户决定**彻底弃用现代按钮** |
| 现代副本删除 | ✅ 用户已在 DEV1 Command Designer 手动删除 3 个现代副本：`cr0c0__mcs_trade_stpayterm_apply/approve/reject!cra80_SanyOverseasCRM!...`（GUID `53754164`/`777888f1`/`d68679d8`）。⚠️ 另有 2 个 `cr0c0__` 按钮（`mcs_sales_calc_cost`、`mcs_order`）属他人资产，未动 |
| 经典按钮参数修复（AI 执行，用户授权） | ✅ 3 个经典批量按钮 `onclickeventjavascriptparameters`：`[{"type":5}]` → `[{"type":23},{"type":12}]`（DeployTool `update-appaction-params`，幂等），对应 JS 签名 `TradeStPayTermGrid.apply/approve/reject(selectedIds, selectedControl)` |
| 勾选消失根因（实测锁定，有微软官方出处） | **微软官方 KB 4481268「设计使然」**（[官方文档](https://learn.microsoft.com/en-us/troubleshoot/dynamics-365/sales/button-in-command-bar-not-appear-after-grid-item-selection)）：UCI 网格勾选记录后只显示 item-specific 按钮，判定标准=命令是否带选中计数规则；与 JS 参数无关（参数 23+12 的按钮照样消失，无缓存浏览器实测）。命令组件库不可跨环境（[官方限制](https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/command-designer-limitations)：命令库绑定 App/每 App 仅允许一个/app element 不进 Solution）；N:N 规则关联不随包（[appaction 表参考](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/appaction)：`appaction_appactionrule_classicrules` IsCustomizable=False）。**失败路径存档**：① 第三方创建 `appactionrule` 被平台硬禁（报 "App Action rules should define only under Microsoft First party Solutions"，传不传 SolutionUniqueName 都一样）；② 直接写 `visibilityformulafunctionname` 字段无效（Power Fx 公式必须注册进 App 命令组件库才求值） |
| 勾选消失最终修复（AI 执行，已 DEV1 验证） | ✅ **复用第一方规则** `msdyn_Mscrm.SelectionCountAtLeastOne!0`（`f39219dc-47e2-493e-89a7-5903a569f97d`，⚠️ 规则 ID 各环境不同：UAT=`3d0c1736-b17b-4750-8e6a-073ab71ebe89`，命令已改为按 uniquename 反查，禁止硬编码 GUID）：`Associate` 消息关联 `appaction_appactionrule_classicrules`（直接 Create 交叉实体不支持）+ `visibilitytype=2`。DeployTool 新命令 `attach-selection-rule <按钮uniquename>`。效果：未勾选隐藏、勾选显示，DEV1 Playwright 无缓存实测 3 按钮全部正确，JS 链路（type 23+12→确认框）同步验证通过 |
| 缓存大坑 | 命令栏元数据客户端缓存在 **Cache Storage（`https://uci-state/` 等）+ Service Worker**，硬刷新/Cmd+Shift+R **无效**，必须「清除站点数据」（F12→应用程序→存储→清除网站数据）。本次多次“改了没反应”均为缓存假象（服务端数据早已正确） |
| 刷新功能验证（#1160 收尾） | ✅ **经典按钮上下文下 type=12（SelectedControl）同样能拿到带 `refresh()` 的列表控件**，局部刷新生效：DevTools 日志 `selectedControl.refresh() 已调用` + 遥测 `page_list_load_time`（列表重新拉取 121ms）双重证实；`refreshGrid` 代码无需任何改动（曾临时加诊断日志，验证后已还原并重新部署发布）。实测记录 TC26072411：批量审批 待审批→生效 + 列表自动刷新。用户此前“执行完不刷新”是其浏览器缓存了 type=5 旧参数所致 |
| 代码固化 | ✅ `AppActionDeployer.CreateButton` 新增可选参数 `jsParameters`（默认 PrimaryControl），`DeployTradeStPayTermButtons` 的 3 个批量按钮传 gridParams（23+12），防止重建回退；删除已过时的「SelectionCount Display Rule 实验」调用（方法本体保留）<br>⚠️ `DeployTradeStPayTermButtons` 仍是「先删后建」模式，重跑会换 GUID、导致 Solution 中按钮引用悬空，非必要不重跑 |
| 最终状态（DEV1） | 仅 4 个经典按钮：clone（表单，type=5）+ apply/approve/reject（列表，type=23+12），VisibilityType=0 始终显示；未勾选点击由 JS 弹「请至少选择一条记录」兜底；批量操作成功后 `selectedControl.refresh()` 局部刷新列表（#1160 能力已随参数平移到经典按钮） |
| 发布结论更新 | 经典按钮的 JS 参数是 appaction 记录数据，**随 Solution 包一起走**——`entity_20260726_peter` 只需含 4 个经典按钮，UAT 导入后无需再手动同步参数（2026-07-25「UAT 手动加 SelectedControl」的手动步骤对经典按钮作废）。⚠️ 组件库仍绝对不可入包 |
| 纪律 | **禁止在 Command Designer 编辑这 4 个按钮**——一编辑就会再生成 `cr0c0__` 现代副本并拖组件库，前功尽弃；改参数/显隐一律走 DeployTool |
| 待办 | 1. 用户自己浏览器需「清除站点数据」后才能看到修复效果（硬刷新无效）<br>2. UAT 侧的 3 个现代副本（本地独立副本）需在 UAT Command Designer 手动删除，否则 UAT 会新旧两套按钮并存<br>3. **发版验证点（2026-07-27 已实测定论）**：`appaction_appactionrule_classicrules` 关联关系**不随 Solution 导入**——UAT 侧 3 个按钮 visibilitytype=2 随包到达但无规则关联，实测表现为「未勾选显示、勾选消失」（KB 4481268 默认行为）。UAT 需对 3 个按钮重跑 `attach-selection-rule`（第一方规则各环境 ID 不同，命令按 uniquename 反查）；新增 MetadataTool 只读命令 `list-appaction-rules [前缀]` 可逐环境核对规则关联；UAT 无 `cr0c0__` 现代副本残留<br>4. ✅ **UAT 已修复并关闭（2026-07-27 下午）**：3 个按钮已在 UAT 关联 `SelectionCountAtLeastOne`（UAT ID `3d0c1736`），回读核对与 DEV1 一致；用户清除站点数据后 UAT 验证通过；发版检查清单已加 5.5/6.4 项（生产发布必跑） |


## 会话更新（2026-07-27 深夜）— 批量按钮显隐随包化：appaction 改 RibbonDiffXml（方案 A 落地）

| 项目 | 内容 |
|---|---|
| 背景 | 上午用 `attach-selection-rule` 修复 UAT 按钮显隐后，用户提出生产发布约束：**客户 IT 不能（也不允许）直连修改生产**，而 N:N 规则关联不随 Solution 导入（上午已实测+官方 `IsCustomizable=False` 佐证），每个环境手工补关联不可持续。用户决策：RibbonDiff 禁令非客户要求，解除，3 个批量按钮改用 RibbonDiffXml |
| 全量按钮审计结论 | DEV1 全部按钮逐一核对（visibilitytype + visibilityformulafunctionname + 规则关联）：**仅 3 个 `mcs_trade_stpayterm` 批量按钮有显隐传输问题**；clone、fsm_data×2、fca_quotaapp【提交】、credit_record×6 全部 visibilitytype=0 无 Power Fx 无规则关联，随包正常，未动 |
| 官方依据（已查证） | ① KB 4481268 勾选消失 by design（[troubleshoot 文档](https://learn.microsoft.com/en-us/troubleshoot/dynamics-365/sales/button-in-command-bar-not-appear-after-grid-item-selection)）② 命令组件库不可跨环境（[command-designer-limitations](https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/command-designer-limitations)：命令库绑 App/每 App 仅一个/app element 不进 Solution）③ [appaction 表参考](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/appaction)：`appaction_appactionrule_classicrules` IsCustomizable=False 不随包 ④ 第三方禁建 appactionrule（仅实测报错，无官方文档） |
| 实施 | 1. 新建 `Code/Customizations/Ribbon/mcs_trade_stpayterm.ribbon.xml`：3 按钮 CustomAction（Location `Mscrm.HomepageGrid.mcs_trade_stpayterm.MainTab.Management.Controls._children`，TemplateAlias o1）+ 内联 `SelectionCountRule Minimum=1` + 1033/2052 LocLabels + CrmParameter `SelectedControlSelectedItemIds`/`SelectedControl`（JS 零改动）<br>2. 导出 DEV1 `entity_20260727_peter` → 合并 Ribbon 片段进实体节点 → 用户 UI 回导 DEV1（非托管）<br>3. DeployTool 新增 `delete-appaction` 命令（先解除规则 N:N 关联再删，否则外键 `appaction_appactionrule_classicrulesOne` 冲突），删除 3 个旧 appaction<br>4. `remove-solution-component 10156 <id>` 从 `entity_20260727_peter` 和主清单移除 3 个 appaction 组件（clone 保留） |
| DEV1 验证（Playwright） | ✅ 未勾选：无批量按钮 ✅ 勾选 1 条：3 个 ribbon 按钮出现于「更多命令」溢出菜单 ✅ 批量申请：确认框→申请成功→status 0→1→未生效视图自动移除记录（TC26072406）✅ 批量拒绝：确认框→拒绝成功→status 1→0→待审批视图自动移除记录 ✅ 测试数据已还原 ✅ 重新导出实体包回读：Ribbon 随实体导出、appactions 目录仅剩 clone |
| 已知差异 | **Ribbon 按钮在 UCI 落入「更多命令」溢出菜单**（旧 appaction 直接显示在命令栏）；经两轮实测（Sequence 100→10/11/12、补 ModernImage 图标）确认：**UCI 网格主命令栏只渲染现代命令，classic ribbon 自定义固定入溢出菜单，Sequence/图标不影响位置**（图标有效果：溢出菜单中带图标显示；Sequence 有效果：排在溢出菜单顶部）。最终形态：溢出菜单顶部 + 图标（r3），用户已确认接受 |
| 工具新发现 | **本机 pac CLI 可用**（`~/.dotnet/tools/pac`，已有 `peter_qiuzw` DEV1 认证 profile），`pac solution import` 可直接导 DEV1 非托管包——后续 ribbon 迭代无需用户 UI 导入；注意导入撞锁（他人包在导）会报 `Cannot start another [Import]`，需错峰重试 |
| 关键经验 | 1. 合并 XML 时注释里含 `<RibbonDiffXml>` 字面量会导致正则误匹配（已修复，注释避免尖括号标签名）2. 删 appaction 前必须先 Disassociate 规则关联 3. appaction 组件 componenttype=10156 4. **UAT/生产发布路径**：实体包托管 Upgrade 导入 → 平台自动删除被移除的旧 appaction + 应用 Ribbon，零手动步骤 |
| 文档更新 | ✅ 发版检查清单 5.5/6.4 改写（attach-selection-rule 步骤作废，改 Ribbon 核对项）✅ d365-tools 第 4 章改写（App Action 优先、显隐随包用 Ribbon 的选型表）✅ d365-dev 与 Code/Customizations/AGENTS.md 的 RibbonDiff 禁令修订 ✅ Code/INDEX.md 登记 ribbon 文件 |
| 下一步 | 1. 用户 n8n 发布 `entity_20260727_peter` 到 UAT（托管 Upgrade）→ 验证按钮显隐与批量操作（清除站点数据后）<br>2. 生产发版按新检查清单执行（Ribbon 随包，无手动步骤）<br>3. 可选：评估溢出菜单位置是否接受，如需上命令栏需进一步调试 Location/Sequence |


## 会话更新（2026-07-30）— Kimi 使用环境与 Git 工作流约定

| 项目 | 内容 |
|---|---|
| 使用环境 | 用户现主要在 **Zed（ACP 集成 Kimi）** 使用 AI，VS Code 侧为官方 Kimi Code 插件 0.6.4；终端 kimi CLI 1.47.0。会话统一存 `~/.kimi/sessions/`（按工作目录 md5 分桶），跨端共享 |
| 当前模型默认 | `~/.kimi/config.toml`：`default_model = "kimi-code/k3-256k"`、`default_thinking = true`、`default_yolo = true`、`compaction_trigger_ratio = 0.85`。yolo 状态按会话持久化，旧会话需手动 `/yolo` 切换 |
| 本机 kimi-cli 补丁×2 | ① `acp/server.py` `list_sessions`：cwd 为空时走 `Session.list_all()`（修 Zed Import Threads 显示 No threads，等效官方未合并 PR #1957）② `new_session` 命令广播改用 `soul.available_slash_commands`（让 Zed 斜杠列表出现 `/skill:*`）。备份 `server.py.bak.pr1957`；⚠️ **kimi-cli 升级会覆盖补丁，需重打** |
| Zed 已知缺口（待官方） | 模型选择器不显示（zed#59096 类问题，改 `default_model` 兜底）；上下文占用率不显示（ACP 端丢弃 `StatusUpdate`，未实现 `usage_update`）；thinking 档位 Low/High/Max 仅 VS Code 插件私有，ACP 无接口；Zed 官方无中文 UI（社区 zed-i18n 未采用） |
| Zed 已配置 | `settings.json`：`agent.thinking_display = "always_collapsed"`（Thinking 块默认折叠）；Kimi 以 custom agent 注册（`~/.local/bin/kimi acp`） |
| **Git 工作流约定（用户确认）** | 本地 Mac SanYi = 个人仓库（origin=GitHub `SanYLocalCode`），客户仓库在 tx-windows，两边独立。**日常需求以同步 tx-windows（客户仓库）为主；功能测试 OK 后再最后提交个人本地仓库**。本地已删除 azure 远程（物理隔离误推）；历史事故=本地曾误同步客户 git，故此前一直不提交本地。AI 执行 git 变更前须复述目标远程+分支并经用户授权 |
| 根目录清理 | 12 张无引用截图+nul 移至 `Backups/TempTest/截图清理-20260730/`；`.playwright-mcp` 日志/快照已清空；保留 `bug874-fixed.png`/`bug1283-fixed.png`/`fca-guard-create-blocked.png`（禅道Bug修复记录引用，Bug 关闭后可清） |

## 会话更新（2026-07-30）— 融资管理 UAT 假提交卡死 Bug 排查与修复（FsmDataBppIntegrationPlugin 抛异常回滚）

| 项目 | 内容 |
|---|---|
| 起因 | UAT 测试人员反馈 FSM202607290001 提交立项审批后 BPP 无返回、审批字段全空，再次提交提示「已有审批在进行中」 |
| 根因 | 测试人员在「融资需求」阶段（mcs_fsm_status=1）点击【提交立项审批】：UAT 的 mcs_fsm_data.js 为旧版无状态前置校验，JS 直接写 mcs_bppstatus=2；后端 `FsmDataBppIntegrationPlugin` 检测「状态与审批类型不匹配」仅 trace 静默 return，不回滚 → 记录永久卡在"审批中"（UAT Plugin Trace 实锤） |
| 修复 | 3 个「不允许提交」分支改抛 `InvalidPluginExecutionException`（事务回滚 bppstatus，前端弹具体原因）；分支 `uat-20260730-peter-fsm-bpp-guard`（commit `9c193286f50`，1 文件 +8/-6）已合并 uat |
| DEV1 | ✅ 独立 Assembly 3 场景验证（临时子类避 2601，已注销/主 Step 已恢复/临时文件已删）✅ 主 Assembly 已更新（ID `9d6ff315`，8419 KB）✅ 主 Assembly 回归通过 |
| UAT 数据修复 | 已清空 FSM202607290001 的 mcs_bppstatus；修复后真实提交走通全链路：bppstartapi 成功 → workflowid=870642051797164032 → 审批链接/Submitted 回写（UAT Messagehandler 含 Handler["mcs_fsm_data"] 工作正常，此前担忧排除） |
| 遗留问题 | 1.「当前审批人」字段错位：表单绑 `mcs_nextapprover`、`BPPHandlerServiceForFsmData` 写 `mcs_bppapprover`（**用户已自行调整**）；且本次 GetCurrentApprover 未取到值（需在 BPP 门户核对实例审批人，区分 BPP 模板配置问题 vs GetNextApprover 调用问题）<br>2. ~~UAT mcs_fsm_data.js 为旧版~~ ✅ 已核实 UAT/DEV1/本地三方 MD5 一致（0b2081a2），新版含状态校验（他人发布 WebResource 时已带齐） |
| 工具新增 | MetadataTool 通用 `update-record <实体名> <GUID> <JSON>` 命令（BuildEntityFromJson 抽取共用，#optionset 支持传 null 清空） |
| 下一步 | ✅ 已完成：用户 n8n 发布 `McsPlugin` 到 UAT，UAT 回归通过（守卫场景拦截+回滚、主 Assembly Trace 实锤），Bug 已关闭；FSM202607290001 待测试人员在 BPP 门户通过/驳回验证回调 |

## 会话更新（2026-07-30 续）— 当前审批人字段错配修复（FsmData/FCA 对齐 BPP 框架 mcs_nextapprover）

| 项目 | 内容 |
|---|---|
| 根因 | BPP 框架 `BPPService.cs` 通用逻辑写死绑定 `mcs_nextapprover`（发起回写+结束清空，除 mcs_quoterdetail 外全实体），FsmData/FCA 表单「当前审批人」也绑该字段；但专属 Handler 与 D365 回调 Plugin 写的是 `mcs_bppapprover` → 当前审批人永远不显示 |
| 修复 | 4 文件 `mcs_bppapprover` → `mcs_nextapprover`：`BPPHandlerServiceForFsmData/FcaQuotaApp.cs`（发起+回调回写）、`FsmDataBppCallbackPlugin/FcaQuotaAppBppCallbackPlugin.cs`（撤回/废弃清空）；分支 `uat-20260730-peter-bpp-nextapprover-fix`（commit `dcdb08a5feb`）→ **PR 6415 已合并**（merge `31c3fe298b8`） |
| 验证 | ✅ DEV1 独立 Assembly 双模块验证（临时子类已注销、主 Step 已恢复、红线完毕）✅ DEV1 主 Assembly 已更新（`9d6ff315`，8429 KB）✅ 主 Assembly 回归 FSM+FCA 通过 |
| BPP 侧独立问题 | UAT 实证融资立项模板 `794612913352237105` 实例（FSM202607290002 / flowId 870693550988402688）`GetNextApprover` 返回空；同时段 mcs_contract_signing 提交审批人正常（liuy2905/lanl2）→ **模板首节点审批人规则未解析出人，属 BPP 团队配置问题**，D365 代码无问题 |
| 编译插曲 | uat 主干连续被同事提交打断：孟绥洪少逗号（自修）→ lius CS0023 `TimeSpan?`（苻坚 PR 6414 修复）；用户明确不动他人代码 |
| 下一步 | 用户 n8n 发布 `McsPlugin` + `Messagehandler` 到 UAT → UAT 验证（需 BPP 团队先修模板审批人解析） |
