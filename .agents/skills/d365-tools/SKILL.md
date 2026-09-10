---
name: d365-tools
description: D365 项目部署工具使用指南。适用于 MetadataTool CLI、DeployTool 已封装方法、Modern Command Bar（App Action）按钮部署。
---

# D365 工具使用指南

## 1. MetadataTool CLI 速查

> **适用场景**：实体/字段/表单/视图/Plugin/JS 的创建、修改、查询、导出

> **🚨 红线**：所有元数据操作必须使用 `D365ToolCommon` 或 `MetadataTool` 已有公共方法。严禁直接调用 `CreateAttributeRequest`/`CreateEntityRequest` 等 SDK 原生 API，严禁临时编写新方法。公共方法缺失须先申请，获批准后方可扩展。

```bash
cd Code/Tools/MetadataTool

# ========== 实体操作 ==========
dotnet run create <json文件>              # 从JSON创建实体+字段（EntityDefinition格式）
dotnet run list-fields <实体名>           # 列出实体所有字段（含类型、必填等）
dotnet run delete-field <实体名> <字段名>  # 删除指定字段
dotnet run add <实体名> <解决方案名>       # 添加实体到解决方案
dotnet run remove <实体名> <解决方案名>    # 从解决方案移除实体

# ========== 字段操作 ==========
dotnet run update-field-default <实体名> <字段名> <默认值>  # 设置字段默认值（选项集/布尔/整数传整数值，小数/金额传数字）
dotnet run update-field-format <实体名> <字段名> <text|url|...>  # 更新 String 字段格式（如审批链接改 url 渲染为超链接；原地更新不影响数据）
# 注意：JSON 定义中 string 字段支持可选 "format": "url" 属性（FieldDefinition.Format → CreateStringField format 参数）
# 以下方法在 EntityManager.cs 中，需写 C# 代码调用：
# CreateStringField()   - 创建文本字段
# CreateMemoField()     - 创建多行文本
# CreateIntegerField()  - 创建整数字段（可设min/max）
# CreateDecimalField()  - 创建小数字段
# CreateMoneyField()    - 创建货币字段
# CreateDateTimeField() - 创建日期字段
# CreatePicklistField() - 创建选项集字段
# CreateBooleanField()  - 创建布尔字段
# CreateLookupField()   - 创建Lookup字段
# UpdatePicklistOptions() - 更新选项集值
# InsertOptionValue()   - 插入选项值

# ========== 表单操作 ==========
dotnet run update-form <实体名>           # 批量添加字段到主窗体（两列布局）
dotnet run add-form-field <实体名> <字段名> <显示名>  # 添加单个字段到主窗体（幂等，自动发布；2026-07-29 新增，复用 UpdateMainForm）
dotnet run update-form-field-label <实体名> <字段名> <中文> [英文]  # 更新主窗体字段单元格标签 2052/1033（自动发布；2026-07-29 新增）
dotnet run replace-form-field <实体名> <旧字段> <新字段> <中文标签> [英文标签]  # 原位替换主窗体字段（保留单元格位置，自动发布；2026-09-03 新增，禅道 #2138；用于新建字段替代旧字段场景，避免 remove+add 落位错误）
dotnet run check-form <实体名>            # 检查窗体字段清单
dotnet run export-formxml <实体名> <路径>  # 导出窗体XML到文件
dotnet run rearrange-form <实体名>        # 重新排列窗体字段（按预定义分组）
# UpdateMainForm()      - 添加字段到主section（C#调用）
# CleanFormFooter()     - 清理footer中的错误字段

# ========== 视图操作 ==========
dotnet run check-view <实体名>            # 检查所有视图字段
dotnet run update-view <实体名>           # 更新默认视图字段
dotnet run update-lookup-view <实体名>     # 更新Lookup弹出视图
dotnet run export-lookup-view <实体名> <路径> # 导出Lookup视图FetchXml
# UpdateDefaultView()   - 更新默认视图（C#调用）
# UpdateLookupView()    - 更新Lookup视图（C#调用）
# RemoveFieldFromViews() - 从所有视图移除字段

# ========== WebResource / JS ==========
dotnet run deploy-js <JS文件路径>         # 部署JS到WebResource
dotnet run deploy-html <HTML文件路径> [显示名称]  # 部署HTML到WebResource（type=1）
dotnet run publish-webresource <名称1> [名称2] ...  # 发布指定WebResource
# DeployWebResource()   - 部署WebResource（C#调用，支持type参数：1=HTML, 3=JS）
# BindJsToForm()        - 绑定JS到表单事件（C#调用）

# ========== Plugin ==========
dotnet run register-plugin <DLL路径> <类名> [实体名]  # 不传实体=仅注册 Assembly+Type（2026-08-20 起）；传实体=注册 Create Step
# ⚠️ 2026-08-20 新增防线（#1641 幽灵 Step/跨包依赖事故）：被 Custom API 绑定的类禁止注册实体 Step，
#    register-plugin / register-plugin-update / register-plugin-advanced / register-step-only / create-plugin-step-with-id 全部强制拦截
dotnet run check-step-assembly [Solution唯一名]  # 跨包依赖检查：包内每个 Step 的实现类所在 Assembly 是否同包（只读，默认 McsPlugin）
# 离线导入仿真（预热）：python3 Code/Tools/release-diff/simulate_import.py --target uat|prod [--batch 按序包列表]
#    发 UAT/生产前各跑一遍；build-registry 重建生产台账；seed-from-uat 生成实体基线。详见 release-diff/README.md
# RegisterPlugin()      - 注册Plugin（C#调用）
# RegisterPluginWithFilter() - 注册带筛选属性的Update Plugin

# ========== 发布 ==========
dotnet run publish [实体名]               # 发布指定实体（不传则PublishAll）
# PublishEntity()       - 发布单个实体
# PublishAll()          - 发布所有自定义项

# ========== 解决方案 ==========
dotnet run export <解决方案名> <路径>      # 导出Solution为ZIP
# ExportSolution()      - 导出解决方案

# ========== 发版自检（只读，不改动环境） ==========
dotnet run --no-build -- check-release <清单.json> [--with-fields]
# 核对清单中的组件是否都在对应 Solution（实体/App Action→entitySolution、
# WebResource→McsWebResource、Plugin Assembly+Step→McsPlugin、Custom API→McsCustomAPI）
# --with-fields：额外逐字段核对实体 mcs_* 自定义字段是否随包
# 自动发现兜底（2026-07-27 新增）：对清单 entities 逐实体反查 appaction（contextentity），
#   清单 appActions 漏写的按钮也会核对是否在 entitySolution（App Action 不是实体子组件，
#   加实体进 Solution 不会自动带入）；已停用按钮仅提示不判缺失；
#   同实体同 JS 函数已有等价按钮在包内的重复副本（如 Command Designer 副本）仅提示不误报
# 清单 JSON 放 Documents/Planning/Releases/，格式：
# { "name":"...", "entitySolution":"entity_XXXXXXXX",
#   "entities":[], "webresources":[], "pluginTypes":[], "customApis":[], "appActions":[] }

# ========== WebResource 发版排查（只读，2026-07-27 新增） ==========
export D365_URL="https://dev1.crm5.dynamics.com"   # 基准环境（清单所在环境）
dotnet run --no-build -- check-webresource-release <基准Solution唯一名> --target <目标环境URL>
# 核对清单 Solution 内全部 WebResource 在目标环境：① 是否存在 ② 运行时内容MD5是否一致
# ③ 是否被非托管 Active 层遮挡(content 覆盖)。症状“已发布但 JS 行为还是旧版”时必跑
# 解读与修复见 /skill:d365-deploy 第 5.4 节；实现：D365ToolCommon.WebResourceReleaseCheckService

# ========== App Action 查询（只读） ==========
dotnet run --no-build -- list-app-actions [uniquename前缀] [--entity <实体名>]
# 按前缀和/或上下文实体过滤；--entity 按 contextentity 反查该实体全部现代命令栏按钮

dotnet run --no-build -- list-appaction-rules [uniquename前缀]
# 列出按钮关联的经典显隐规则（appaction_appactionrule_classicrules N:N，只读）
# 用途：勾选消失修复（attach-selection-rule）逐环境核对；2026-07-27 实测该 N:N 关联**不随 Solution 导入**，
#   UAT 导入按钮后必须重跑 DeployTool attach-selection-rule，否则按钮退回「未勾选显示、勾选消失」默认行为

dotnet run --no-build -- check-solution-coverage <源Solution唯一名> [实体包Solution唯一名]
# 反向核对：以源 Solution（如 AllComponent_Peter_NoUAT 主清单）为真相源，
# 按开发手册 4.4 核对其组件是否已分布到发版包：
#   WebResource→McsWebResource、OptionSet→McsOptionSet、普通工作流→McsAutomate、BPF(category=4)→实体包、
#   Custom API 及其实现 Assembly/Step→McsCustomAPI、其余 Assembly+Step→McsPlugin、
#   实体/站点地图/App Action→实体包（每次发版新建需传第二个参数，如 entity_20260722）、
#   角色→role_XX（每版本一包，跳过）
#
# 注：以上命令只核对组件归属/覆盖，不核对发布顺序。发布顺序统一按
# /skill:d365-deploy 4.1 的 14 环节固定顺序矩阵执行；本批次用不到的包标记跳过，不删除环节。

# ========== 主清单 Solution 维护（写操作，需用户明确授权） ==========
dotnet run --no-build -- add-manifest-to-solution <清单.json> <Solution唯一名>
# 把清单组件批量加入指定 Solution（幂等：已存在自动跳过）
# 实体含全部子组件；pluginTypes 只加 Step 不加 Type 本体；customApis 含参数/响应属性
# 用途：维护 AllComponent_Peter_NoUAT 主清单 Solution（永不导入 UAT，仅供发版对照）
# 组件源清单：Documents/Planning/Releases/allcomponent-peter.json

# ========== 测试数据 ==========
dotnet run create-credit-items            # 创建评分项目测试数据(22条)
dotnet run create-qualitative-enums       # 创建定性枚举值测试数据(30条)

# ========== 导入/组件分布诊断（只读，2026-08-19 #1928 新增） ==========
dotnet run --no-build -- query-import-log <Solution名称关键字>
# 查该 Solution 最近一次导入的 importjob 日志：根结构标签统计、非 success 的 result 明细、
#   sitemap/appmodule 相关节点。用于排查「导入成功但某组件没生效」（如 AppModuleSiteMap 是否真处理）

dotnet run --no-build -- query-sitemap-layers <sitemapId>
# 查指定 sitemap 组件出现在哪些 Solution 的 solutioncomponent 中（含 Active 层判断），
#   用于排查共享 sitemap（92 个包共携带）导入后未生效的分层问题

# ========== 角色权限工具（2026-08-21 新增，实现：D365ToolCommon.Security.SecurityRoleService） ==========
dotnet run --no-build -- list-role-privileges <角色关键字> [实体名过滤]
# 只读：角色权限明细（读/写/建/删/追加/追加到/分派/共享 × 深度）；不传实体名输出按实体分组的紧凑表 + 杂项权限
# 注意：同名角色存在多 BU 副本（如 LTC Regional Sales Operations 在 DEV1 有 92 个副本），只读命令逐副本输出

dotnet run --no-build -- query-user-permissions <用户domainname> [实体名过滤]
# 只读：用户全部角色（标注直接/团队继承）+ RetrieveUserPrivileges 有效权限汇总（同名取最大深度）

dotnet run --no-build -- set-role-privilege <角色关键字> <实体名> <权限类型> <深度>
# 【写，需用户明确授权】权限类型: read|write|create|delete|append|appendto|assign|share
#   深度: none(移除)|user(本人)|bu(本部门)|childbu(本部门及子部门)|org(组织)
#   幂等（已是目标状态跳过）；输出变更前→变更后并回读确认；多 BU 副本/多匹配一律拒绝要求精确名称；
#   禁止改 System Administrator/System Customizer；D365 权限变更即时生效无需发布

dotnet run --no-build -- assign-role <用户domainname> <角色名>     # 【写，需授权】挂角色（幂等）
dotnet run --no-build -- remove-role <用户domainname> <角色名>     # 【写，需授权】摘角色（幂等）
#   角色名优先精确匹配；对当前连接账号自己操作时警告；写命令首行均打印目标环境 URL
```

---

## 2. DeployTool 已封装方法

```csharp
// 已封装方法，直接使用：
UpdateWebResource(ServiceClient service, string webResourceName, string filePath)
// 更新 JS WebResource 内容（Base64编码自动处理）

DeployAppActions(ServiceClient service)
// 部署 Modern Command Bar 按钮（App Action），替代 RibbonDiff.xml

AppActionDeployer.UpdateButtonParameters(string uniqueName, string parametersJson)
// 更新已有按钮的 onclickeventjavascriptparameters（幂等；uniquename 先精确后前缀匹配）
// CLI：dotnet run update-appaction-params <按钮uniquename或前缀> <参数JSON>
// 参数 type 枚举（实测锁定）：5=PrimaryControl，12=SelectedControl，23=SelectedControlSelectedItemIds
// 用途：禅道 #1160 为批量按钮追加 SelectedControl 参数（[{"type":23},{"type":12}]）实现 grid.refresh()
// ⚠️ 仅限 DEV 使用：UAT 自定义项变更必须走 Solution 发布，禁止直连修改；
//    UAT 现代按钮为本地独立副本（componentidunique 与 DEV 不同），参数只能由用户在 UAT Command Designer 手动同步

AppActionDeployer.DeleteAppAction(string uniqueName)
// CLI：dotnet run delete-appaction <按钮uniquename或前缀>
// 删除按钮记录（先自动解除全部经典规则 N:N 关联，否则外键冲突）；不从 Solution 移除组件引用

AppActionDeployer.SetButtonInactive(string uniqueName, bool inactive)   // 2026-08-15 新增（Bug #1834）
// CLI：dotnet run set-appaction-inactive <按钮uniquename或前缀> <true|false>
// statecode=Inactive 真正隐藏按钮（不删组件，幂等）；⚠️ isdisabled 只禁点不隐藏，不能当隐藏用
// ⚠️ 各环境 appaction GUID 不同（UAT/生产为导入重建），跨环境操作必须按 uniquename，禁用 DEV 的 GUID 更新其他环境

AppActionDeployer.AttachFirstPartySelectionCountRule(string uniqueName)
// CLI：dotnet run attach-selection-rule <按钮uniquename或前缀>
// 关联第一方规则 msdyn_Mscrm.SelectionCountAtLeastOne!0（各环境均内置，但规则 ID 各环境不同，命令内按 uniquename 反查）
// + visibilitytype=2（Classic Rules），效果：未勾选隐藏、勾选≥1条显示
// 用途：修复「勾选记录后按钮消失」（微软官方 KB 4481268 设计使然：无选中计数规则的按钮勾选后一律隐藏）
// ⚠️ 场景收窄（2026-07-27）：该 N:N 关联不随 Solution 导入，每个目标环境都要重跑——生产不允许直连时不可行。
//    新建需显隐随包的按钮一律用 RibbonDiffXml（见第 4 章）；本命令仅用于存量 appaction 按钮的应急修复。
// 官方依据：
//   1. KB 4481268（勾选消失 by design）：https://learn.microsoft.com/en-us/troubleshoot/dynamics-365/sales/button-in-command-bar-not-appear-after-grid-item-selection
//   2. 命令组件库限制（不可跨 App/跨环境）：https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/command-designer-limitations
//   3. appaction 表参考（N:N 关系 IsCustomizable=False，不随 Solution 导入）：https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/appaction
// 注意：① 第三方禁止创建 appactionrule（报 First party Solutions 错误），只能复用第一方规则；
//      ② N:N 关联必须用 Associate 消息，直接 Create 交叉实体不支持；
//      ③ 改完客户端需「清除站点数据」才能看到效果（命令栏缓存于 Cache Storage+Service Worker，硬刷新无效）

PublishEntity(ServiceClient service)
// 只发布指定实体（默认使用，避免全局阻塞）

PublishAll(ServiceClient service)
// 执行 PublishAllXmlRequest 发布所有自定义项（需用户批准）
```

**使用流程：**
1. 修改本地文件（JS/XML/C#）
2. 运行 DeployTool：`cd Code/Tools/DeployTool && dotnet run`
3. 验证 D365 界面效果

---

## 3. 发布规范（强制）

> 所有 WebResource / 实体发布必须统一使用 `D365ToolCommon` 中的通用发布服务，**禁止**在业务代码中直接构造 `PublishXmlRequest`。

### 3.1 通用发布服务

| 发布对象 | 服务类 | 方法 |
|---------|--------|------|
| WebResource | `D365ToolCommon.WebResource.WebResourceService` | `PublishWebResources(params string[] names)` |
| 实体 | `D365ToolCommon.Publishing.PublishingService` | `PublishEntities(params string[] entityNames)` |
| 全部元数据 | `D365ToolCommon.Publishing.PublishingService` | `PublishAll()`（⚠️ 需用户明确授权） |

### 3.2 为什么必须走通用服务

1. **WebResource 发布用 GUID**：直接传名称给 `PublishXmlRequest` 在某些环境下会报 `An unexpected error occurred.`；通用服务会先查询 `webresourceid`，再用 GUID 发布，稳定可靠。
2. **统一重试机制**：通用服务内置 3 次重试，避免偶发网络/环境抖动导致发布失败。
3. **统一 ParameterXml 格式**：包含 `<nodes/>`、`<securityroles/>`、`<settings/>`、`<workflows/>`，与 D365 UI 单个"发布"按钮行为一致。
4. **避免重复代码**：禁止每个工具都写一份发布逻辑。

### 3.3 代码示例

```csharp
using D365ToolCommon.WebResource;
using D365ToolCommon.Publishing;

// 发布 WebResource
var wrService = new WebResourceService(serviceClient);
wrService.UpdateFromFile("mcs_trade_stpayterm.js", jsPath);
wrService.PublishWebResources("mcs_trade_stpayterm.js");

// 发布实体
var pubService = new PublishingService(serviceClient);
pubService.PublishEntities("mcs_credit_record");
```

### 3.4 已封装的 CLI 命令

```bash
# 发布 WebResource
cd Code/Tools/MetadataTool
dotnet run publish-webresource <名称1> [名称2] ...

# 发布实体
cd Code/Tools/MetadataTool
dotnet run publish <实体名>

# 厂端授信余额调整 Custom API（2026-07-22 新增）
dotnet run deploy-fcaquota-api <DLL路径> [Plugin类名]   # 注册 Assembly + 创建/更新 Custom API（默认类名为远程主项目类）
dotnet run delete-fcaquota-api                          # 删除 mcs_AdjustFcaQuotaBalance
dotnet run test-fcaquota-api <客户编码> <金额> <环节> <动作> [合同编码] [订单编码]

# 通用数据查询/清理（2026-07-22 新增）
dotnet run query-records <实体名> <字段列表,逗号分隔> [条数]   # 只读，按 createdon 倒序
dotnet run delete-record <实体名> <记录GUID>                   # 单条删除，用于测试数据清理

# BPF 进度条可见性诊断（2026-07-28 新增，只读）
dotnet run check-bpf-access <用户domainname> [BPF名称关键字=信用评估]
# 基于 RetrieveUserPrivileges（含直接+团队继承角色）核对用户对 BPF 主实体与流程实体的 Read 权限
# 症状「能打开记录但看不到进度条」= 角色漏配 BPF 流程实体（如 mcs_credit）Read，
# 配置入口：Security Role →「业务流程」页签；详见统一角色权限矩阵 3.5 节

# Money 字段取值范围（2026-07-22 新增）
dotnet run update-field-range <实体名> <字段名> <最小值> <最大值>
```

---

## 4. 命令栏按钮部署（🚨 2026-08-01 用户明确：跨环境发布一律 RibbonDiffXml）

> **核心原则（2026-08-01 再次修订，用户指定并记入 AGENTS.md 红线）：凡需要随包跨环境（DEV→UAT→生产）发布的命令栏按钮，一律用 RibbonDiffXml 内联在实体自定义中随实体包走；App Action 现代按钮仅限 DEV 临时验证场景。导包改实体前必须先问用户用哪个载体包，严禁自行选择。**

### 4.1 两种载体对比与选型

| 维度 | App Action（SDK 创建） | RibbonDiffXml |
|------|------|------|
| UI 可维护性 | 可编辑/可删除 | **只读**，无法 UI 编辑，删除需改 XML 重导 |
| 部署方式 | SDK 直建，秒级 | 只能随 Solution 导入（阻塞环境数分钟） |
| 始终显示 + JS 拦截 | ✅ 首选 | 不必要 |
| **显隐规则随包**（如 SelectionCountRule） | ❌ 经典规则靠 N:N 关联 `appaction_appactionrule_classicrules`（`IsCustomizable=False`），**永不随包**；Power Fx 公式必须注册进命令组件库（不可跨环境） | ✅ **规则内联在 XML，随实体包走，生产零手动步骤** |

**选型结论（2026-08-01 用户定稿）**：跨环境发布的按钮 → 一律 RibbonDiffXml；App Action 仅限 DEV 临时验证。**载体包必须先问用户**（红线），导出重导会带上包内全部组件（含他人实体），擅自选包影响面不可控。
**案例**：`Code/Customizations/Ribbon/mcs_trade_stpayterm.ribbon.xml`（3 个列表批量按钮，内联 `SelectionCountRule Minimum=1`，1033/2052 LocLabels，CrmParameter `SelectedControlSelectedItemIds`+`SelectedControl` 与 JS 签名一一对应）；`Code/Customizations/Ribbon/mcs_credit_record.ribbon.xml`（表单按钮【Coface 下单】，`Mscrm.Form.{entity}.MainTab.Management.Controls._children`，CrmParameter `PrimaryControl`）。部署用 `MetadataTool deploy-ribbon <实体> <xml> <载体Solution> [工作目录] [幂等前缀]`（导出载体包→合并实体 Ribbon 节点→重打包→非托管导入→发布，幂等）。
**注意**：Ribbon 按钮在 UCI 固定落入「更多命令」溢出菜单（两轮实测：Sequence/ModernImage 不影响位置，主栏只渲染现代命令；Sequence 决定溢出菜单内排序、ModernImage 提供图标）；Location 表单用 `Mscrm.Form.{entity}.MainTab.Management.Controls._children`、列表用 `Mscrm.HomepageGrid.{entity}.MainTab.Management.Controls._children`；同一实体不要 Legacy + App Action 混用同一功能按钮（会重复）。**DEV1 迭代可用本机 pac CLI 导入**（`pac solution import`，已有 peter_qiuzw profile），撞锁报 `Cannot start another [Import]` 错峰重试。

### 4.1.1 🚨 按钮显隐问题排障 SOP（2026-08-15 花一整天换来的，强制执行）

> 适用：「按钮该隐藏却还在 / 该显示却不显示 / 按钮点了没反应」类问题。**禁止不查就改、禁止用 API 回读当验证。**

**第一步永远是定位按钮来源（10 分钟，别猜）：** URL 加 `&ribbondebug=true` → `⋯` 菜单 →「命令检查器」→ 树里点该按钮，看三样东西：
| 看什么 | 判读 |
|---|---|
| 按钮 Id | `mcs.{entity}.xxx.Button` = 经典 ribbon（RibbonDiffXml 引入）；uniquename 形如 `mcs_xxx_apply` 无 `mcs.` 前缀 = App Action 现代按钮 |
| SolutionUniqueName / 解决方案层 | 按钮来自哪个层（Active=非托管层） |
| Display/Enable rules | 哪条规则在控制显隐/可用 |

**按来源对症下药：**
| 来源 | 隐藏/移除的正确做法 | 错误做法（都踩过） |
|---|---|---|
| App Action 现代按钮 | `DeployTool set-appaction-inactive <uniquename> true`（statecode=1，不删组件） | ❌ `isdisabled=true` 只禁点不隐藏；❌ 按 DEV 的 GUID 更新其他环境（各环境 GUID 不同，导入重建） |
| 经典 ribbon 按钮（已在环境 Active 层存在） | **RibbonDiffXml 加 `HideCustomAction` 显式隐藏**（HideActionId=原 CustomAction Id + Location 一致），随实体包导入 | ❌ **注释/清空 XML 再导入=空 diff，平台不移除 Active 层已有按钮**（2026-08-15 DEV1/UAT 双实锤） |

**生效三件套（ribbon 变更缺一不可）：** ① 导入成功（看 importjob completedon/结果列）→ ② **Publish 成功**（撞锁会静默失败，必须回查，n8n 自动 Publish 实测撞锁失败过）→ ③ **Regenerate ribbon metadata**（命令检查器顶部按钮，后台任务 15~30 分钟，状态在 Solutions History「Ribbon Metadata Generation Operations」视图）。

**验证只认 UI 实证（2026-08-15 铁律）：** 清站点数据（Cache Storage+Service Worker，硬刷新无效）或无痕窗口 → 勾选 1 条记录 → 看主命令栏+溢出菜单 → 截图留证。❌ **禁止用 `RetrieveEntityRibbon`（get-entity-ribbon）当验证依据**——它读的存储与 UCI 渲染用的预计算 blob 不是一套，会出现「API 显示干净、UI 按钮还在」的假象（已实锤）。

**环境锁判读：** `list-failed-imports` 列出的是 progress<100 记录，**含已失败/已完成的，不等于正在跑**；判断是否真锁要看 completedon 是否有值 / 结果列。微软第一方包（OmnichannelPrime 等）导入会长时间持锁，只能等。

### 4.2 App Action 正确创建方式

**文件位置：** `Code/Tools/DeployTool/AppActionDeployer.cs`

**使用示例：**
```csharp
var deployer = new AppActionDeployer(serviceClient);
deployer.DeployButtons();
```

**单按钮创建方法签名：**
```csharp
void CreateButton(
    string uniqueName,      // 唯一名，如 "mcs_credit_record_refresh_data"
    string label,           // 显示文本，如 "数据集成刷新"
    string tooltip,         // 悬停提示
    string functionName,    // JS 函数名，如 "CreditRecordForm.refreshDataIntegration"
    Guid webResourceId,     // mcs_credit_record.js 的 WebResource ID
    Guid entityId,          // mcs_credit_record 实体在 entity 表的 ID
    int sequence            // 排序号，如 100100016
)
```

### 4.3 关键字段类型对照表（必须严格匹配）

| 字段 | 类型 | 示例值 | 常见错误 |
|------|------|--------|---------|
| `context` | `OptionSetValue` | `new OptionSetValue(1)` | — |
| `contextentity` | `EntityReference` | `new EntityReference("entity", entityId)` | 误用 `string` |
| `location` | `OptionSetValue` | `new OptionSetValue(0)` | — |
| `onclickeventtype` | `OptionSetValue` | `new OptionSetValue(2)` | — |
| `sequence` | `decimal` | `(decimal)100100016` | 误用 `int` 或 `double` |
| `statecode` | `OptionSetValue` | `new OptionSetValue(0)` | — |
| `statuscode` | `OptionSetValue` | `new OptionSetValue(1)` | — |
| `type` | `OptionSetValue` | `new OptionSetValue(0)` | — |
| `visibilitytype` | `OptionSetValue` | `new OptionSetValue(0)` | — |

### 4.4 获取 entityId 的方法

```csharp
var query = new QueryExpression("entity")
{
    ColumnSet = new ColumnSet("entityid"),
    Criteria = new FilterExpression
    {
        Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "mcs_credit_record") }
    }
};
var entityId = service.RetrieveMultiple(query).Entities[0].Id;
```

> ⚠️ **注意**：`entity` 表的 `name` 字段是逻辑名（如 `mcs_credit_record`），不是显示名。

### 4.5 按钮参数格式

**PrimaryControl 参数：**
```csharp
appAction["onclickeventjavascriptparameters"] = "[{\"type\":5}]";
```

**对应 JS 函数签名：**
```javascript
CreditRecordForm.refreshDataIntegration = function (primaryControl) {
    // primaryControl 是 formContext
};
```

---

*本文件记录 D365 部署工具使用指南，跨项目可复用。*
*更新规则：发现新的工具命令或部署方法时更新。*
