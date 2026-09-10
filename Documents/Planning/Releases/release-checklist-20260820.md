# 发版检查清单（Release Checklist）

> **用途**：每次发版（DEV → UAT → 生产）前逐一核对，防止组件遗漏、字段类型冲突、插件依赖等问题阻断发版。
> **使用方式**：发版前新建本文副本（`release-checklist-YYYYMMDD.md`），逐项打勾并记录结果；任何一项 ❌ 都不得进入下一步。
> **原则**：所有检查项均为只读操作（查询/导出/本地解析），环境修改动作（加组件、发布、导入）由用户在授权后执行。
>
> **本副本**：2026-08-20 生产发版专用（2026-08-17 预建）。
> **🚨 2026-08-18 口径变更（增量发版，用户明确）**：本次不发全量包，只发 **805 之后有新增/改动的组件**。实体包=`entity_20260818_peter`（DEV1 已建，增量模式：改名实体 behavior=1 含元数据、其余实体 behavior=2 纯壳、只含变更子组件）；McsPlugin/McsWebResource/McsCustomAPI 固定包照旧整包发。
> **增量判定方法（系统层面，不看板）**：`Code/Tools/_DeltaScan` dump DEV1/UAT 元数据 → `Code/Tools/release-diff/delta_scan.py` 与 805 基线包（`Backups/Solutions/entity_20260727_peter_uat_20260805.zip` 等）diff；产物存档 `Backups/TempTest/deltascan_20260818/`。
> **⚠️ 导出后必做**：剥除 account Ribbon（`strip_entity_ribbon.py <zip> Account`，见上线核对清单 2.4.22）。
> **已知不可裁决项**：RequiredLevel 在导出 XML 中有损（ApplicationRequired/BusinessRequired 都序列化为 required），必填性差异不进包、以生产实测为准。
> **已完成项（2026-08-17 预审计）**：阶段 1.6 createdby 反向审计已提前执行（工具 `Code/Tools/_TempQuery`，输出存档 `Backups/TempTest/audit_20260820_prerelease.txt`），结果见文末「预审计结论」。
> 最近更新：2026-08-05（按发布截图固化 9 个 D365 包 + 5 个 Azure 发布项的固定核对顺序；本批次用不到的包保留环节并标记跳过），此前：2026-07-24（新增字段类型冲突检查、Plugin 字段依赖检查，来源：2026-07-22 生产导入失败事故）

---

## 阶段 0：发版范围确认

- [ ] **0.1** 明确本次发版内容：涉及哪些实体/字段/JS/HTML/Plugin/Custom API/App Action/BPF/工作流/角色
- [ ] **0.2** 明确发版包构成（对照开发手册 4.4 映射 + 2026-08-05 截图固定顺序）：

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

- [ ] **0.3** 已生成本次「发布顺序矩阵」：14 个环节逐项标记 `✅ 发布` 或 `⏭️ 跳过+原因`；**不得因为本批次不用而从清单中删除任何环节**
- [ ] **0.4** 确认是否有「删除/重建字段」「删除组件」类变更 → 有则直接进入阶段 2 重点核对

---

## 阶段 1：主清单与组件分布核对（防漏组件）

- [ ] **1.1** 主清单 `AllComponent_Peter_NoUAT` 已包含本次全部新组件（DEV 验证通过后应第一时间加入；临时 Assembly/测试组件/他人组件不得加入）
- [ ] **1.2** 已从环境删除的组件，已同步从主清单移除（防孤儿组件）
- [ ] **1.3** 跑分布核对（只读），**必须全绿**：
  ```bash
  cd Code/Tools/MetadataTool
  dotnet run --no-build -- check-solution-coverage AllComponent_Peter_NoUAT entity_<发版日期>
  ```
- [ ] **1.4** 跑发版自检（只读），**必须全绿**：
  ```bash
  dotnet run --no-build -- check-release ../../../Documents/Planning/Releases/release-<日期>-<主题>.json
  ```
- [ ] **1.5** 跑依赖检查（只读），无缺失依赖（历史教训：`mcs_fsm_data.js` 未入包导致 App Action 依赖失败、`SdkMessage Does Not Exist`）。**只传发布顺序矩阵中标记为 `✅ 发布` 的包；`⏭️ 跳过` 项保留在矩阵但不传命令**：
  ```bash
  dotnet run --no-build -- check-solution-deps McsOptionSet McsWebResource role_<发版日期> entity_<发版日期> McsCustomAPI McsPlugin McsAutomate app_allcomponents sln_Import
  ```
- [ ] **1.6** 🚨 **createdby 反向审计**（只读，2026-08-04 新增）：coverage 是「主清单→发版包」单向核对，**主清单自己缺组件永远全绿**。每次发版前必须反向审计「我创建的组件是否都在主清单」（历史教训：2026-08-04 审计出 7 个 App Action + 5 个 Step 漏网）：
  ```bash
  cd Code/Tools/_TempQuery && dotnet run --no-build   # createdby=gw_qiuzw 反向审计
  ```
  预期假阳性（无需处理）：视图（随实体隐式分发）、Custom API 实现 Step（平台自动创建）、测试语言包 `ms_languagefile_credit_test_*`、他人实体（项目全文零引用验证）

---

## 阶段 2：字段变更风险核对（防 80041A06，🚨 本次事故新增）

- [ ] **2.1** **字段类型对比**：本次发版内容 vs 上次发版归档快照，🚨 段必须为空：
  ```bash
  python3 Code/Tools/release-diff/diff_solution_packages.py \
    Backups/Solutions/Releases/<上次发版包>.zip <本次发版包.zip>
  # 大包导不出时用清单替代：
  python3 Code/Tools/release-diff/diff_package_vs_dev1.py \
    Backups/Solutions/Releases/<上次发版包>.zip <本次字段清单目录>
  ```
- [ ] **2.2** 自查本次开发周期内是否有「删除字段 / 删除后同名重建」操作（查 Memory.md 会话记录 + 实体定义变更）
  - 有 → 列出清单（实体.字段、旧类型→新类型），**提前通知三一先在目标环境删除旧字段**，并提供：字段逻辑名/显示名、所在实体中英文名、删除前需从表单视图移除的提醒、数据备份提醒
- [ ] **2.3** ➖ 段（已删字段）非空时，提醒三一 update 模式导入不会自动删除，建议手动清理废弃字段
- [ ] **2.4** **Plugin 字段依赖核对**（仅当有字段被删时）：
  - Step 筛选属性/PreEntityImage 是否绑定被删字段（`query-plugin-steps <类名>` 查 Filter；`query-records sdkmessageprocessingstepimage ...` 查镜像）
  - Plugin 代码硬引用（`ColumnSet`/ConditionExpression/`update["字段"]`）被删字段的组件清单 → 评估过渡期影响，写入给三一的交接说明
  - 参照模板：`Documents/Planning/Releases/Plugin字段依赖核对_20260723.md`

---

## 阶段 3：Plugin / Custom API 核对（防 80048071 / 8004801D）

- [ ] **3.1** 无遗留临时独立 Assembly（本地测试 Assembly 测试完必须注销 Step/Type/Assembly，否则 Solution 导入报 `8004801D`）：
  ```bash
  dotnet run --no-build -- query-plugin-namespace SanyD365.Plugins
  ```
- [ ] **3.2** 本次 Assembly 无「移除 PluginType」变更；若有废弃插件，按客户惯例**保留空壳类**（Obsolete 标记、Execute 空实现），或提升 Assembly major/minor 版本号（否则托管环境报 `80048071`）
- [ ] **3.3** Custom API 实现插件归 `SanyD365.D365ExtensionApi.*` 项目（严禁放业务插件项目），实现 Assembly/Step 在 `McsCustomAPI` 包内
- [ ] **3.4** DEV Assembly 已用合并后 `uat` 代码重新编译并更新（严禁用未入仓代码编译的 DLL）；已核对 `pluginassembly.modifiedon`（不要只看 version）
- [ ] **3.5** 新增 Step / PreEntityImage 已注册并加入主清单

---

## 阶段 4：多语言与配置数据核对

- [ ] **4.1** 语言包只追加不覆盖：`Code/Customizations/WebResources/Language/1033.json`/`2052.json`（本地唯一数据源）→ 与 DEV 逐条 diff，只同步差异 key；同步前已导出 DEV 备份；**严禁整包覆盖 `ms_languagefile_1033/2052`**；非我们的 key 一律不动
- [ ] **4.2** 元数据翻译：translations 导出 → 填词 → 导入 → 逐字段核对 1033/2052 双语共存（注意 `ss:Index` 列错位、source 文本可能在 1033 列）
- [ ] **4.3** 配置数据已按 `Documents/Planning/D365配置数据清单.md` 核对：`ms_systemconfiguration` 配置项、自定义实体配置数据（评分卡/Coface 对照表/汇率/NACE 映射等）、自动编号配置、角色

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

---

## 预审计结论（2026-08-17，阶段 1.6 提前执行）

> 工具：`Code/Tools/_TempQuery`（已重写为纯只读，旧版备份 `Backups/TempTest/TempQuery_20260805_backup.cs`）；完整输出 `Backups/TempTest/audit_20260820_prerelease.txt`。
> 主清单 166 组件；McsPlugin 2869 / McsWebResource 1499 / McsCustomAPI 2883。

### ✅ 健康项
- 实体：我方前缀候选 41 个中真我方实体全部在主清单；❓项（fm* 资金模块、creditaccount/creditnote 系、credit_insurance_rate）与 8-4 审计一致=他人模块，无新增
- WebResource：我创建 19 个中 17 个业务资源全部 主清单✅+McsWebResource✅（含 8-4 picker HTML、8-13 mcs_fsm_detail_data.js）
- Custom API：5 个本体全部 主清单✅+McsCustomAPI✅（含 8-14 mcs_CalcContractRiskExposure）
- Plugin Step：我创建 67 个中 60 个 主清单✅+McsPlugin✅（含 8-15 FcaQuotaAppProcSyncPlugin×2、8-13 FsmDetailDataCompletedGuardPlugin×4）；缺口 7 个 = 5 个 Custom API implementation 绑定 Step（平台自动创建，预期假阳性）+ 1 个 fca_proc 编号 Step 幽灵行 dd60（见待决策#2）+ 1 个真缺口 CalculateRiskExposurePlugin Step 主清单漏加（见待决策#1）
- 退休 appaction 红线复核：apply/approve/reject 均 state=1 停用、均不在 entity_20260727_peter
- 停用 Step（TradeStPayTermSharePlugin、FsmDataStage4NotifyPlugin）均已登记上线核对清单 2.4.14/2.4.17/2.4.19
- 8-5 后新增 AppAction=0（无 Command Designer 副本漏网）；8-5 后新增 Custom API 32 个中我方仅 1 个且已入包，其余为 SYSTEM/他人团队
- mcs_customer_tag 在主清单✅

### ⚠️ 待决策/待处理项（发版前必须闭环）
| # | 事项 | 现状 | 处理 |
|---|------|------|------|
| 1 | `CalculateRiskExposurePlugin: Create of mcs_credit_scoringcard` Step（#1641，`30285291-b797-f111-b8dc-6045bd1c0cde`）主清单漏加 | ~~McsPlugin ✅ 但主清单 ❌~~ | ✅ 2026-08-17 已补；**2026-08-20 定论：该 Step 为工具副产品幽灵组件（触发即拦截评分卡创建），DEV1 已删、主清单/包内组件行已随删除自动清除、UAT 已手动停用，详见上线核对清单 2.4.23** |
| 2 | fca_proc 编号 Step 同名双活：d37f（08-04 重建，两包✅）+ dd60（07-01 原始行，不在任何包） | 双 Step 均启用 | ✅ **2026-08-17 已按用户决策停用 dd60（只停用不删除）**，回读 state=1；d37f 保持启用。已登记上线核对清单 2.4.20 |
| 3 | 退休 appaction 主清单残留不一致（reject 仍在） | 对发版无实际影响 | ✅ **2026-08-17 已按用户决策将 reject（`124c5d83-...`）移出主清单**（组件本体保留环境，仍停用），回读实锤 |
| 4 | #1837/#1838 标签改动：①fca 系「厂端授信→安全交易基线」改名（字段 6 个 + 实体显示名 3 个）随 entity 包自动带（UAT 已验证带入）；②**2026-08-17 补改显示名（用户指示「都改掉」）**：5 个实体显示名（mcs_fca_quota→安全交易基线额度表、mcs_fca_proc→安全交易基线模型计算表、mcs_fca_records→安全交易基线额度动态调整管理台账表、mcs_fca_mdlversion→安全交易基线模型版本表、mcs_fca_mdlconfig→安全交易基线模型参数配置表，均含英文名+集合名）+ 6 个主键字段标签 + `mcs_fca_quota.mcs_usedsellerbalance`（厂端授信占用金额USD→安全交易基线占用金额USD/Secure Transaction Baseline Used Amount USD），全部 set-entity-label/set-field-label 双语写入+发布，回读 6 实体「厂端授信」标签残留=0；③`account.mcs_creditgrade`（客户等级→资信评级/Credit Rating）发布路径未定：account 为平台共享实体不入我方包 | account 标签 DEV1=资信评级、UAT=客户等级（未发） | ✅ 2026-08-17 用户指定载体包 entity_20260727_peter：`account.mcs_creditgrade` 单字段已加入（最小增量，同 7-16 先例，不带 account 其他子组件），回读实锤在包；用户自发 UAT 验证。|
| 5 | 5 个 Custom API implementation Step 均不在任何包 | 与 8-4 审计结论一致=预期假阳性（平台自动创建绑定 Step；前 4 个已多次成功发布验证无影响） | 维持不处理 |
| 6 | `mcs_feishu` WebResource 不在 McsWebResource | 非我方组件（createdby 非我，8-11 他人创建），飞书团队自管 | 仅提醒，不动 |
| 7 | 视图 112 个不在主清单 | 预期假阳性（随实体隐式分发，8-4 已定论） | 不处理 |
