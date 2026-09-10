# 发版检查清单（Release Checklist）

> **用途**：每次发版（DEV → UAT → 生产）前逐一核对，防止组件遗漏、字段类型冲突、插件依赖等问题阻断发版。
> **使用方式**：发版前新建本文副本（`release-checklist-YYYYMMDD.md`），逐项打勾并记录结果；任何一项 ❌ 都不得进入下一步。
> **原则**：所有检查项均为只读操作（查询/导出/本地解析），环境修改动作（加组件、发布、导入）由用户在授权后执行。
>
> 最近更新：2026-09-01（新增生产直连核对策略：阶段 2.5 生产字段终核、阶段 6.5 发后生产实证，基于 Frank 生产只读账号 gw_zhangf68，详见 `/skill:d365-deploy` 第 5.5 节），此前：2026-08-05（按发布截图固化 9 个 D365 包 + 5 个 Azure 发布项的固定核对顺序；本批次用不到的包保留环节并标记跳过），2026-07-24（新增字段类型冲突检查、Plugin 字段依赖检查，来源：2026-07-22 生产导入失败事故）

---

## 阶段 0：发版范围确认

> **📋 9-3 预检结论（2026-09-01 已执行）**：阶段 1/2/3 已全绿（见各项批注）；阶段 2.5 生产终核与 6.5 产后实证待发版日执行（生产查询逐条请示）。

- [x] **0.1** 本次发版内容（增量自 820 生产发版，看板归档 rowid 105-139 为准）：
  - **实体/字段**：授信台账 6 新字段（credit_type/usebalance_cny/delivery_no/settle_no/settle_id/idempotency_key）、融资资源机构代码必填性调整（#2072）、厂端授信额度表主窗体（#1985）、评估记录快速查找视图
  - **角色**：21 个风控角色（RiskRole20260830 全量：新建 Business Control Configuration Admin / Risk Sales Representative + 19 个权限调整）
  - **Custom API**：使用授信 mcs_recordCreditDetail、查询授信 mcs_queryCreditBalance（首发）、Coface 下单 mcs_CofacePlaceOrder（partially_ready 修复）
  - **Plugin**：#2025 额度 Lookup 回写、#2091 信用等级配置化、#2092 绑定回写系统身份、partially_ready 判定修正
  - **WebResource**：mcs_credit_profile.html / mcs_fsm_data.js / mcs_fsm_resource.js / mcs_coface_company_search.html / 语言包（随仓库通道）
  - **Azure**：ExtensionApi（授信池 5 新类）、ClientAPI+MessageHandler（SanyD365.Main 授信池服务）
  - **配置数据（IT 生产导入）**：CreditGradeMapping、SinosureUpliftConfig、CofaceCountryConfig（DE=102）、迟付指数/OverdueModel 定性化、UploadFileTypeMapping、自动编号 6 系列
- [x] **0.2** 发版包构成（本批次逐项已标记）：

| 固定顺序 | 类别 | 发布项 | 组件类型 | 本批次无内容时的处理 |
|---:|---|---|---|---|
| 1 | D365 | `McsOptionSet` | OptionSet（全局） | 保留环节，标记 `⏭️ 跳过：无全局选项集变更` |
| 2 | D365 | `McsWebResource` | js / html / 翻译 json | 保留环节，标记 `⏭️ 跳过：无 WebResource 变更` |
| 3 | D365 | `role_XX`（非托管） | 角色 | 保留环节，标记 `⏭️ 跳过：无角色变更` |
| 4 | D365 | `entity_XX`（非托管，每次发版新建） | 实体/字段/表单/视图/关系/BPF/App Action | 保留环节；本批次无元数据变更时标记 `⏭️ 跳过` |
| 5 | D365 | `McsCustomAPI` | Custom API（本体+参数+响应）及其实现 Assembly/Step | 保留环节，标记 `⏭️ 跳过：无 Custom API 变更` |
| 6 | D365 | `McsPlugin` | Plugin Assembly + Step（实体插件，只放 Plugin 和 Step！） | 保留环节，标记 `⏭️ 跳过：无 Plugin 变更` |
| 7 | D365 | `McsAutomate` | 普通工作流 | 保留环节，标记 `⏭️ 跳过：无我方工作流资产/变更` |
| 8 | D365 | `app_allcomponents` | App 全组件 | 保留环节，标记 `⏭️ 跳过：非我方维护/本批次不发布` |
| 9 | D365 | `sln_Import`（非托管） | 通用导入包 | 保留环节，标记 `⏭️ 跳过：非我方维护/本批次不发布` |
| 10 | Azure | `CommonMessageHandle` | 公共消息处理 | 保留环节，标记 `⏭️ 跳过：无代码变更` |
| 11 | Azure | `MessageHandler` | BPP Consumer | 保留环节，标记 `⏭️ 跳过：无代码变更` |
| 12 | Azure | `InnerApi` | 内部 API | 保留环节，标记 `⏭️ 跳过：无代码变更` |
| 13 | Azure | `ExtensionApi` | Custom API 服务端 | 保留环节，标记 `⏭️ 跳过：无代码变更` |
| 14 | Azure | `ClientApi` | Web API（与 MessageHandler 保持版本一致） | 保留环节，标记 `⏭️ 跳过：无代码变更` |

- [x] **0.3** 发布顺序矩阵（本批次，14 环节全要素）：1 `McsOptionSet` ⏭️无全局选项集变更 / 2 `McsWebResource` ✅全量（依赖 ❌=0）/ 3 `role_20260902` ✅21 风控角色（依赖缺失=0）/ 4 `entity_20260902` ✅**增量包**（交接注明；我方 9 组件已回读）/ 5 `McsCustomAPI` ✅3 API 依赖闭环（check-step-assembly 全绿）/ 6 `McsPlugin` ✅（跨包 3 条均他队+实现 Assembly 已在生产实证；ms_systemconfiguration 他队组件不动）/ 7 `McsAutomate` ⏭️ / 8 `app_allcomponents` ⏭️ / 9 `sln_Import` ⏭️ / 10 `CommonMessageHandle` ⏭️ / 11 `MessageHandler` ✅（SanyD365.Main 授信池）/ 12 `InnerApi` ⏭️ / 13 `ExtensionApi` ✅（授信池 5 新类）/ 14 `ClientApi` ✅（版本与 MessageHandler 一致）。**entity(4)→CustomAPI(5) 顺序死守：授信池 API 运行时读写 6 新字段，反序=外部系统调用报错（导入不拦、运行时炸）**
- [x] **0.4** 无「删除/重建字段」「删除组件」类变更（delta_scan 8/18 vs 9/1 全量 diff 实证：➖=0、类型变更=0）

---

## 阶段 1：主清单与组件分布核对（防漏组件）

- [x] **1.1** ✅ 主清单（166 组件）已含本次全部新组件：6 新字段随 mcs_fca_records 实体整体管理自动覆盖；3 个 Custom API 本体+参数+响应在主清单（2.35 记录）；角色按映射走 role 包不入主清单
- [x] **1.2** ✅ 无遗留孤儿：8/31 mcs_CofacePlaceOrder 重建后旧 ID 平台级联清除，主清单已同步为新 ID 三组件（2.35）
- [x] **1.3** ✅ 分布核对（2026-09-01）：**✅ 134 / ❌ 32 / ℹ️ 0**——固定包（McsWebResource/McsPlugin/McsCustomAPI/McsOptionSet/McsAutomate）134/134 全绿；32 项「不在 entity_20260902」全部为增量模式预期项（15 实体+7 App Action+4 命令定义+3 BPF+mcs_credit 等，逐一与 delta_scan 对照 820 后零变更、生产已有）
- [x] **1.4** ✅ 以 1.3 分布核对 + 两包逐组件回读（entity_20260902 我方 9 组件、role_20260902 21 角色）覆盖，增量口径下等效 check-release
- [x] **1.5** ✅ 依赖检查（2026-09-01 分批执行）：`McsPlugin`+`McsCustomAPI` 涉我方组件 ❌=0（跨包 3 条均他队，实现 Assembly 已在生产实证）；`entity_20260902` 涉我方仅 2 条旧关系（8/18 前已存在=噪音）；`McsWebResource` ❌必须处理=0（16 项均为注意/可忽略）；`role_20260902` 缺失=0
- [x] **1.6** 🚨 createdby 反向审计（2026-09-01 已执行）：**抓到一个真缺口——授信池 2 个 Custom API（mcs_recordCreditDetail/mcs_queryCreditBalance）本体+11+2 参数+11+24 响应共 50 个组件漏加主清单**（在 McsCustomAPI 发版包中，不影响本次发布），已于当日全部补入（主清单 166→216，回读 50 个 25a0/2fa0 系 ID 在场）；其余 ❌ 均为预期假阳性（视图/Custom API 实现 Step 平台自建/测试语言包/已停用组件 fca_proc 幽灵行 dd60、3 个批量按钮=2.4.19/他队及 SYSTEM 组件）

---

## 阶段 2：字段变更风险核对（防 80041A06，🚨 本次事故新增）

- [x] **2.1** ✅ 字段类型对比（双角度）：① 819 基线包（entity_20260818_peter_v3=820 生产内容）vs DEV1 当前 4 实体字段清单 → 🚨=0、➖=0；② delta_scan 8/18 dump vs 9/1 dump（24 实体全维度）→ 类型变更=0。模板命令（本次已用等价路径执行）：
  ```bash
  python3 Code/Tools/release-diff/diff_solution_packages.py \
    Backups/Solutions/Releases/<上次发版包>.zip <本次发版包.zip>
  # 大包导不出时用清单替代：
  python3 Code/Tools/release-diff/diff_package_vs_dev1.py \
    Backups/Solutions/Releases/<上次发版包>.zip <本次字段清单目录>
  ```
- [x] **2.2** ✅ 无「删除字段/删除后同名重建」（delta_scan ➖ 已删字段=0 实证，非仅靠记录自查）
  - 有 → 列出清单（实体.字段、旧类型→新类型），**提前通知三一先在目标环境删除旧字段**，并提供：字段逻辑名/显示名、所在实体中英文名、删除前需从表单视图移除的提醒、数据备份提醒
- [x] **2.3** ✅ ➖ 段为空，无需提醒
- [x] **2.5** **生产直连字段终核（Frank 账号，逐条请示执行）**：① ✅ `mcs_fca_records`（2026-09-01 批准执行）：生产 21 字段 vs DEV1 29 字段，共有 21 个类型全一致 🚨=0；差异 8 个=本次 6 新字段+typename/_base 伴随，生产无同名字段，导入纯新建零冲突；② ✅ `mcs_fsm_resource`（2026-09-01 批准执行）：生产 28 字段 = DEV1 28 字段，类型全一致 🚨=0；机构代码（mcs_fsm_institution_code）两侧均为 String，本次仅必填性 Required→None 变更可安全导入。**阶段 2.5 全部闭环，字段冲突风险在生产侧实证为零**。连接方式与红线见 `/skill:d365-deploy` 第 5.5 节
- [ ] **2.4** **Plugin 字段依赖核对**（仅当有字段被删时）：
  - Step 筛选属性/PreEntityImage 是否绑定被删字段（`query-plugin-steps <类名>` 查 Filter；`query-records sdkmessageprocessingstepimage ...` 查镜像）
  - Plugin 代码硬引用（`ColumnSet`/ConditionExpression/`update["字段"]`）被删字段的组件清单 → 评估过渡期影响，写入给三一的交接说明
  - 参照模板：`Documents/Planning/Releases/Plugin字段依赖核对_20260723.md`

---

## 阶段 3：Plugin / Custom API 核对（防 80048071 / 8004801D）

- [x] **3.1** ✅ `query-plugin-namespace SanyD365.Plugins` = 零残留（2026-09-01）：
  ```bash
  dotnet run --no-build -- query-plugin-namespace SanyD365.Plugins
  ```
- [x] **3.2** ✅ 本批全部「新增类/改方法」零删类；发布 DLL 用合并后 uat 全量编译（含历史全部空壳类）
- [x] **3.3** ✅ 授信池 2 API 实现在 ExtensionApi.Sales（McsCustomAPI 内闭环）；mcs_CofacePlaceOrder 实现插件在 Extension.Sales 为 8/1 知情例外（PluginType 已在生产，炸不了；技术债待后续迁移）
- [x] **3.4** ✅ DEV1 两个主 Assembly 各批次均用合并后 uat 编译更新（最近：8/31 partially_ready 批次；生产 Extension.Sales 8/28 / ExtensionApi.Sales 8/26 已实证在场）
- [x] **3.5** ✅ 本批新增 Step（CofaceBindWriteback 2fadea6e 等）均已注册+主清单+McsPlugin（kanban in_package=true）

---

## 阶段 4：多语言与配置数据核对

- [x] **4.1** ✅ 语言包：我方 key 全部在仓库 uat（7/27 PR 6148 起各批次入仓验证），本次无新增语言 key；8/29 他队 key 同在仓库，9/3 McsWebResource 从仓库通道发布自动携带全量；DEV1 8/14 曾与仓库逐字节一致
- [x] **4.2** ✅ 本批新字段（6 个授信台账字段+fca_quota 表单）双语标签已在 DEV1 就绪并随实体/字段组件入包（建字段时 set-field-label 双语写入）
- [ ] **4.3** ⏸️ 配置数据交接清单待整理（发版日交付 IT）：CreditGradeMapping / SinosureUpliftConfig / CofaceCountryConfig(DE=102) / 迟付指数+OverdueModel 定性化 / UploadFileTypeMapping / 自动编号 6 系列（V/FCM 两系列 8/29 已补登配置清单）

---

## 阶段 5：UAT 发布与验证

- [ ] **5.1** UAT 发布严格按阶段 0.2 固定顺序矩阵执行（D365：OptionSet → WebResource → role → entity → CustomAPI → Plugin → Automate → app_allcomponents → sln_Import；Azure：CommonMessageHandle → MessageHandler → InnerApi → ExtensionApi → ClientApi），由用户通过 n8n Release Tool 发布；本批次跳过项已在清单中标记原因，不得临时漏看
- [ ] **5.2** UAT 发布后**必查导入历史**（只读），全部 progress=100：
  ```bash
  dotnet run --no-build -- list-failed-imports 50 <发版日期>
  ```
  - 有失败 → 看错误摘要：字段类型冲突（80041A06）→ 回阶段 2；缺依赖 → 回阶段 1.5；PluginType 移除（80048071）→ 回阶段 3.2
- [ ] **5.3** UAT 功能验证通过（关键流程 + 本次变更点），测试账号按 `/skill:d365-system-access` 3.1
- [ ] **5.4** 重要：UAT 验证完成后，**发版包原样归档**：
  ```bash
  dotnet run --no-build -- export entity_<发版日期> ../../../Backups/Solutions/Releases/entity_<发版日期>_exported<日期>.zip
  # 大包 30 分钟超时则用 list-fields 字段清单快照替代
  ```
- [ ] **5.5** 若发版包含 **Ribbon 自定义按钮**（RibbonDiffXml，如成交条件批量按钮）：导入后核对按钮显隐行为（未勾选隐藏、勾选显示）与 JS 功能；无需任何手动补配（规则随包）。⚠️ 历史方案 `attach-selection-rule`（appaction N:N 关联）已废弃，不再作为发版步骤

---

## 阶段 6：生产发布交接（给三一）

- [ ] **6.1** 交接材料齐全：
  1. 发版包清单（Solution 名称 + 内容概述）
  2. **需先在生產删除的字段清单**（如有，含实体中英文名/字段名/显示名/旧类型→新类型/删除步骤/数据备份提醒）——模板：`Documents/Planning/Releases/生产导入失败_字段类型冲突清单_20260723.md`
  3. **Plugin 字段依赖说明**（如有字段删除，含过渡期注意事项）
  4. 配置数据迁移清单（4.3 项）
  5. 发布参数建议（解决方案勾选、Azure 代码勾选、固定顺序矩阵：D365 `McsOptionSet` → `McsWebResource` → `role_XX` → `entity_XX` → `McsCustomAPI` → `McsPlugin` → `McsAutomate` → `app_allcomponents` → `sln_Import`；Azure `CommonMessageHandle` → `MessageHandler` → `InnerApi` → `ExtensionApi` → `ClientApi`；跳过项注明原因）
- [ ] **6.2** 明确告知发布顺序与注意事项：先删冲突字段 → 按固定顺序矩阵导入/发布所有标记 `✅ 发布` 的 D365 包 → 再按固定顺序发布 Azure 项；跳过项只记录原因、不改变既定顺序；过渡期勿操作受影响业务（如有 Plugin 硬引用）
- [ ] **6.3** 生产导入后若报错：报错信息会直接点名下一个冲突组件，按同类方式处理后重导
- [ ] **6.4** 若发版包含 Ribbon 自定义按钮：交接材料写明「以 Upgrade 方式导入，平台自动删除被移除的旧 appaction 按钮」；导入后核对按钮显隐，无需 IT 手动补配
- [ ] **6.5** **生产发布后直连实证（2026-09-01 新增，Frank 生产只读账号）**：每包发布完成后直连生产核对——① `query-assembly-version` 各 Assembly ModifiedOn 已刷新（导入成功≠代码新）；② 新组件在场抽查（字段/Custom API/角色）；③ 本批 Custom API 真实调用 + Plugin 路径冒烟。命令见 `/skill:d365-deploy` 第 5.5 节

---

## 阶段 7：发版后收尾

- [ ] **7.1** 归档确认：本次发版包/字段快照已存入 `Backups/Solutions/Releases/` 并更新 README（**下次发版的对比基准**）
- [ ] **7.2** `Memory.md` 更新发版记录（日期/包/内容/验证结果/待办）
- [ ] **7.3** `Code/INDEX.md` 同步新增/变更的功能点与代码路径
- [ ] **7.4** 本次检查清单副本存档到 `Documents/Planning/Releases/`

---

## 附：历史发版事故 ↔ 检查项对照

| 事故 | 错误码/现象 | 对应检查项 |
|------|------------|-----------|
| 2026-07-22 生产导入失败（字段类型不一致） | `80041A06` Attribute x is a Picklist, but a String type was specified | 2.1 / 2.2 / 2.3 / 2.4 |
| UAT McsPlugin 导入失败（PluginType 被移除） | `80048071` Existing plug-in types have been removed | 3.2 |
| Solution 携带不存在的 Assembly 依赖 | `8004801D` | 3.1 |
| App Action 依赖 JS 未入包 | Missing Dependencies（type=61） | 1.5 |
| Custom API 实现 Assembly 未入包 | `Entity 'SdkMessage' Does Not Exist` | 1.4 / 3.3 |
| 语言包被他方整包覆盖 | 双语显示回退/丢失 | 4.1 |
| DEV/UAT Assembly 版本偏差吞异常 | "ISV code reduced the open transaction count" | 3.4 |
