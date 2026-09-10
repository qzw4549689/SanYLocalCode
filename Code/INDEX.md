# 项目功能代码索引

> **索引范围**：本文件覆盖 `Code/` 目录下由本人负责/维护的 D365 自定义代码与工具。
> **路径基准**：所有代码路径均相对 `Code/` 目录。
> **维护原则**：新增、删除或迁移功能点时，必须同步更新本索引。

---

## 索引字段说明

| 字段 | 说明 |
|---|---|
| **模块** | 业务域/技术域划分 |
| **功能点** | 中文业务功能名 |
| **代码路径** | 对应源文件/目录的相对路径 |
| **涉及实体** | 该功能点直接操作的 D365 实体（如适用） |
| **备注** | 关键说明、状态、发布注意等 |

---

## 1. 客户管理

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| 客户管理 | Account 信用评估扩展表单逻辑 | `Customizations/WebResources/JS/mcs_account.js` | `account` | 信用分/等级/有效状态/外部评级字段只读；Coface ID 格式校验；信用状态提示 |
| 客户管理 | 客户主数据信用评估扩展 | `Customizations/WebResources/JS/mcs_customermasterdata.js` | `mcs_customermasterdata` | 与 `mcs_account.js` 同逻辑的信用字段控制 |
| 客户管理 | 客户主数据信用评估扩展校验（黑名单/不予授信） | `Customizations/Plugins/Account/AutoNumber/AccountValidationPlugin.cs` | `account` | 本地独立 Assembly；校验 `mcs_blacklist`、`mcs_creditgrant` |
| 客户管理 | 客户主数据信用评估字段校验 | `Customizations/Plugins/CustomerMasterData/Validation/CustomerMasterDataValidationPlugin.cs` | `mcs_customermasterdata` | 本地独立 Assembly；校验 Coface ID、客户等级、经销商分级等 |
| 客户管理 | 客户资信附件编号自动生成 | `Customizations/Plugins/CustomerFile/AutoNumber/CustomerFileAutoNumberPlugin.cs` | `mcs_customer_file` | 本地独立 Assembly；规则：ATT + YYYYMMDD + 4 位序列号 |
| 客户管理 | 客户信用标签创建初始化与更新同步 | `Customizations/Plugins/CustomerTag/AutoNumber/CustomerTagInitPlugin.cs` | `mcs_customer_tag`、`mcs_credit_items` | Create 后复制集成值到复核值，Update 后同步复核展示值 |
| 客户管理 | 客户信用标签修改阶段校验 | `Customizations/Plugins/CustomerTag/AutoNumber/CustomerTagValidationPlugin.cs` | `mcs_customer_tag`、`mcs_credit_record`、`mcs_credit_items` | 本地独立 Assembly；仅人工复核阶段允许修改，按数据类型限制字段 |
| 客户管理 | 客户信用标签表单逻辑 | `Customizations/WebResources/JS/mcs_customer_tag.js` | `mcs_customer_tag` | 集成值/复核字段显隐与只读、按评估状态控制可编辑性、数据类型与评分项目分类映射、保存校验 |
| 客户管理 | 客户信用画像仪表盘 | `Customizations/WebResources/HTML/mcs_credit_profile.html` | `account`、`mcs_customermasterdata`、`mcs_credit_record`、`mcs_customer_tag`、`mcs_contract`、`mcs_contractdetail` | PRD 2.0 画像：基本信息、信用分/等级、信用基础标签、已签待执行合同、重点尽调；内嵌信用飞轮弹窗 |

---

## 2. 信用评估管理

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| 信用评估管理 | 客户信用评估记录表单逻辑 | `Customizations/WebResources/JS/mcs_credit_record.js` | `mcs_credit_record` / `mcs_custcredit` | 状态流 9-16、客户信息带出、下一步/数据集成刷新/重新发起按钮、BPP 审批提交/查看/废弃、Coface 企业搜索、BPF 拦截与字段锁定；**状态 10→11 不再因 Coface 缺失阻断，状态 12→13 强制校验标签已补录** |
| 信用评估管理 | Coface 企业搜索弹窗 | `Customizations/WebResources/HTML/mcs_coface_company_search.html` | `mcs_credit_record`、`account`、`mcs_customermasterdata` | 按英文名称/国家编码搜索 Coface 企业并绑定 `mcs_cofaceid`；**搜索条件上方显示 Coface 联系邮箱提示** |
| 信用评估管理 | 客户信用评估记录命令栏定义（占位） | `Customizations/Entities/mcs_credit_record/RibbonDiff.xml` | `mcs_credit_record` | 当前目录仅有 RibbonDiff.xml，无 Entity.xml；按钮通过 Modern Command Bar / App Action 部署 |
| 信用评估管理 | 客户信用评估记录表实体定义 | `Customizations/Entities/mcs_custcredit/Entity.xml` | `mcs_custcredit` | 显示名“客户信用评估记录表”，含评估编码、客户信息、Coface/URBA/Report/BPP 字段、评估状态 9-16、信用分等 |
| 信用评估管理 | 客户信用评估记录表主窗体 | `Customizations/Entities/mcs_custcredit/FormXml/main/4a54029f-08d0-4002-8efa-cd81adcd8f4e.xml` | `mcs_custcredit` | 主窗体 XML |
| 信用评估管理 | 客户信用评估记录表卡片窗体 | `Customizations/Entities/mcs_custcredit/FormXml/card/4b86e165-68a9-4c2f-9bac-e4177ea46be2.xml` | `mcs_custcredit` | 卡片窗体 XML |
| 信用评估管理 | 客户信用评估记录表快速创建窗体 | `Customizations/Entities/mcs_custcredit/FormXml/quick/2f69f1ee-2861-4c59-ad3d-937cbd7c2382.xml` | `mcs_custcredit` | 快速创建窗体 XML |
| 信用评估管理 | 客户信用评估记录表视图 | `Customizations/Entities/mcs_custcredit/SavedQueries/6be83c19-0b5a-430a-afed-44001cc0de82.xml` | `mcs_custcredit` | 系统/个人视图定义 |
| 信用评估管理 | 信用评估记录编码自动生成 | `Customizations/Plugins/CreditRecord/AutoNumber/CreditRecordAutoNumberPlugin.cs` | `mcs_credit_record` | 本地独立 Assembly；规则：SCO + YYYYMMDD + 4 位序列号 |
| 信用评估管理 | 信用评估状态流转校验 | `Customizations/Plugins/CreditRecord/Validation/CreditRecordStatusTransitionPlugin.cs` | `mcs_credit_record` | 本地独立 Assembly；阻止非法状态变更，控制 BPF 阶段 |
| 信用评估管理 | 信用评估记录过期处理作业 | `ServiceJobs/CreditRecordExpiration/CreditRecordExpirationService.cs` | `mcs_credit_record`、`mcs_customermasterdata`、`account`、`mcs_credit_scoringcard`、`mcs_credit_items` | 将超过 365 天的已审批评估记录置为失效，并同步更新客户主数据信用评估有效状态 |
| 信用评估管理 | 信用评估记录过期处理入口 | `ServiceJobs/CreditRecordExpiration/Program.cs` | — | 支持 `--execute`/`--diagnose`/`--analyze-scoring-cards`/`--seed-test-data` 四种模式 |
| 信用评估管理 | 空日志记录器 | `ServiceJobs/CreditRecordExpiration/NullCreditRecordExpirationLogger.cs` | — | 未传入 `ILogger` 时的默认空实现 |
| 信用评估管理 | 过期处理项目配置 | `ServiceJobs/CreditRecordExpiration/CreditRecordExpiration.csproj` | — | .NET 10 控制台应用，引用 Dataverse Client 与 `D365ToolCommon` |

---

## 3. 信用评分与评分卡

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| 信用评分与评分卡 | 客户信用评分项目表单逻辑 | `Customizations/WebResources/JS/mcs_credit_items.js` | `mcs_credit_items` | 基础配置表字段只读；按数据类型显示定量/定性配置提示 |
| 信用评分与评分卡 | 定性评分项目枚举值表单逻辑 | `Customizations/WebResources/JS/mcs_credititem_value.js` | `mcs_credititem_value` | 只读、校验仅定性项目可配枚举值、必填校验 |
| 信用评分与评分卡 | 客户评分卡配置表单逻辑 | `Customizations/WebResources/JS/mcs_credit_scoringcard.js` | `mcs_credit_scoringcard` | 评分项目自动带出编码/名称/类型/分类；定量显式 min/max、定性显式 listvalue；min/max 联动校验；克隆新建 |
| 信用评分与评分卡 | 客户信用飞轮可视化 | `Customizations/WebResources/HTML/mcs_credit_wheel.html` | `account`、`mcs_credit_record`、`mcs_customer_tag` | Vue + ECharts 旭日图展示五大类信用得分 |
| 信用评分与评分卡 | ECharts 图表库（依赖） | `Customizations/WebResources/JS/mcs_credit_wheel_echarts.js` | — | 压缩后的 ECharts，用于信用飞轮/画像 |
| 信用评分与评分卡 | Vue 3 运行时库（依赖） | `Customizations/WebResources/JS/mcs_credit_wheel_vue.js` | — | 压缩后的 Vue 3.5.38，用于信用飞轮 |
| 信用评分与评分卡 | 评分项目保存校验 | `Customizations/Plugins/CreditItems/AutoNumber/CreditItemsValidationPlugin.cs` | `mcs_credit_items` | 本地独立 Assembly；编码格式、唯一性、数据类型等 |
| 信用评分与评分卡 | 评分项目枚举值保存校验 | `Customizations/Plugins/CreditItemValue/AutoNumber/CreditItemValueValidationPlugin.cs` | `mcs_credititem_value`、`mcs_credit_items` | 本地独立 Assembly；定性类型校验、选择项编码唯一性 |
| 信用评分与评分卡 | 评分卡配置编码自动生成 | `Customizations/Plugins/ScoringCard/AutoNumber/AutoNumberPlugin.cs` | `mcs_credit_scoringcard` | 本地独立 Assembly；规则：SC + YYYYMMDD + 4 位序列号 |
| 信用评分与评分卡 | 评分卡配置校验 | `Customizations/Plugins/ScoringCard/Validation/CreditScoringCardValidationPlugin.cs` | `mcs_credit_scoringcard`、`mcs_credititem_value` | 同一 category+项目下定性值不重复、定量区间不重叠 |
| 信用评分与评分卡 | 信用分计算主 Plugin | `Customizations/Plugins/CreditScore/Plugin/CreditScorePlugin.cs` | `mcs_credit_record`、`account`、`mcs_customermasterdata`、`salesorder` | 本地独立 Assembly；状态 13 时计算信用分并回写；**计算前校验所有空标签已补录，否则阻断** |
| 信用评分与评分卡 | 信用分计算核心算法 | `Customizations/Plugins/CreditScore/Calculator/ScoreCalculator.cs` | `mcs_credit_scoringcard`、`mcs_customer_tag`、`mcs_credit_items` | 本地独立 Assembly；按评分卡配置逐项定量/定性评分；**禅道 #2090：指标缺失时按评分卡「缺失」档赋分（定性=listvalue 为 O 的配置行；定量=min/max 均空的配置行），未配置缺失档兜底 0 分** |
| 信用评分与评分卡 | BPF 阶段同步 Plugin | `Customizations/Plugins/CreditScore/Plugin/BpfStageSyncPlugin.cs` | `mcs_credit_record` | 本地独立 Assembly；`mcs_status` 变更时同步 `stageid` |
| 信用评分与评分卡 | BPF 阶段同步辅助类 | `Customizations/Plugins/CreditScore/Plugin/BpfSyncHelper.cs` | `mcs_credit_record` | 本地独立 Assembly；维护状态值到 BPF StageId 映射 |
| 信用评分与评分卡 | 客户评分卡配置表实体定义 | `Customizations/Entities/mcs_credit_scoringcard/Entity.xml` | `mcs_credit_scoringcard` | 含评分卡类型（7 类）、评分项目编码/名称/分类、数据类型、定量 min/max、定性项目值、赋分等 |
| 信用评分与评分卡 | 客户评分卡配置表主窗体 | `Customizations/Entities/mcs_credit_scoringcard/FormXml/main/{19af02d4-5686-4f97-ad2b-700d3f2772fa}.xml` | `mcs_credit_scoringcard` | 主窗体布局 |
| 信用评分与评分卡 | 客户评分卡配置表卡片窗体 | `Customizations/Entities/mcs_credit_scoringcard/FormXml/card/{d8ad7640-dcad-4e7c-bce2-58f886e7aa37}.xml` | `mcs_credit_scoringcard` | 卡片窗体 XML |
| 信用评分与评分卡 | 客户评分卡配置表快速创建窗体 | `Customizations/Entities/mcs_credit_scoringcard/FormXml/quick/{1ed57397-232c-4dbb-9c01-f899fdd6bc47}.xml` | `mcs_credit_scoringcard` | 快速创建窗体 XML |
| 信用评分与评分卡 | 客户评分卡配置表视图 | `Customizations/Entities/mcs_credit_scoringcard/SavedQueries/` 下多个 XML | `mcs_credit_scoringcard` | 视图定义集合 |
| 信用评分与评分卡 | 客户信用评分项目表实体定义 | `Customizations/Entities/mcs_credit_items/Entity.xml` | `mcs_credit_items` | 含评分项目编码/名称/说明、分类、数据类型、内外部、人工补录、外部提供等字段 |
| 信用评分与评分卡 | 客户信用评分项目表主窗体 | `Customizations/Entities/mcs_credit_items/FormXml/main/{9f9a6ca1-1bfe-49bf-8054-1f8ec8e53590}.xml` | `mcs_credit_items` | 主窗体 XML |
| 信用评分与评分卡 | 客户信用评分项目表卡片窗体 | `Customizations/Entities/mcs_credit_items/FormXml/card/{5eaa5a53-1ce5-4373-a0f4-b5f9fbd04e82}.xml` | `mcs_credit_items` | 卡片窗体 XML |
| 信用评分与评分卡 | 客户信用评分项目表快速创建窗体 | `Customizations/Entities/mcs_credit_items/FormXml/quick/{cf5de9c4-020e-4170-8aa0-a64e118accc7}.xml` | `mcs_credit_items` | 快速创建窗体 XML |
| 信用评分与评分卡 | 客户信用评分项目表视图 | `Customizations/Entities/mcs_credit_items/SavedQueries/` 下多个 XML | `mcs_credit_items` | 视图定义集合 |
| 信用评分与评分卡 | 定性评分项目枚举值实体定义 | `Customizations/Entities/mcs_credititem_value/Entity.xml` | `mcs_credititem_value` | 含评分项目编码、选择项编码/名称等字段 |
| 信用评分与评分卡 | 定性评分项目枚举值主窗体 | `Customizations/Entities/mcs_credititem_value/FormXml/main/{08f828aa-3c8e-4697-b275-92f3a45bbc63}.xml` | `mcs_credititem_value` | 主窗体 XML |
| 信用评分与评分卡 | 定性评分项目枚举值卡片窗体 | `Customizations/Entities/mcs_credititem_value/FormXml/card/{3a27a16e-fad0-4260-8e16-62da964268af}.xml` | `mcs_credititem_value` | 卡片窗体 XML |
| 信用评分与评分卡 | 定性评分项目枚举值快速创建窗体 | `Customizations/Entities/mcs_credititem_value/FormXml/quick/{67bc95e5-4d40-43a9-828c-7f9b0e8acca1}.xml` | `mcs_credititem_value` | 快速创建窗体 XML |
| 信用评分与评分卡 | 定性评分项目枚举值视图 | `Customizations/Entities/mcs_credititem_value/SavedQueries/` 下多个 XML | `mcs_credititem_value` | 视图定义集合 |

---

## 4. Coface 征信集成

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| Coface 集成 | Coface API 服务封装 | `Customizations/Plugins/CofaceIntegration/Api/CofaceApiService.cs` | — | 本地独立 Assembly；封装公司搜索、URBA360、Report、PDF 下载、下单接口（ExecutePost/调查单/URBA监控单/Report单/即时报告）等 HTTP 调用 |
| Coface 集成 | Coface Token 管理 | `Customizations/Plugins/CofaceIntegration/Token/CofaceTokenManager.cs` | `ms_systemconfiguration` | 本地独立 Assembly；Token 缓存化（读写 `coface_idtoken`/`coface_token_expiry`，失效自动重新认证，回写用系统身份） |
| Coface 集成 | Coface API 配置读取 | `Customizations/Plugins/CofaceIntegration/CofaceConfigHelper.cs` | `ms_systemconfiguration` | 本地独立 Assembly；读取 CofaceApiConfig JSON 配置 |
| Coface 集成 | Coface API 配置模型 | `Customizations/Plugins/CofaceIntegration/CofaceApiConfig.cs` | — | 本地独立 Assembly；baseUrl/authUrl/apiKey/username/password |
| Coface 集成 | Coface 国家特殊处理配置 | `Customizations/Plugins/CofaceIntegration/CofaceCountryConfig.cs` | — | 本地独立 Assembly；定义受限国家/CEE 国家及 Report 产品映射 |
| Coface 集成 | Coface 国家配置读取 | `Customizations/Plugins/CofaceIntegration/CofaceCountryConfigHelper.cs` | `ms_systemconfiguration` | 本地独立 Assembly；读取 CofaceCountryConfig JSON 配置 |
| Coface 集成 | Coface 汇率读取与转换 | `Customizations/Plugins/CofaceIntegration/CofaceExchangeRateHelper.cs` | `transactioncurrency` | 本地独立 Assembly；D365 标准汇率 1 USD→LC 转 1 LC→USD |
| Coface 集成 | Coface NACE 行业映射 | `Customizations/Plugins/CofaceIntegration/CofaceNaceMappingHelper.cs` | `mcs_coface_nace_mapping` | 本地独立 Assembly；NACE Division → 三一行业 |
| Coface 集成 | Coface 定性指标值映射 | `Customizations/Plugins/CofaceIntegration/CofaceQualitativeMappingHelper.cs` | `mcs_credititem_value`、`mcs_credit_items` | 本地独立 Assembly；Coface 原始值 ↔ 三一标准编码/中文显示名 |
| Coface 集成 | JSON 数值安全解析扩展 | `Customizations/Plugins/CofaceIntegration/JsonElementExtensions.cs` | — | 本地独立 Assembly；兼容 Number/String 两种格式的 decimal 读取 |
| Coface 集成 | Full Report 数据解析 | `Customizations/Plugins/CofaceIntegration/Parser/FullReportParser.cs` | — | 本地独立 Assembly；提取注册资本、从业年限、诉讼记录 |
| Coface 集成 | URBA360 数据解析 | `Customizations/Plugins/CofaceIntegration/Parser/Urba360Parser.cs` | `mcs_coface_financial_indicator` | 本地独立 Assembly；提取外部评级、国别/行业风险、财务指标等 9 项 |
| Coface 集成 | Coface 数据集成主 Plugin | `Customizations/Plugins/CofaceIntegration/Plugin/CofaceDataSyncPlugin.cs` | `mcs_credit_record`、`mcs_customer_tag`、`mcs_credit_items`、`mcs_credit_scoringcard`、`mcs_customer_file`、`account`、`mcs_customermasterdata`、`salesorder`、`mcs_outstanding` | 本地独立 Assembly；状态 11 时拉取 URBA360/Full Report/内部交易数据并写入标签 |
| Coface 集成 | Coface 企业搜索 Custom Action | `Customizations/Plugins/CofaceIntegration/Plugin/CofaceSearchCompanyPlugin.cs` | — | 本地独立 Assembly；Custom Action `mcs_CofaceSearchCompany` |
| Coface 集成 | Coface 系统内下单 Custom API Plugin | `Customizations/Plugins/CofaceIntegration/Plugin/CofacePlaceOrderPlugin.cs` | `mcs_credit_record` | 本地独立 Assembly；Custom API `mcs_CofacePlaceOrder`；点击推进式下单状态机（调查单→URBA监控单→Report单），防重复扣费先查后下 |
| Coface 集成 | 绑定 Coface ID 回写客户主数据 Plugin（禅道 #2092） | `Customizations/Plugins/CofaceIntegration/Plugin/CofaceBindWritebackPlugin.cs` | `mcs_credit_record` | Update PostOperation，Filter=mcs_cofaceid；系统身份回写客户主数据科法斯客户代码（空才写、有值不覆盖） |
| Coface 集成 | Coface 订单信息提取帮助类 | `Customizations/Plugins/CofaceIntegration/CofaceOrderInfoHelper.cs` | — | 本地独立 Assembly；从 CofaceDataSyncPlugin 抽取的 URBA/Report 订单就绪判定与 publicationId 提取公共逻辑 |
| Coface 集成 | Coface 订单查询测试程序 | `Customizations/Plugins/CofaceIntegration/CheckCofaceOrders.cs` | — | 本地独立 Assembly；独立控制台入口，仅输出参数 |
| Coface 集成 | Coface API 测试工具 | `Tools/CofaceApiTest/Program.cs` | — | 认证、URBA360/Report 订单查询与内容获取 |
| Coface 集成 | 国家代码测试 | `Tools/CofaceApiTest/TestCountryCode.cs` | — | 对比 `countryCode=CN` 与 `PL` 的 URBA360 响应 |
| Coface 集成 | Coface 财务指标配置导入工具 | `Tools/CofaceConfigImporter/Program.cs` | `mcs_coface_financial_indicator` | 读取 JSON 导入财务指标配置 |
| Coface 集成 | Coface 数据集成测试 | `Tools/CofaceConfigImporter/TestCofaceSync.cs` | `mcs_credit_record` | 列出候选记录、触发 `CofaceDataSyncPlugin`、查看标签与 Trace |
| Coface 集成 | Coface 财务指标 JSON 配置 | `Tools/CofaceConfigImporter/coface_financial_indicators.json` | `mcs_coface_financial_indicator` | 财务指标配置数据 |
| Coface 集成 | Coface 财务指标 JSON 配置（0612 版） | `Tools/CofaceConfigImporter/coface_financial_indicators_0612.json` | `mcs_coface_financial_indicator` | 0612 版本配置数据 |
| Coface 集成 | Coface 财务指标配置备份 | `Tools/CofaceConfigImporter/coface_financial_indicators_backup_20260612.json` | `mcs_coface_financial_indicator` | 配置备份 |
| Coface 集成 | Coface 财务指标标准配置 | `Tools/CofaceConfigImporter/mcs_coface_financial_indicator.json` | `mcs_coface_financial_indicator` | 标准导入文件 |


---

## 5. BPP 审批集成

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| BPP 集成 | BPP 审批流程发起 | `Customizations/Plugins/BppIntegration/Plugin/BppIntegrationPlugin.cs` | `mcs_credit_record` | 本地独立 Assembly；状态 14 时调用 `mcs_bppstartapi`，防重复提交 |
| BPP 集成 | BPP 审批结果回调处理 | `Customizations/Plugins/BppIntegration/Plugin/BppCallbackPlugin.cs` | `mcs_credit_record`、`mcs_customermasterdata` | 本地独立 Assembly；监听 `mcs_bppstatus`，更新业务状态并生成 BPP 链接 |
| BPP 集成 | 信用等级映射配置读取（禅道 #2091） | `Customizations/Plugins/BppIntegration/CreditGradeMappingConfig.cs` | `ms_systemconfiguration` | 读 `CreditGradeMapping` 配置（信用分→A0-A4 阈值，下限含降序匹配），缺失/解析失败用内置新口径默认值（70/58/49/40/0）兜底不阻断 |
| BPP 集成 | 客户信用评估 BPP 审批处理 | `SanyD365Project/Service/SanyD365.Main/Entities/BPP/BPPHandlerServices/BPPHandlerServiceForCreditRecord.cs` | `mcs_credit_record`、`account`、`mcs_bppapply` | 实现 `IBPPHandlerService`：封装 BPP 表单变量、发起前清理旧流程、发起后更新审批链接/下一审批人、统一回调处理、错误信息回写 |
| BPP 集成 | 融资管理 BPP 提交（立项/方案审批） | `Customizations/Plugins/FinancingManagement/Bpp/FsmDataBppIntegrationPlugin.cs` | `mcs_fsm_data` | 本地独立 Assembly；`mcs_bppstatus` 非2→2 时校验状态与可提交标记后调 `mcs_bppstartapi` |
| BPP 集成 | 融资管理 BPP 回调处理 | `Customizations/Plugins/FinancingManagement/Bpp/FsmDataBppCallbackPlugin.cs` | `mcs_fsm_data` | 本地独立 Assembly；监听 `mcs_bppstatuscode`，按 `mcs_approve_type` 流转融资状态（2→3 / 3→4），驳回恢复可提交标记，撤回/废弃清空 BPP 标识 |
| BPP 集成 | 融资管理 BPP 审批处理 | `SanyD365Project/Service/SanyD365.Main/Entities/BPP/BPPHandlerServices/BPPHandlerServiceForFsmData.cs` | `mcs_fsm_data`、`mcs_bppapply` | 实现 `IBPPHandlerService`；按 `mcs_approve_type` 选 TemplateCode（FsmDataInitiation/FsmDataProject），表单变量含融资编号/记录链接/客户名称/客户编码/融资经理；#1561 起按审批类型取提交备注（立项取 `mcs_fsm_initiation_remark`/方案取 `mcs_fsm_project_remark`）写入平台级 `ApproveOpn`/`RetryApproveOpn`（审批记录-起草人节点意见，FundClaim 先例；不走表单变量，融资两个 BPP 模板无备注字段 Code）；审批信息快照：#1713 起立项（type=1）发起/回调同步写 `mcs_init_*` 快照组，#1754/#1756 起方案（type=2）同步写 `mcs_proj_*` 快照组，通用组（`mcs_bppstatuscode`/`mcs_fsm_data_url` 等）无条件写供 `FsmDataBppCallbackPlugin` 流转触发但不上表单，表单「立项审批」section 绑 `mcs_init_*`、`融资解决方案审批」section 绑 `mcs_proj_*` |
| BPP 集成 | 融资管理表单提交审批逻辑 | `Customizations/WebResources/JS/mcs_fsm_data.js` | `mcs_fsm_data` | `FsmDataForm.submitInitiationApproval` / `submitProjectApproval`：前端校验后置 `mcs_approve_type` + `mcs_bppstatus=2` 触发后端 Plugin；#1561 起 payload 按审批类型带对应提交审批备注字段（`mcs_fsm_initiation_remark`/`mcs_fsm_project_remark`，仅状态 2/3 可填，随 updateRecord 同事务落库）；阶段锁定矩阵 `applyStageControl`：#1656 状态=2 未提交立项审批前六要素可编辑（提交后锁/驳回解锁/通过锁死，取代 #1540 全锁）；#1652 提交立项审批前允许 BPF 回退融资需求（状态同步回 1+自动保存防反弹，审批中/已通过禁回退）；#2071 融资方案接口人 `mcs_fsm_manager` 状态 1/2 必填（元数据保持 None，JS setRequiredLevel） |

---

## 6. 融资管理

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| 融资管理 | 融资资源管理表单逻辑 | `Customizations/WebResources/JS/mcs_fsm_resource.js` | `mcs_fsm_resource`、`mcs_bank` | 机构代码全类型锁定只读（禅道 #2072）：银行时选择银行自动带出机构代码（`mcs_bank.mcs_bankno`）/机构名称（`mcs_bank.mcs_name`）；保险/其他时机构代码=融资资源编号（新建保存后 addOnPostSave 同步+无感保存；元数据配套改非必填）；从银行切换到其他类型时隐藏 Bank 并清空 Bank/机构名称（机构代码由 sync 覆盖）；金融产品多选按类型筛选：银行/保险/其他均为 1-10 共 10 项（禅道 #1572 保险与银行同代码表；禅道 #2085 其他类型与银行/保险一致）；国家→洲省级联（#1528），所在城市 #2138 起改手工输入文本字段 `mcs_fsm_institution_city_text`（原城市 Lookup `mcs_fsm_institution_city` 改非必填移出表单，城市级联移除）（FluentUI 多选控件 addOption 需用对象签名 {text,value}） |
| 融资管理 | 融资需求级联带出/弹窗过滤/清空联动/保存校验 | `Customizations/WebResources/JS/mcs_fsm_data.js` | `mcs_fsm_data`、`mcs_leadmain`、`mcs_quoter`、`mcs_quote_main`、`mcs_contract`、`mcs_customermasterdata` | `FsmDataForm.onLoad`：线索（新字段 `mcs_leadmain_id`→mcs_leadmain）/报价单（新字段 `mcs_quoter_id`→mcs_quoter）/合同 onChange 全量重算派生字段（大区/国家/事业部/客户名称/客户编码，优先级 合同>报价单>线索，客户编码取 sapnumber）；合同/报价单向上代入线索；报价单弹窗按线索过滤（addPreSearch）；来源清空时派生字段联动清空；onSave 校验三来源至少一个 + 重复性校验（三者任一相同即重复，**同步 XHR 查询**，2026-08-07 #1652 二次修复：异步 preventDefault 会中止 BPF 阶段导航保存导致回退弹回）；提交立项/方案审批前置分阶段必填校验（融资六要素+合同号；方案阶段四项贴息/费用/回购/其它条件 2026-08-04 #1559 起非必填），融资经理自动取登录人。旧字段 mcs_lead_id/mcs_quote_id 保留不删（2026-07-24 红线）。**禅道 #2150（2026-09-07）：合同改多选**——新字段 `mcs_contract_ids`（Memo 存 GUID 逗号分隔，平台公共 PCF `mcs_common.control.lookup.multiplechoice` 绑值，同成交条件基线库）+ `mcs_contract_nos`（合同编号文本，onChange `onContractIdsChanged`→`syncContractNos` 同步）；以**第一个合同**带出派生字段；重复校验改 `contains(mcs_contract_ids)` 任一合同相同即重复；阶段控制/必填清单换多选字段；原「按线索过滤合同放大镜」随控件取消（PCF 无过滤参数）；旧单选 `mcs_contract_id` 表单隐藏保留，存量不迁移（用户拍板） |
| 融资管理 | 合同编号多选 picker（禅道 #2169①） | `Customizations/WebResources/HTML/mcs_fsm_contract_multiselect.html` | `mcs_fsm_data`、`mcs_contract`、`mcs_quoter`、`mcs_quote_main` | #2150 合同改多选后业务要求候选合同按线索/报价单过滤，平台 PCF `mcs_common.control.lookup.multiplechoice` 无过滤参数（#1559 实锤）→ 参照机构 picker 自制：线索（mcs_leadmain_id）有值→`mcs_contract.mcs_leadmain`=线索过滤；无线索则报价单→报价主表→其线索过滤；均无→不过滤；写回 `mcs_contract_ids`+fireOnChange 复用 #2150 编号同步/第一个合同带出；可编辑状态读 `mcs_contract_ids` 控件 disabled 与表单阶段控制同源。**上下文获取要点（DEV1 实锤）**：存量记录 parent.Xrm.Page 已绑定可用；新建表单顶层 Xrm.Page 是 stub 永不绑定，真实表单在顶层子 frame（uclient/blank.htm，含 FsmDataForm+已绑定 Page），getFormPage 按 自身→父级→顶层→顶层子 frame 逐级找含 mcs_fsm_status 的 Page + 轮询等待就绪；FsmStageChanged 也分发在该 frame 的 window |
| 融资管理 | 融资六要素/解决方案页面字段（禅道 #1559） | `Customizations/WebResources/JS/mcs_fsm_data.js` | `mcs_fsm_data`、`mcs_fsm_resource` | 六要素「融资产品」单选选项集 `mcs_fsm_product`（仅银行类 1-11），六要素/方案双单元格同一字段（方案侧标签=金融产品、只读）；「融资资源机构」多选 = 自制 HTML WebResource `mcs_fsm_resource_multiselect.html` 嵌入式 picker（平台 PCF `mcs_common.control.lookup.multiplechoice` 无过滤参数不满足下拉级过滤，bundle 实锤查询无 $filter）：仅启用且机构产品含所选融资产品的机构显示，搜索+勾选写回 `mcs_fsm_resource_ids`（Memo 存 GUID 逗号分隔，表单隐藏单元格保留属性），`onResourceIdsChanged` 校验并按机构类型（1银行/2保险/9其它）分组把名称/编码逗号分隔带入 6 个只读字段（`mcs_fsm_bank/insurance/other_names/codes`，`SOLUTION_AUTO_FIELDS` 始终只读）；融资产品变更清空重选；#1507 的 syncResourceName/filterProductsByResource/ALL_PRODUCT_OPTIONS 已废弃移除，旧字段 product_desc/resource_id/resource_name/resource_products 表单隐藏保留不删 |

| 融资管理 | 融资资源状态同步（激活回写是否启用过，禅道 #1433） | `Customizations/Plugins/FinancingManagement/Resource/FsmResourceStateSyncPlugin.cs` | `mcs_fsm_resource` | Update Filter=statecode PostOp Sync；列表【激活】（statecode→0）时幂等回写 `mcs_fsm_rl_status=true`（单向标记，停用不清）；主 Assembly 类名 `SanyD365.D365Extension.Sales.Plugins.FinancingManagement.Resource.FsmResourceStateSyncPlugin` |
| 融资管理 | 融资资源删除守卫（禅道 #1433 关联 PRD 删除规则） | `Customizations/Plugins/FinancingManagement/Resource/FsmResourceDeleteGuardPlugin.cs` | `mcs_fsm_resource` | Delete PreOp Sync + PreImage（mcs_fsm_rl_status+createdby）；已启用过拦截、非创建人拦截（SysAdmin 放行）；⚠️ Delete 管道 `context.UserId` 恒为 SYSTEM，创建人比对必须用 `InitiatingUserId`；业务角色不写死靠安全角色删除权限配置 |
| 融资管理 | 融资资源机构代码重复校验（禅道 #1512） | `Customizations/Plugins/FinancingManagement/Resource/FsmResourceDuplicationCheckPlugin.cs` | `mcs_fsm_resource` | Create/Update PreOp Sync（Update Filter=mcs_fsm_institution_code）；机构代码全局唯一、含停用记录（用户确认口径）；系统身份查重防权限绕过；拦截提示含已有记录编号并引导「启用」原记录 |
| 融资管理 | 融资落实订单号唯一校验（禅道 #1511） | `Customizations/Plugins/FinancingManagement/Detail/FsmDetailDataDuplicationCheckPlugin.cs` | `mcs_fsm_detail_data` | Create/Update PreOp Sync（Update Filter=mcs_order_id,mcs_fsm_data_id + PreImage 补齐）；同一融资管理记录（mcs_fsm_data_id）下订单号（mcs_order_id）唯一，不同融资管理记录间不拦截（用户确认口径，PRD 融资方案落实-新增-保存唯一性校验）；系统身份查重；拦截提示显示订单名称（Create Target Lookup 无 Name 需显式 Retrieve）；⚠️ 该校验仍针对旧字段 `mcs_order_id`（salesorder），表单 2026-09-02 起已改用新字段 `mcs_orderid`（mcs_order），新字段暂无唯一性校验覆盖 |
| 融资管理 | 融资落实表单逻辑（只读守卫+订单过滤） | `Customizations/WebResources/JS/mcs_fsm_detail_data.js` | `mcs_fsm_detail_data`、`mcs_fsm_data`、`mcs_order`、`mcs_fmprocess` | 禅道 #1816：主单 BPF 完成（statuscode=2）全表单只读+提示；2026-09-02：订单编号放大镜按主表合同过滤（`mcs_order.mcs_contract`，addPreSearch+addCustomFilter，缓存未就绪/无合同空结果兜底）；**禅道 #2150（2026-09-07）：主表合同改多选**——读 `mcs_contract_ids` 解析 GUID 数组，`mcs_contract IN 多选合同`过滤；主表无合同时按客户兜底（link-entity account，`mcs_contractbuyer`→`mcs_customermasterdata`=主表客户主数据，与判新老客户同口径）；均无仍空结果兜底 |
| 融资管理 | 进入融资落实阶段小铃铛通知（禅道 #1654） | `Customizations/Plugins/FinancingManagement/Notify/FsmDataStage4NotifyPlugin.cs` | `mcs_fsm_data`、`systemuser` | Update PostOp Sync（Filter=mcs_fsm_status + PreImage：mcs_fsm_status/mcs_fsm_no/mcs_fsm_manager/createdby）；融资状态非4→4（方案审批通过回调）时调 `SendAppNotification`（Recipient=融资经理 mcs_fsm_manager 为空兼底 createdby）发小铃铛提醒，正文含融资编号（本环境不支持 Data 操作按钮/正文链接不可点击，2026-08-06 实测）；通知失败仅记 Trace 不影响主流程；预研命令 `test-app-notification`（MetadataTool） |

---

## 7. 贸易条款

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| 贸易条款 | 成交条件样板库表单逻辑 | `Customizations/WebResources/JS/mcs_trade_stpayterm.js` | `mcs_trade_stpayterm` | Lookup 编码/名称同步、多选查找组件同步、克隆新增、列表批量申请/审批/拒绝 |
| 贸易条款 | 成交条件样板库列表批量按钮 Ribbon 定义（2026-07-27 新增） | `Customizations/Ribbon/mcs_trade_stpayterm.ribbon.xml` | `mcs_trade_stpayterm` | 批量申请/审批/拒绝 3 按钮 + 内联 SelectionCountRule（勾选≥1 显示）+ 双语 LocLabels；显隐规则随实体包走，替代原 appaction 方案（N:N 关联不随包） |
| 信用评估管理 | 【Coface 下单】表单按钮（2026-08-01 最终定稿 App Action） | `Tools/DeployTool/AppActionDeployer.cs`（DeployButtons 中 `mcs_credit_record_place_coface_order`） | `mcs_credit_record` | Coface 系统内下单按钮（`CreditRecordForm.placeCofaceOrder` + PrimaryControl，fonticon=ShoppingCart）；用户最终决策：无特殊显隐控制用 App Action，不用 Ribbon（曾短暂 Ribbon 化后回退，环境残留用 ValueRule 恒否 DisplayRule 同 Id 覆盖隐藏） |
| 贸易条款 | 成交条件产品分类关系表单逻辑 | `Customizations/WebResources/JS/mcs_trade_ptgrouptype.js` | `mcs_trade_ptgrouptype` | 产品线/产品分类 Lookup 变更后自动带出编码与名称 |
| 贸易条款 | 成交条件样板库编码自动生成 | `Customizations/Plugins/TradeStPayTerm/AutoNumber/TradeStPayTermAutoNumberPlugin.cs` | `mcs_trade_stpayterm` | 本地独立 Assembly；规则：TC + YYMMDD + 2 位序列号 |
| 贸易条款 | 成交条件产品分类关系产品线同步 | `Customizations/Plugins/TradeStPayTerm/Sync/TradePtGroupTypeProductLineSyncPlugin.cs` | `mcs_trade_ptgrouptype`、`mcs_productline` | 本地独立 Assembly；根据产品线 Lookup 同步编码/名称 |
| 贸易条款 | 成交条件样板库保存校验 | `Customizations/Plugins/TradeStPayTerm/Validation/TradeStPayTermValidationPlugin.cs` | `mcs_trade_stpayterm` | 首付款、账期、状态流转、重复记录校验 |
| 贸易条款 | 成交条件样板库提交审批按事业部共享（禅道 #1151） | `Customizations/Plugins/TradeStPayTerm/Sharing/TradeStPayTermSharePlugin.cs` | `mcs_trade_stpayterm`、`mcs_bu` | Update PostOperation；状态变为待审批时按 mcs_businessunit 查 mcs_bu.mcs_buteamid，共享（Read+Write）给 BU 团队 |
| 贸易条款 | 成交条件样板库查询 Custom API | `Customizations/Plugins/TradeStPayTerm.Api/QueryTradeStPayTermPlugin.cs` | `mcs_trade_stpayterm` | 本地独立 Assembly；Custom Action `mcs_QueryTradeStPayTerm` |
| 贸易条款 | 成交条件样板库查询服务 | `Customizations/Plugins/TradeStPayTerm.Api/TradeStPayTermQueryService.cs` | `mcs_trade_stpayterm`、`mcs_customermasterdata`、`mcs_country`、`mcs_trade_ptgrouptype`、`mcs_trade_pttype` | 本地独立 Assembly；按事业部/子公司/国家/产品分类/客户分类匹配生效记录 |
| 贸易条款 | 成交条件产品分类多语言（英文） | `Customizations/WebResources/Language/1033.json` | — | `TradePtType_*` 英文标签 |
| 贸易条款 | 成交条件产品分类多语言（中文） | `Customizations/WebResources/Language/2052.json` | — | `TradePtType_*` 中文标签 |

---

## 8. 工厂信用

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| 工厂信用 | 厂端授信模型版本表单逻辑 | `Customizations/WebResources/JS/mcs_fca_mdlversion.js` | `mcs_fca_mdlversion` | 新建默认值（生效=是、开始/结束日期）；保存前校验生效版本日期重叠 |
| 工厂信用 | 厂端授信模型配置表单逻辑 | `Customizations/WebResources/JS/mcs_fca_mdlconfig.js` | `mcs_fca_mdlconfig` | 客户分类+客户等级组合唯一性校验；ALL 等级基准额度校验；因子字段必填校验 |
| 工厂信用 | 厂端授信流程计算表单逻辑 | `Customizations/WebResources/JS/mcs_fca_proc.js` | `mcs_fca_proc` | BPF 阶段切换前校验；黑名单/逾期（>6 个月且逾期率>50%）自动判定不予授信；模型版本有效校验；同客户未生效记录唯一（新建提示跳转+保存阻断，禅道 #1644） |
| 工厂信用 | 厂端授信额度调整申请表单逻辑 | `Customizations/WebResources/JS/mcs_fca_quotaapp.js` | `mcs_fca_quotaapp` | 客户/模型序列号带出、调整后余额计算、保存校验、提交 BPP 审批；审批中(2)/通过(3) 全表单只读（mcs_bppstatus 未上表单需服务端读取，禅道 #1283）；模型序列号带出按 mcs_active 过滤唯一有效记录（禅道 #1644） |
| 工厂信用 | 厂端授信模型生效启用回写 | `Customizations/Plugins/FactoryCredit/ProcActivation/FcaProcActivationPlugin.cs` | `mcs_fca_proc`、`mcs_fca_quota`、`mcs_fca_records` | 本地独立 Assembly；状态 3 时自动创建额度生效申请单（待人工提交 BPP）；禅道 #1644：生效时本记录 mcs_active=是+同客户其他有效记录置否（一客户仅一条有效），退回计算联动置否 |
| 工厂信用 | 厂端授信额度调整申请审批回写 | `Customizations/Plugins/FactoryCredit/Bpp/Services/QuotaActivationService.cs`、`QuotaRecordService.cs` | `mcs_fca_quotaapp`、`mcs_fca_quota`、`mcs_fca_records` | 审批通过后回写额度表（余额=调整后额度-占用）并写台账 |
| 工厂信用 | 厂端授信余额调整 Custom API | `Customizations/Plugins/FactoryCredit.Api/AdjustFcaQuotaBalancePlugin.cs` | `mcs_fca_quota`、`mcs_fca_records` | 本地独立 Assembly；Custom API `mcs_AdjustFcaQuotaBalance`，初始化/占用/释放统一接口 |
| 工厂信用 | 厂端授信余额调整服务 | `Customizations/Plugins/FactoryCredit.Api/FcaQuotaAdjustService.cs` | `mcs_fca_quota`、`mcs_fca_records`、`mcs_customermasterdata`、`mcs_contract`、`mcs_order` | 本地独立 Assembly；额度调整核心逻辑（不变式：额度=余额+占用；占用防重；补偿回滚） |
| 授信池816 | 使用授信 Custom API | `Customizations/Plugins/CreditPool.Api/RecordCreditDetailPlugin.cs` | `mcs_fca_records`、`mcs_fca_quota`、`mcs_approvedquota` | 本地独立 Assembly；Custom API `mcs_recordCreditDetail`（哑记账），占用/释放/初始化+幂等台账，供交货单/订单/解款记录调用 |
| 授信池816 | 使用授信服务 | `Customizations/Plugins/CreditPool.Api/RecordCreditDetailService.cs` | `mcs_fca_records`、`mcs_fca_quota`、`mcs_customermasterdata`、`mcs_contract`、`mcs_order`、`mcs_approvedquota` | 本地独立 Assembly；FACTORY 走额度表（同旧 API 不变式），SINOSURE 不落库台账聚合净占用；释放超占用时占用金额按 0 兜底 |
| 授信池816 | 中信保上浮配置读取 | `Customizations/Plugins/CreditPool.Api/SinosureUpliftConfig.cs` | `ms_systemconfiguration` | 配置名 SinosureUpliftConfig（JSON：factor/cap/excludedCountries），缺省 ×1.5 封顶 8M |
| 授信池816 | 查询授信 Custom API | `Customizations/Plugins/CreditPool.Api/QueryCreditBalancePlugin.cs` | `mcs_fca_quota`、`mcs_fca_records`、`mcs_approvedquota`、`mcs_contract` | 本地独立 Assembly；Custom API `mcs_queryCreditBalance`，客户维度查询+传合同附带合同授信金额，给合同模块调用 |
| 授信池816 | 查询授信服务 | `Customizations/Plugins/CreditPool.Api/QueryCreditBalanceService.cs` | `mcs_fca_quota`、`mcs_fca_records`、`mcs_approvedquota`、`mcs_customermasterdata`、`mcs_contract`、`transactioncurrency` | 本地独立 Assembly；批复限额读 mcs_approvedquota（T+1），净占用台账聚合，CNY 按 transactioncurrency 汇率实时换算。⚠️ 已下沉 SanyD365.Main `AppCreditPoolService`（本地副本为旧版）：合同授信金额=该合同下有效融资管理记录 mcs_fsm_credit_amount_usd 求和，**禅道 #2150 起匹配改 `contains(mcs_contract_ids,'合同GUID')`** |
| 授信池816 | 合同交易风险敞口计算 Custom API | `Customizations/Plugins/RiskExposure.Api/CalculateRiskExposurePlugin.cs`、`RiskExposureService.cs` | `mcs_contract`、`mcs_fsm_data`、`mcs_fca_quota`、`mcs_approvedquota` | 本地独立 Assembly（远程 `D365ExtensionApi.Sales/Apis/RiskExposure`）；Custom API `mcs_CalcContractRiskExposure`（禅道 #1641），A 类=签约占用+厂端已用+风险赊销−厂端授信−中信保余额，B 类=风险赊销−外部融资授信金额；**禅道 #2150（2026-09-07）：融资记录匹配改 `mcs_contract_ids contains 合同GUID`（原单选 Lookup eq），授信金额=合同总金额（美元 `mcs_totalcontractamount_base`）×（1−首付比例 `mcs_fsm_payment_ratio`），取最新一条融资记录** |

---

## 9. 公共工具与共享库

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| 公共工具 | D365 连接工厂 | `Tools/D365ToolCommon/Connection/D365ConnectionFactory.cs` | — | 支持 ClientSecret / OAuth / Device Code；持久化 token 缓存 |
| 公共工具 | 多语言标签创建 | `Tools/D365ToolCommon/Metadata/LabelHelper.cs` | — | 默认 2052/1033 |
| 公共工具 | 字段检查/创建/更新/删除 | `Tools/D365ToolCommon/Metadata/MetadataFieldService.cs` | — | `FieldExists`、`CreateStringFieldIfNotExists`、`DeleteField`、`UpdateRequiredLevelAsync` 等；SDK 不生效时通过 Web API PUT 兜底 |
| 公共工具 | Plugin Step 配置模型 | `Tools/D365ToolCommon/Plugin/Models/StepConfig.cs` | — | Message/Entity/Stage/Mode/Filter/Rank 模型 |
| 公共工具 | Plugin/Assembly/Step 查询 | `Tools/D365ToolCommon/Plugin/PluginQueryService.cs` | — | `QueryAssemblies`、`QueryTypesByName`、`QueryStepsByType`、`QueryPluginTraceLog` 等只读查询 |
| 公共工具 | Plugin 注册/更新 | `Tools/D365ToolCommon/Plugin/PluginRegistrationService.cs` | — | Assembly + Type + Steps 一站式部署 |
| 公共工具 | Plugin Step/Type/Assembly 删除 | `Tools/D365ToolCommon/Plugin/PluginStepDeletionService.cs` | — | 注销整个 Assembly，避免 Solution 导入错误 `8004801D` |
| 公共工具 | 发布实体/元数据 | `Tools/D365ToolCommon/Publishing/PublishingService.cs` | — | `PublishEntities`、`PublishAll`；带重试；业务代码禁止调用 `PublishAll` |
| 公共工具 | Solution 组件管理 | `Tools/D365ToolCommon/Solution/SolutionComponentService.cs` | — | `AddWebResourceToSolution`、`AddEntityToSolution`、`AddComponentToSolution` |
| 公共工具 | 多语言翻译导入导出 | `Tools/D365ToolCommon/Translation/TranslationService.cs` | — | 标准 D365 翻译导入导出 |
| 公共工具 | WebResource 查询/更新/创建/发布 | `Tools/D365ToolCommon/WebResource/WebResourceService.cs` | — | `QueryByName`、`UpdateContent`、`Create`、`PublishWebResources` 等；按 ID 发布，带重试 |
| 公共工具 | WebResource 发版排查（Active 层遮挡） | `Tools/D365ToolCommon/WebResource/WebResourceReleaseCheckService.cs` | — | 只读；双环境对比存在性/内容 MD5/非托管 Active 层 content 覆盖；CLI：`MetadataTool check-webresource-release` |
| 公共工具 | 安全角色与权限服务 | `Tools/D365ToolCommon/Security/SecurityRoleService.cs` | `role`、`privilege`、`systemuserroles`、`teammembership` | 角色/角色权限/用户有效权限查询（只读）+ 角色权限调整（Add/RemovePrivilegesRole）、用户挂摘角色（写，幂等）；CLI：`MetadataTool list-role-privileges` / `query-user-permissions` / `set-role-privilege` / `assign-role` / `remove-role` |
| 公共工具 | 多语言帮助类 | `Customizations/WebResources/JS/mcs_language_helper.js` | `ms_languagefile_1033`、`ms_languagefile_2052` | 按当前用户语言加载 JSON 语言包，提供 `getLabel` |
| 公共工具 | 发版包字段对比（防 80041A06） | `Tools/release-diff/diff_solution_packages.py`、`Tools/release-diff/diff_package_vs_dev1.py` | — | 只读；发版前对比上次发版包 vs 本次内容，提前发现同名字段类型不一致；快照归档于 `Backups/Solutions/Releases/` |

---

## 10. 部署与诊断工具

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| 部署工具 | D365 部署工具 CLI | `Tools/DeployTool/Program.cs` | — | 一键 `all` 执行全部；支持 `webresource`、`appactions`、`coface`、`bpp`、`probe`、`publish` 等子命令 |
| 部署工具 | Modern Command Bar 按钮部署 | `Tools/DeployTool/AppActionDeployer.cs` | `mcs_credit_record`、`mcs_credit_scoringcard`、`mcs_trade_stpayterm` | 创建 Modern Command Bar / App Action 按钮 |
| 部署工具 | RibbonDiff.xml SDK 直接部署 | `Tools/DeployTool/RibbonDeployer.cs` | — | 操作 `ribboncustomization` 等隐藏实体 |
| 部署工具 | RibbonDiff.xml Web API 部署 | `Tools/DeployTool/RibbonWebApiDeployer.cs` | — | `ImportRibbonXml` + Web API POST 备用 |
| 部署工具 | Plugin 部署助手 | `Tools/DeployTool/DeployPlugin.cs` | — | 注册/更新 Coface/TradeStPayTerm/Bpp/CreditScore 等 Assembly/Type/Steps |
| 部署工具 | 部署 CreditScore Plugin | `Tools/DeployTool/DeployCreditScorePlugin.cs` | `mcs_credit_record` | 更新 `SanyD365.Plugins.CreditScore` |
| 部署工具 | 注册 Coface 企业搜索 Custom Action Plugin Step | `Tools/DeployTool/CofaceCustomActionDeployer.cs` | — | `mcs_CofaceSearchCompany` |
| 部署工具 | 部署 Coface 配置数据 | `Tools/DeployTool/CofaceConfigDeployer.cs` | `mcs_coface_*` | 部署国家配置、内部评分项目映射 |
| 部署工具 | 注册 CustomerTag Plugin Step | `Tools/DeployTool/RegisterCustomerTagPlugin.cs` | `mcs_customer_tag` | Update PreOp Step 注册 |
| 部署工具 | 删除 CustomerTagValidationPlugin 旧 Step | `Tools/DeployTool/DeletePluginStep.cs` | — | 按 name 删除旧 Step |
| 部署工具 | 更新 `mcs_credit_record` 表单布局 | `Tools/DeployTool/UpdateFormLayout.cs` | `mcs_credit_record` | 添加 `mcs_workflowid`/`mcs_nextapprover`/`mcs_bpplink` |
| 部署工具 | 移除进度条 Section | `Tools/DeployTool/RemoveProgressBar.cs` | `mcs_credit_record` | 从主表单移除 `section_progress_bar` |
| 部署工具 | 清除 WebResource 缓存 | `Tools/DeployTool/ClearWebResourceCache.cs` | — | 发布 `mcs_credit_record.js` / `mcs_credit_record_progress.html` |
| 部署工具 | 删除进度条 WebResource | `Tools/DeployTool/DeleteWebResource.cs` | — | 删除 `mcs_credit_record_progress.html` |
| 部署工具 | 创建 BPP 相关字段 | `Tools/DeployTool/CreateBppFields.cs` | `mcs_credit_record` | 创建 `mcs_workflowid`、`mcs_nextapprover` |
| 部署工具 | 创建逾期未回收率模型分字段 | `Tools/DeployTool/CreateOverdueField.cs` | `mcs_fca_proc` | 创建 `mcs_overduerate` (0-100) |
| 诊断工具 | 诊断信用分计算问题 | `Tools/DeployTool/DiagnoseCreditScore.cs` | `mcs_credit_record` | 状态/客户/标签/评分卡诊断 |
| 诊断工具 | 检查当前记录状态 | `Tools/DeployTool/CheckCurrentStatus.cs` | `mcs_credit_record` | 状态/BPP 字段检查 |
| 诊断工具 | 检查客户标签数据 | `Tools/DeployTool/CheckCustomerTags.cs` | `mcs_customer_tag` | 定量/定性值检查 |
| 诊断工具 | 检查字段是否存在 | `Tools/DeployTool/CheckFieldExists.cs` | `mcs_credit_record`、`account` | 字段存在性检查 |
| 诊断工具 | 检查字段名 | `Tools/DeployTool/CheckFields.cs` | `mcs_customer_tag`、`mcs_credit_scoringcard` | 字段名检查 |
| 诊断工具 | 检查 `mcs_credit_itemsno` 字段类型 | `Tools/DeployTool/CheckItemFieldType.cs` | `mcs_credit_items` | 字段类型诊断 |
| 诊断工具 | 检查 Plugin Trace 日志 | `Tools/DeployTool/CheckPluginTrace.cs` | `mcs_credit_record` | Plugin Trace 检查 |
| 诊断工具 | 检查最新记录 Coface 字段 | `Tools/DeployTool/CheckRecordFields.cs` | `mcs_credit_record` | URBA/Report/JSON/API 状态检查 |
| 诊断工具 | 检查注册资本相关字段 | `Tools/DeployTool/CheckRegisteredCapital.cs` | `mcs_credit_record`、`mcs_customer_tag`、`mcs_credit_items` | 注册资本字段检查 |
| 诊断工具 | 检查 Report JSON 注册资本数据 | `Tools/DeployTool/CheckReportJson.cs` | `mcs_credit_record` | 关键字搜索注册资本 |
| 诊断工具 | 检查 `mcs_scoreid`/`mcs_credititem` 类型 | `Tools/DeployTool/CheckScoreIdType.cs` | `mcs_credit_record` | 字段类型诊断 |
| 诊断工具 | 检查标签实际数值 | `Tools/DeployTool/CheckTagValues.cs` | `mcs_customer_tag` | 定量/定性值检查 |
| 诊断工具 | 查找 Account 国家相关字段 | `Tools/DeployTool/FindAccountFields.cs` | `account` | 按关键字 country/region/address 查找 |
| 诊断工具 | 查找 CustomerTagValidation Plugin Type | `Tools/DeployTool/FindPluginStep.cs` | `mcs_customer_tag` | 按 typename/name 查询 |
| 诊断工具 | 修复信用记录国家代码 | `Tools/DeployTool/FixCountryCode.cs` | `mcs_credit_record` | `CN` → `PL` 修复 |
| 诊断工具 | 修复 personnel 记录用户关联 | `Tools/DeployTool/FixPersonnelRecord.cs` | `mcs_personnel` | 修正 `mcs_personnel.mcs_systemuseraccount` |
| 诊断工具 | 修复用户账户映射 | `Tools/DeployTool/FixUserAccountMapping.cs` | `mcs_useraccount`、`mcs_personnel` | 创建/关联 `mcs_useraccount` → `mcs_personnel` |
| 诊断工具 | 重置信用评估记录状态 | `Tools/DeployTool/ResetCreditRecordStatus.cs` | `mcs_credit_record` | 状态回 12，清空 BPP/信用分字段 |
| 诊断工具 | 重新触发 Coface Plugin | `Tools/DeployTool/RetriggerPlugin.cs` | `mcs_credit_record` | 更新 `mcs_status=11` 触发 `CofaceDataSyncPlugin` |
| 诊断工具 | 更新 Account 国家 | `Tools/DeployTool/UpdateAccountCountry.cs` | `account` | `account.mcs_country` → Poland |
| BPP 诊断 | 检查 BPP 配置数据 | `Tools/DeployTool/CheckBppConfig.cs` | `mcs_bpp*` | BPP 相关实体/设置/环境变量检查 |
| BPP 诊断 | 探测 BPP Custom API 详情 | `Tools/DeployTool/CheckBppCustomApi.cs` | `mcs_bppstartapi` | Custom API 详情检查 |
| BPP 诊断 | 探测 BPP 环境 | `Tools/DeployTool/CheckBppEnvironment.cs` | `mcs_credit_record` | Action/字段/Plugin/Custom API 环境检查 |
| BPP 诊断 | 检查 BPP Plugin 注册详情 | `Tools/DeployTool/CheckBppPluginDetails.cs` | `mcs_credit_record` | Assembly/Type/Steps/Trace 检查 |
| BPP 诊断 | 检查 BPP 用户映射 | `Tools/DeployTool/CheckBppUserMapping.cs` | `mcs_useraccount`、`mcs_personnel` | 与 `systemuser` 映射检查 |
| BPP 诊断 | 检查 D365 用户 domainaccount | `Tools/DeployTool/CheckBppUsers.cs` | `systemuser` | 输出启用用户的 `domainname` |
| BPP 诊断 | 查找 BPP Handler 主类 | `Tools/DeployTool/FindBppHandlerMain.cs` | — | 在 Sales/Main Assembly 中查找 |
| BPP 诊断 | 探测 BPP Assembly | `Tools/DeployTool/ProbeBppAssembly.cs` | — | 查 ExtensionApi/Sales 类型、BppStart Steps |
| BPP 诊断 | 探测 BPP 框架 | `Tools/DeployTool/ProbeBppFramework.cs` | — | 查 Extension Assembly、BPP/Handler Type、可能配置实体 |
| BPP 诊断 | 探测 Domain Account | `Tools/DeployTool/ProbeDomainAccount.cs` | `systemuser` | 查 systemuser 字段、下载 Assembly 用 `strings` 分析 domainaccount |
| BPP 诊断 | 探测 personnel 字段 | `Tools/DeployTool/ProbePersonnelFields.cs` | `mcs_personnel` | 字段元数据；如无记录可自动创建 |
| BPP 诊断 | 查询用户账户 | `Tools/DeployTool/QueryUserAccount.cs` | `mcs_useraccount` | 记录结构查询 |
| BPP 诊断 | 搜索所有 BPP 类型 | `Tools/DeployTool/SearchAllBppTypes.cs` | — | 反射/元数据搜索 |
| BPP 诊断 | 搜索 BPP 配置 | `Tools/DeployTool/SearchBppConfig.cs` | — | 环境变量、`msdyn_customcontrolextendedsettings`、`mcs_bpptemplate` |
| BPP 诊断 | 下载 BPP Sales Assembly | `Tools/DeployTool/DownloadBppSalesAssembly.cs` | — | 从 `pluginassembly` 保存 DLL |
| BPP 诊断 | 下载 Extension API Assembly 并反编译 | `Tools/DeployTool/DownloadExtensionApiAssembly.cs` | — | 下载 `SanyD365.D365ExtensionApi`，使用 `monodis` 反编译 `BppStartApis` |
| BPP 测试 | 测试 BPP 流程 V2 | `Tools/DeployTool/TestBppFlowV2.cs` | `mcs_credit_record` | 13→14 异步流程，轮询 workflowId |
| BPP 测试 | 测试 BPP 集成流程 | `Tools/DeployTool/TestBppIntegrationFlow.cs` | `mcs_credit_record` | 12→13→14 端到端，含 Plugin Trace |
| BPP 测试 | 测试 BPP Start API | `Tools/DeployTool/TestBppStartApi.cs` | `mcs_credit_record` | 调用 `mcs_bppstartapi` / `mcs_bppcheckapi` |
| BPP 测试 | 测试 BPP 与飞书 | `Tools/DeployTool/TestBppWithFeishu.cs` | `mcs_credit_record` | 使用飞书账号 `gw_qiuzw` 触发审批 |
| BPP 测试 | 测试 BPP 真实模板 | `Tools/DeployTool/TestBppWithRealTemplate.cs` | `mcs_credit_record` | 模板 Code `781094754802827383` |
| BPP 测试 | 测试 personnel 查询方式 | `Tools/DeployTool/TestPersonnelQuery.cs` | `mcs_personnel` | 6 种 FetchXML/QueryExpression 查询 |
| BPP 诊断 | 查看 BPP Plugin Trace | `Tools/DeployTool/ViewBppPluginTrace.cs` | `mcs_credit_record` | `BppIntegrationPlugin` 完整日志 |
| 信用分诊断 | 查看 Credit Score Trace | `Tools/DeployTool/ViewCreditScoreTrace.cs` | `mcs_credit_record` | Credit Score 相关 trace |


---

## 11. 元数据管理工具

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| 元数据工具 | D365 元数据管理工具 CLI | `Tools/MetadataTool/Program.cs` | — | 命令分发器：实体/字段/表单/视图创建、WebResource 部署、Plugin 注册、翻译导入导出、信用相关诊断与同步等 |
| 元数据工具 | 禅道 #2090 OverdueModel 改定性数据配套 | `Tools/MetadataTool/Program.cs`（`update-overdue-model-qualitative` 命令） | `mcs_credit_items`、`mcs_credititem_value`、`mcs_credit_scoringcard`（写） | 幂等：评分项目改定性+说明 → 建 S01~S10+O 枚举 → 重建 OverdueModel 评分卡分档行（权重与生产 0829 新卡一致：直销缺失档 17/经销商 15）→ 迟付指数补定性 O 缺失档行（直销 2/经销商 3） |
| 元数据工具 | 实体/字段/表单/视图/Plugin/WebResource 综合管理 | `Tools/MetadataTool/Services/EntityManager.cs` | — | `CreateEntity`、`CreateStringField`、`CreatePicklistField`、`CreateLookupField`、`BindJsToForm`、`RegisterPlugin`、`CreateCreditItemRecords` 等核心元数据操作 |
| 元数据工具 | JSON 实体/字段定义模型 | `Tools/MetadataTool/Models/EntityDefinition.cs` | — | `EntityDefinition`、`FieldDefinition`、`LoadFromJson`、`SaveToJson` |
| 元数据工具 | 多语言标签辅助（本地副本） | `Tools/MetadataTool/Helpers/LabelHelper.cs` | — | 与 D365ToolCommon 逻辑一致 |
| 元数据工具 | 列出指定前缀实体/统计记录数 | `Tools/MetadataTool/Services/ListEntitiesHelper.cs` | — | `ListByPrefix`、`CountRecords` |
| 元数据工具 | 检查/修复客户主数据基础字段 | `Tools/MetadataTool/Services/CheckAndFixAccountMasterDataHelper.cs` | `account`、`mcs_customermasterdata` | `CheckAndFixByAccountName` |
| 元数据工具 | 清空客户主数据信用字段 | `Tools/MetadataTool/Services/ClearAccountCreditFieldsHelper.cs` | `mcs_customermasterdata` | `ClearByName` 清空 8 个信用字段 |
| 元数据工具 | Coface 基础数据导入导出 | `Tools/MetadataTool/Services/CofaceDataSyncHelper.cs` | `mcs_coface_*` | NACE mapping、汇率配置导入导出 |
| 元数据工具 | 合同产品明细诊断 | `Tools/MetadataTool/Services/ContractProductDiagnosticHelper.cs` | `mcs_contract`、`mcs_contractdetail` | 已签待执行合同产品名称诊断 |
| 元数据工具 | 信用评估记录集成诊断 | `Tools/MetadataTool/Services/CreditRecordDiagnosticHelper.cs` | `mcs_credit_record` | 检查 record / tags / files / scoring card / trace |
| 元数据工具 | Custom API 部署 | `Tools/MetadataTool/Services/CustomApiDeployer.cs` | — | `DeployTradeStPayTermQueryApi`、`DeployFcaQuotaAdjustApi`、`DeployCofacePlaceOrderApi`、`DeleteCustomApi`、`BindPluginType` |
| 元数据工具 | 客户画像 WebResource 发布 | `Tools/MetadataTool/Services/PublishProfileWebResources.cs` | — | 阻塞检测/重试发布 |
| 元数据工具 | 查询 Custom API | `Tools/MetadataTool/Services/QueryCustomApis.cs` | — | `ListCustomApis` 含参数与响应属性 |
| 元数据工具 | 查询 Plugin Steps | `Tools/MetadataTool/Services/QueryPluginSteps.cs` | — | `QueryStepsByNamespace`、`QueryAssemblyVersion` 等 |
| 元数据工具 | 设置客户主数据重点尽调标志 | `Tools/MetadataTool/Services/SetMasterDataIsddHelper.cs` | `mcs_customermasterdata` | `SetByAccountName` 设置 `mcs_isdd` |
| 元数据工具 | 客户/客户主数据 Coface ID 与国家编码维护 | `Tools/MetadataTool/Services/CustomerCofaceIdHelper.cs` | `account`、`mcs_customermasterdata` | CLI：`customer-coface <编号或名称>`（只读查询）、`set-customer-coface <客户编号|主数据GUID> <icon#ID>`（同步更新主数据+关联 account，格式校验与编号重复保护）、`set-customer-country <客户编号|主数据GUID> <国家编码>`（改 `mcs_countrycode`）、`clear-customer-coface <客户编号|主数据GUID>`（清除误绑，含关联 account） |
| 元数据工具 | 查询/同步客户画像 | `Tools/MetadataTool/Services/SyncAccountProfileHelper.cs` | `account`、`mcs_credit_record` | `ShowProfile`、`SyncProfile`、`SimulateApproval` |
| 元数据工具 | 共享库隔离测试 | `Tools/MetadataTool/Services/TestCommonService.cs` | `mcs_test_common` | 创建/删除测试实体 |
| 元数据工具 | 上传相关 Custom API 测试 | `Tools/MetadataTool/Services/UploadApiTester.cs` | — | `McpUploadFile`、`mcs_InitUploadFile`、`mcs_CommitUploadFile`、`mcs_GenerateUploadFileUrl` |
| 元数据工具 | 实体元数据定义文件 | `Tools/MetadataTool/Definitions/*.json` | — | `mcs_fca_mdlconfig`、`mcs_fca_mdlversion`、`mcs_fca_proc`、`mcs_fca_quota`、`mcs_fca_quotaapp`、`mcs_fca_records`、`mcs_trade_ptgrouptype`、`mcs_trade_pttype`、`mcs_trade_stpayterm` 等 |
| 元数据工具 | 评分卡导入数据 | `Tools/MetadataTool/Data/scoring_cards_import.json` | `mcs_credit_scoringcard` | 评分卡配置导入数据 |
| 元数据工具 | Coface 配置示例 | `Tools/MetadataTool/examples/*.json` | `mcs_coface_*`、`mcs_credit_record` | NACE mapping、汇率、信用评估记录示例 |
| 元数据工具 | WebResource 源文件副本 | `Tools/MetadataTool/WebResources/JS/*.js` | — | `mcs_account.js`、`mcs_credit_record.js`、`mcs_credit_scoringcard.js` 等副本，用于 MetadataTool 部署 |
| 元数据工具 | 发版 Solution 完整性自检 | `Tools/MetadataTool/Program.cs`（`check-release` 命令） | `solutioncomponent` 等（只读） | 读发版清单 JSON，核对实体/字段/WebResource/Plugin(Assembly+Step)/Custom API/App Action 是否在对应 Solution（实体→entity_XX、WR→McsWebResource、Plugin→McsPlugin、CustomAPI→McsCustomAPI），只读不改动；清单放 `Documents/Planning/Releases/` |
| 元数据工具 | 主清单 Solution 批量加组件 | `Tools/MetadataTool/Program.cs`（`add-manifest-to-solution` 命令） | `solutioncomponent` 等（写） | 把清单组件批量加入指定 Solution（幂等）；用于维护 `AllComponent_Peter_NoUAT` 主清单（永不导入 UAT，仅供发版对照）；需用户明确授权 |
| 元数据工具 | 主清单 Solution 分布核对 | `Tools/MetadataTool/Program.cs`（`check-solution-coverage` 命令） | `solutioncomponent` 等（只读） | 以源 Solution（如 `AllComponent_Peter_NoUAT`）为真相源，按开发手册 4.4 核对其组件是否已分布到发版包（WebResource→McsWebResource、OptionSet→McsOptionSet、工作流→McsAutomate、Custom API 及其实现 Assembly/Step→McsCustomAPI、其余 Assembly+Step→McsPlugin、实体/站点地图/App Action→实体包由第二个参数指定如 `entity_20260722`、角色→role_XX 跳过）；只读不改动 |

---

## 12. 辅助工具

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| Plugin 注册辅助 | Plugin / Step 自动化注册 | `Customizations/PluginRegistrationHelper/Program.cs` | `pluginassembly`、`plugintype`、`sdkmessageprocessingstep` | 自动化注册/更新 Assembly、Plugin Type 与 Steps；覆盖 Account、CreditRecord、CreditItems、CreditItemValue、CustomerTag、ScoringCard 等插件 |
| Plugin 注册辅助 | 项目配置 | `Customizations/PluginRegistrationHelper/PluginRegistrationHelper.csproj` | — | .NET Framework 4.8 控制台应用 |
| Solution 浏览 | Solution 组件浏览器 | `Tools/SolutionViewer/server.js` | — | Node.js Web 应用，用于浏览 D365 Solution 组件 |
| Solution 浏览 | 项目配置 | `Tools/SolutionViewer/package.json` | — | Node.js 依赖配置 |
| 禅道同步 | 禅道同步工具 | `Tools/ZentaoSync/` | — | 禅道数据同步工具（详见该目录 README） |
| 任务看板 | 任务进度看板（待处理/开发中/待发布/已发布） | `Tools/KanbanBoard/` | — | 纯 Python 标准库 + SQLite，部署于 tx-windows:8100（计划任务 KanbanBoard 常驻，开机自启）；AI 通过 REST API 录入/更新任务 |

---

## 13. 语言包与多语言资源

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| 多语言 | 公共多语言标签（英文） | `Customizations/WebResources/Language/1033.json` | — | 公共英文标签；**追加更新，禁止覆盖** |
| 多语言 | 公共多语言标签（中文） | `Customizations/WebResources/Language/2052.json` | — | 公共中文标签；**追加更新，禁止覆盖** |
| 多语言 | 评分卡多语言测试包（英文） | `Customizations/WebResources/Language/credit_test_1033.json` | — | `CreditScoringCard_*` 英文标签 |
| 多语言 | 评分卡多语言测试包（中文） | `Customizations/WebResources/Language/credit_test_2052.json` | — | `CreditScoringCard_*` 中文标签 |

---

## 14. 项目配置文件

| 模块 | 功能点 | 代码路径 | 涉及实体 | 备注 |
|---|---|---|---|---|
| 配置 | 环境配置 | `SanyD365Project/config/environment.json` | — | D365 环境地址等配置 |
| E2E 测试 | Playwright E2E 测试 | `SanyD365Project/Tests/e2e/*.ts` | — | 认证、基础流程、主流程 E2E 测试 |
| E2E 测试 | Playwright 配置 | `SanyD365Project/Tests/playwright.config.ts` | — | Playwright 测试配置 |
| E2E 测试 | 依赖配置 | `SanyD365Project/Tests/package.json` | — | Node.js 测试依赖 |

---

## 维护说明

1. **AI 修改代码前必须查阅本索引**：每次修改 `Code/` 下源代码前，先检索 `Code/INDEX.md`，确认相关功能点、涉及实体和代码路径，避免误改或遗漏关联文件。
2. **新增功能点必须登记**：创建新的 Plugin、JS、ServiceJob、Tool、实体、WebResource 或 HTML 页面时，必须在对应模块下新增一行，确保路径准确、功能描述清晰。
3. **删除/重命名文件必须同步更新索引**：避免移交或后续开发时路径失效。
4. **实体窗体/视图文件较多时**，可合并为「xxx 表窗体/视图集合」一行，但必须在备注中说明具体目录。
5. **本地独立 Assembly 与远程主项目代码需区分**：本地独立 Assembly 文件名以 `SanyD365.Plugins.<模块>.dll` 输出；同步到远程主项目后需在备注中标注。
6. **禁止覆盖公共文件**：语言包等公共文件更新时必须追加，详见 `Code/Customizations/AGENTS.md`。
7. **元数据创建必须使用公共方法**：详见 `Code/Tools/README.md` 与 `Code/Customizations/AGENTS.md`。
8. **索引更新纳入完成检查清单**：功能开发完成后，应像更新测试用例、Bug 记录一样，检查 `Code/INDEX.md` 是否已同步。

---

## 15. 项目工程文件与文档

### 15.1 Plugin / ServiceJob / Tool 项目文件

以下 `.csproj` 为各功能模块对应的 .NET 项目配置文件，与上方功能点一一对应：

| 模块 | 项目文件路径 | 说明 |
|---|---|---|
| 客户管理 | `Customizations/Plugins/Account/AutoNumber/AccountPlugins.csproj` | Account 校验插件项目 |
| 客户管理 | `Customizations/Plugins/CustomerMasterData/Validation/CustomerMasterDataPlugins.csproj` | 客户主数据校验插件项目 |
| 客户管理 | `Customizations/Plugins/CustomerFile/AutoNumber/CustomerFilePlugins.csproj` | 客户资信附件编号插件项目 |
| 客户管理 | `Customizations/Plugins/CustomerTag/AutoNumber/CustomerTagPlugins.csproj` | 客户标签插件项目 |
| 信用评估管理 | `Customizations/Plugins/CreditRecord/AutoNumber/CreditRecordPlugins.csproj` | 信用评估自动编号插件项目 |
| 信用评估管理 | `Customizations/Plugins/CreditRecord/Validation/CreditRecordValidationPlugins.csproj` | 信用评估状态校验插件项目 |
| 信用评分与评分卡 | `Customizations/Plugins/CreditItems/AutoNumber/CreditItemsPlugins.csproj` | 评分项目校验插件项目 |
| 信用评分与评分卡 | `Customizations/Plugins/CreditItemValue/AutoNumber/CreditItemValuePlugins.csproj` | 评分项目枚举值校验插件项目 |
| 信用评分与评分卡 | `Customizations/Plugins/ScoringCard/AutoNumber/ScoringCardPlugins.csproj` | 评分卡插件项目 |
| 信用评分与评分卡 | `Customizations/Plugins/CreditScore/CreditScore.csproj` | 信用分计算插件项目 |
| Coface 集成 | `Customizations/Plugins/CofaceIntegration/CofaceIntegration.csproj` | Coface 集成插件项目 |
| BPP 集成 | `Customizations/Plugins/BppIntegration/BppIntegration.csproj` | BPP 集成插件项目 |
| 贸易条款 | `Customizations/Plugins/TradeStPayTerm/TradeStPayTermPlugins.csproj` | 贸易条款插件项目 |
| 贸易条款 | `Customizations/Plugins/TradeStPayTerm.Api/TradeStPayTermApiPlugins.csproj` | 贸易条款查询 API 插件项目 |
| 工厂信用 | `Customizations/Plugins/FactoryCredit/FactoryCreditPlugins.csproj` | 工厂信用插件项目 |
| 工厂信用 | `Customizations/Plugins/FactoryCredit.Api/FactoryCreditApiPlugins.csproj` | 厂端授信余额调整 API 插件项目 |
| 授信池816 | `Customizations/Plugins/CreditPool.Api/CreditPoolApiPlugins.csproj` | 使用授信 API（816 授信池）插件项目 |
| 融资管理 | `Customizations/Plugins/FinancingManagement/FinancingManagementPlugins.csproj` | 融资管理插件项目 |
| Plugin 注册辅助 | `Customizations/PluginRegistrationHelper/PluginRegistrationHelper.csproj` | Plugin 注册辅助工具项目 |
| ServiceJob | `ServiceJobs/CreditRecordExpiration/CreditRecordExpiration.csproj` | 信用评估过期处理作业项目 |
| 公共工具 | `Tools/D365ToolCommon/D365ToolCommon.csproj` | D365 公共工具库项目 |
| 部署工具 | `Tools/DeployTool/DeployTool.csproj` | 部署工具项目 |
| 元数据工具 | `Tools/MetadataTool/D365MetadataTool.csproj` | 元数据管理工具项目 |
| Coface 工具 | `Tools/CofaceApiTest/CofaceApiTest.csproj` | Coface API 测试工具项目 |
| Coface 工具 | `Tools/CofaceConfigImporter/CofaceConfigImporter.csproj` | Coface 配置导入工具项目 |

### 15.2 其他工程/临时文件

| 模块 | 功能点 | 代码路径 | 备注 |
|---|---|---|---|
| 部署工具 | 信用评估记录表单调试副本 | `Tools/DeployTool/mcs_credit_record.js` | DeployTool 目录下的 `mcs_credit_record.js` 副本，通常用于本地调试或部署比对 |
| 部署工具 | 表单调试 XML | `Tools/DeployTool/form_debug.xml` | 表单布局调试临时文件 |

### 15.3 说明文档

| 模块 | 文档路径 | 说明 |
|---|---|---|
| 代码目录总说明 | `Code/Customizations/README.md` | Customizations 目录结构与用途说明 |
| Customizations 规范 | `Code/Customizations/AGENTS.md` | Customizations 目录 AI 协作规范与开发红线 |
| 评分卡模块说明 | `Customizations/Plugins/ScoringCard/README.md` | 评分卡插件模块说明 |
| 同步跟踪 | `Customizations/SYNC_TRACKING.md` | 本地代码与远程主项目同步跟踪 |
| 工具目录说明 | `Code/Tools/README.md` | Tools 目录用途与元数据创建红线 |
| 部署工具说明 | `Tools/DeployTool/README.md` | DeployTool 使用说明 |
| 元数据工具说明 | `Tools/MetadataTool/README.md` | MetadataTool 使用说明 |
| Solution 浏览器说明 | `Tools/SolutionViewer/README.md` | SolutionViewer 使用说明 |
| 禅道同步说明 | `Tools/ZentaoSync/README.md` | ZentaoSync 使用说明 |
| SanyD365 项目说明 | `Code/SanyD365Project/README.md` | SanyD365Project 项目说明 |
| E2E 测试说明 | `SanyD365Project/Tests/e2e/README.md` | Playwright E2E 测试说明 |

---

## 16. 不在本索引范围内的文件说明

以下文件/目录属于 **Solution 导出产物、构建产物或第三方依赖**，不作为功能代码列入本索引：

| 类型 | 路径示例 | 原因 |
|---|---|---|
| Solution 解包产物 | `SanyD365Project/solutions/unpacked*/**` | Solution 导出/解包后的元数据副本 |
| Solution 托管/非托管包 | `SanyD365Project/solutions/managed/`、`unmanaged/` | Solution 打包文件 |
| 实体导出包 | `SanyD365Project/Exports/entity_*/**` | 实体导出产物 |
| 编译输出 | `**/bin/`、`**/obj/` | 编译生成的 DLL/PDB |
| Python 虚拟环境 | `Tools/ZentaoSync/venv/` | 第三方依赖环境 |
| Node.js 依赖 | `**/node_modules/` | 第三方依赖包 |
| E2E 测试报告 | `SanyD365Project/Tests/playwright-report/` | Playwright 测试报告 |
| 已编译 DLL | `Customizations/Plugins/_Deploy/*.dll` | 编译产物 |

