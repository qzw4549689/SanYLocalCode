# D365 客户信用评估系统 — 项目记忆

> **项目：** 三一重工 D365 客户信用评估系统
> **技术栈：** Dynamics 365 (Dataverse) + C# Plugin + JavaScript WebResource
> **最后更新：** 2026-06-21（新增 Coface 汇率改用 D365 标准汇率评估）

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
    ├── Tests/                     # ⭐ 测试用例总集.md (104 条用例)
    │   └── BugReports/            # ⭐ 测试Bug反馈记录.md (8 个 Bug)
    ├── DailyReports/              # ⭐ 工作日报汇总.md (5 天记录)
    └── Methodology/               # D365+AI 实施方法论
```

### 2.0 AI 协作红线

> 详细规则见 `/skill:d365-deploy` 第 0 章。
>
> - **AI 代码提交铁律**：除非用户明确说出"提交"或"推送"二字，否则 AI 不得执行任何 Git 操作。
> - **McsPlugin 解决方案红线**：`McsPlugin` 里只能放 Plugin 和 Step，不能放实体/字段/WebResource 等其他组件。
> - **Git 推送约定**：默认推送到项目 Git（Azure DevOps `uat`），只有用户明确说 push 到个人 Git 时才推送到 `origin`。
> - **PublishAll 执行红线**：凡是需要 `PublishAll`（全局发布）的操作，由用户执行；AI 只运行指定实体的发布（如 `dotnet run publish mcs_customer_tag`），不运行无实体参数的 `dotnet run publish` 或等效全局发布命令。

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
| 文档 | `Documents/Planning/成交条件样板库_阶段1实施方案.md`<br>`Documents/Planning/成交条件样板库_阶段2-5实施方案.md`<br>`Documents/BusinessAnalysis/成交条件产品分类_示例数据.xlsx`<br>`Documents/DevelopmentStandards/三一D365开发规范文档.md` |
| Git 分支 | `uat-260621-peter-tradestpayterm`（已合并到 `uat`）<br>`uat-260623-peter-tradestpayterm-fix`（已合并到 `uat`）<br>`uat-260623-peter-tradestpayterm-api`（已推送到 origin，等待用户合并到 `uat`） |
| 关键确认 | 审批机制：D365 内批量按钮；客户等级：暂不参与查询/重复校验；重复校验：交集即重复；编码规则：`TC`+`YYMMDD`+2 位序号；产品分类 Lookup 引用 `mcs_trade_pttype` 而非 `mcs_productline`；默认 EUR 逻辑暂不修改；阶段 4 采用 Custom API（D365→D365 内部接口） |
| 待确认 | 1. 泵路事业部真实编码（当前代码用 `BU-1018` 占位）<br>2. 个人客户判断逻辑（客户主数据表无 `mcs_customertype` 字段） |
| 下一步 | 1. ✅ 用户已合并 PR `uat-260623-peter-tradestpayterm-api` 到 `uat`<br>2. ✅ 已在 DEV1 部署归并后的 `SanyD365.D365Extension.Sales` Assembly<br>3. ✅ 已用 MetadataTool 注册 Custom API 到 `McsCustomAPI` 解决方案并验证通过<br>4. ✅ 批量按钮隐藏 bug 已在 DEV1 修复并验证<br>5. ✅ UAT 已重新发布 `McsCustomAPI`（单独勾选），`mcs_QueryTradeStPayTerm` Custom API 调用验证通过<br>6. ⏸️ 注意 `Sany CRM Sales` App 的 Command Component Library 同步到 UAT（如需） |

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
| **开发计划** | `Documents/Planning/开发计划.md` | 功能清单 + 进度跟踪 + 功能-测试-Bug 关联总表（57 个功能映射 104 个用例） | 每完成一个功能 |
| **测试用例总集** | `Documents/Tests/测试用例总集.md` | 104 条测试用例，TC-{MODULE}-{NNN} 编号，含功能编号列 | 每新增/修改功能 |
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
TC-BASE   — 基础实体与字段测试
TC-ACC    — Account 表单逻辑
TC-ITEM   — 评分项目表逻辑
TC-ENUM   — 枚举值表逻辑
TC-CARD   — 评分卡配置表逻辑
TC-EVAL   — 评估记录表逻辑
TC-TAG    — 标签表逻辑
TC-SCCFG  — 评分卡配置数据测试
TC-EVCF   — 评估记录数据测试
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

**AI 约束（待写入 AGENTS.md）**：
> 在 `Code/Tools/` 下新增功能时，优先使用 `D365ToolCommon` 中的通用方法。如果通用方法不满足需求，先扩展通用方法，而不是在工具项目里临时写重复代码。

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
  - ✅ **财务指标国家编码对照表**：已完成（`mcs_coface_financial_indicator` 实体 + 21国84条数据 + `Urba360Parser` 重构）
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

**同步流程**：本机开发 → DEV 注册 → **DEV 测试确认（卡点，必须测试人通过）** → 改命名空间 → 复制到远程 → 远程编译(D365.sln, .NET 4.6.2) → Git提交(uat) → PR → Release Tool(n8n) → 部署

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

### 7.2.1 历史交易类内部指标数据源确认（2026-06-19 记录）

> 状态：📝 已记录，暂不修改代码

**已确认信息：**

| 评分项目编码 | 评分项目名称 | 数据类型 | 内外部 | 当前状态 |
|-------------|-------------|---------|--------|---------|
| `SalesAmount` | 历史采购金额 | 定量 | 内部（100000000） | 数据源待确认 |
| `ARAmount` | 历史逾期金额 | 定量 | 内部（100000000） | 数据源待确认 |
| `ARAge` | 历史逾期账龄（还款习惯） | 定量 | 内部（100000000） | 数据源待确认 |

**当前代码状态：**
`CofaceDataSyncPlugin.WriteToCustomerTags` 中这三个指标会走到 `GetCofaceValue`，因 Coface 数据无对应 key 返回 null，标签值为空。仅 `BigAccount` 实现了 CRM 取值。

**待确认问题：**
1. `SalesAmount`：查询 `salesorder.totalamount` 累计，还是读 Account.`mcs_totalpurchaseamount`？是否筛选订单状态/日期？
2. `ARAmount` / `ARAge`："客户月度在外货款"实体逻辑名、逾期金额字段、逾期账龄字段、生成版本年月字段。
3. 货币转换：源货币、目标货币、汇率来源。

**预计改动点（确认后执行）：**
1. `CofaceDataSyncPlugin.WriteToCustomerTags` 增加内部指标分支
2. 新增 `GetInternalValue` 方法按指标编码查询不同数据源
3. 如需货币转换，复用现有汇率工具

---

### 7.3 BPP 审批通过 365 天后评估记录失效（2026-06-19 记录）

> 状态：✅ 核心逻辑已实现（本地独立项目 `Code/ServiceJobs/CreditRecordExpiration/`），⏸️ 待确认宿主项目后集成

**需求描述：**
客户信用分经 BPP 审批通过后，评估记录的【有效状态】初始为有效。若审批日期超过 365 天，则该评估记录的【有效状态】应自动置为失效。

**涉及字段：**
- `mcs_credit_record.mcs_active`：有效状态（Boolean，审批通过时置为 `true`）
- `mcs_credit_record.mcs_approvedate`：审批日期（DateTime，审批通过时填充 `DateTime.Now`）

**当前实现状态：**
- `CreditRecordBppCallbackPlugin` 在 BPP 审批通过分支中已设置 `mcs_active = true` 和 `mcs_approvedate = DateTime.Now`。
- **尚未实现**超过 365 天后自动将 `mcs_active` 置为 `false` 的逻辑。

**建议实现方案（待评估）：**
1. **定时批量作业**：在 `Code/Tools/DeployTool` 或 MetadataTool 中新增命令（如 `expire-credit-records`），扫描所有 `mcs_active = true` 且 `mcs_approvedate < DateTime.Now.AddDays(-365)` 的记录，批量更新为 `mcs_active = false`。
2. **D365 异步工作流 / System Job**：创建自定义工作流活动，由 D365 系统作业每日调度执行。
3. **Azure Function / Service 项目定时任务**：在 `Service.sln` 中新增定时任务，每日检查并更新过期记录。

**下一步行动：**
1. 与业务确认失效后的业务影响（是否触发通知、是否允许重新发起评估、是否影响客户主数据 `mcs_creditvalid`）。
2. 选择技术实现方案（定时工具 / D365 工作流 / Azure Function）。
3. 开发并注册对应的 Plugin / 工具命令 / 定时任务。
4. 补充测试用例（TC-FLOW / TC-EVAL 中增加 365 天失效场景）。

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

> 💡 **用途**：跨环境对比 Plugin 是否同步时，不要只看 `version`（可能都是 `1.0.0.0`），要对比 `modifiedon`。

---

*本文件记录当前项目的特定上下文，每次对话开始时自动读取。*
*更新规则：每完成一个里程碑、发现新Bug、或进度变更时更新。*
