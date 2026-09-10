# 发版检查清单（2026-09-10 晚 bugfix 修复发布）

> 依据 `发版检查清单.md` 模板建立。发版公告（刘泞 09-10）：今晚 22:00 封板开始导出，23:00 发布。
> **⚠️ 09-10 上午计划变更：不是正式全量发布，是 bugfix 修复发布**——实体包=IT 在 PRE 环境新建的 `entity_bugfix_0910`（非托管）；Plugin 走补丁模式（Bugfix-20260903 发版分支编译 DLL 更新 PRE/生产）；JS 直接改 PRE 环境（用户明确）；Azure 管道不动。
> 上次生产发版点 = 2026-09-02（发版包=entity_20260902/role_20260902，本清单以其为对比基准；0903 晚曾有补丁 DLL）。

## 阶段 0：发版范围确认

- [x] **0.1** 本次发版内容（增量自 0902 生产发版，看板待发布 34 项为准，rowid 140~185）：
  - **实体/元数据**：mcs_fsm_data 合同多选 2 新字段+主窗体（#2150/#2169）、Active 融资管理视图（#2169）、Sitemap 菜单改名（#2172）、贴息/融资费用双语改名+主窗体布局、mcs_fsm_detail_data.mcs_orderid+主窗体、mcs_fsm_resource 所在城市 Lookup 化（新 city_text 字段+必填性调整+主窗体）、mcs_customer_tag 定量指标 2 字段下限放宽、mcs_trade_ptgrouptype 默认视图加 2 列
  - **角色**：Financing Resource Manager（#1433 配套，role_20260902 已含，rowid 144）
  - **Plugin（McsPlugin，随 DEV1 主 Assembly）**：Coface 解析器系列（负值指标 166/行业风险 172/注册资本 175/诉讼债权+无配置国兜底 176/量纲×100 177/公式型国家兜底+二次修正 178）、#2147 聚合口径、#2189 厂端客户分类聚合（185）、BPP 审批人快照（180，Step 过滤+PreImage 已配）、新老客户/算分/SalesAmount（0903 后已合 uat）
  - **WebResource（McsWebResource）**：mcs_credit_record.js（国家编码 Lookup 取码 153）、mcs_fsm_data.js（#2150/#2176，157）、mcs_fsm_detail_data.js（158）、mcs_fsm_contract_multiselect.html 新增（169）、语言包 FsmContractPicker_* 4 key（170）、mcs_fsm_resource_multiselect.html（179）、mcs_fsm_resource.js（#2072 保存修复，183）
  - **Azure**：ExtensionApi（授信池三变更 154、风险敞口 #2150 159、成交条件查询客户编码 165）、ClientApi（授信池合同授信金额 160）
  - **配置数据**：162/163/164/173/181/182/184 五环境均已执行完毕（不随包）
- [x] **0.2/0.3** 发布顺序矩阵（按刘泞公告 14 环节）：1 `McsOptionSet` ✅发（无我方变更，随队）/ 2 `McsWebResource` ✅发（7 项我方）/ 3 role 包（待 IT 建）✅发（Financing Resource Manager 已在 DEV1 role_20260902 内实锤，新包建后需确认带入）/ 4 **entity 包（待 IT 建）**✅发（**缺口清单见阶段 1.3，封板前必须补齐**）/ 5 `McsCustomAPI` ✅发（无新 API，Assembly 随包刷新）/ 6 `McsPlugin` ✅发 / 7 `McsAutomate` ⏭️无我方资产 / 8 `app_allcomponents` ⏭️非我方维护 / 9 `sln_Import` ⏭️不在公告清单 / 10 `CommonMessageHandle` ⏭️无变更 / 11 `MessageHandler` ✅发（SanyD365.Main BPP Handler DataCenter 路由 1a7f1bd6af6）/ 12 `InnerApi` ⏭️无变更 / 13 `ExtensionApi` ✅发（154/159/165）/ 14 `ClientApi` ✅发（160，与 MessageHandler 版本一致）
- [x] **0.4** 无「删除/重建字段」「删除组件」类变更（delta_scan 0901 dump vs 0910 dump 全量 diff 实证：➖已删字段=0、🚨类型变更=0；详见 Backups/TempTest/deltascan_20260910/）

## 阶段 1：主清单与组件分布核对

- [x] **1.1** 主清单完整性：createdby 反向审计（09-10 已跑）——09-04 后新增组件零漏网（145 项不在主清单均为历史预期假阳性：视图/CustomAPI 实现 Step/测试语言包/退休 appaction/dd60 幽灵行）
- [x] **1.2** 无环境删除组件需移除
- [x] **1.3** 分布核对（09-10 已跑）：`check-solution-coverage AllComponent_Peter_NoUAT entity_20260902`（上次发版包作基准）✅186 / ❌31——其中 28 项为增量预期项（零变更实体/App Action/命令定义/BPF，同 0903 口径）；**真实缺口=上次发版后有改动但未发走的组件**，加上 delta_scan 0901→0910 实锤变更与 0903/0906/0907 增量包比对，**需加入今晚新 entity 包的组件清单**：
  - **mcs_fsm_detail_data（4 个，0902 后登记但未发走，UAT 实锤无 mcs_orderid）**：实体壳 c0e26537 / mcs_orderid 8507f058 / 关系 8e8ff71f / 主窗体 Information 46355123
  - **mcs_fsm_data（9 个）**：实体 64c5bff3 / mcs_contract_ids 0471fbbc / mcs_contract_nos 5e99edca / mcs_fsm_fee af2afbfb / mcs_fsm_fee_base 3c34b954 / mcs_fsm_interest_discount 7b2afbfb / mcs_fsm_interest_discount_base 6e4991a0 / 视图 Active 融资管理 dd8cbd8f / 主窗体 Information 97420f52
  - **mcs_fsm_resource 0903 批次（4 个）**：mcs_fsm_institution_city b2348c3f（必填性变更）/ mcs_fsm_institution_city_text 130d02da（新字段）/ 关系 mcs_city_mcs_fsm_resource 969572be / 主窗体 Information 4c099276
  - **mcs_customer_tag（2 个）**：mcs_itemintvalue1 80f004c9 / mcs_itemintvalue2 326b98cf（字段下限 0→-999999999）
  - **mcs_trade_ptgrouptype（1 个）**：视图 成交条件产品分类关系列表 dce2e02b
  - **entity_20260906_peter 批次（64 个：8 实体壳+56 系统视图多语言标签）**：UAT 已有、生产无——✅ **2026-09-10 用户拍板：生产没有的都要带上，64 个组件全部并入今晚新包**
  - **Sitemap「Sany CRM Sales」5654d2d6**（#2172 菜单改名 09-08 才改，上次发版包里的 Sitemap 是旧文案，今晚新包必须带）
  - 基准包 entity_20260902 里已含但**未发走**的 mcs_fsm_detail_data 4 组件也要带入新包（见上）
  - 上次已发走无需重复：mcs_fsm_resource 壳+mcs_fsm_institution_code（0902 已随包到生产）

> IT 建好新 entity 包后：重跑 `check-solution-coverage AllComponent_Peter_NoUAT entity_<今晚>` 复核全绿
- [ ] **1.4** check-release 等效覆盖：1.3 + 逐组件回读（同 0903 口径），待 1.3 缺口补齐后重跑 coverage 复核
- [x] **1.5** 依赖检查 `check-solution-deps`（8 包，09-10 已跑）：涉我方模块 ❌=0（3574 项「必须处理」全部为他队组件/历史噪音，同 0903 口径）；picker HTML 两文件在 McsWebResource ✅（表单依赖闭环）
- [x] **1.6** createdby 反向审计 ✅（见 1.1）

## 阶段 2：字段变更风险核对（防 80041A06）

- [x] **2.1** 字段类型对比：delta_scan 0901→0910 dump 全量 diff（24 实体）：🚨 类型变更=0、➖ 删除=0；新增=mcs_contract_ids(Memo)/mcs_contract_nos(String)/mcs_orderid(Lookup)/mcs_fsm_institution_city_text(String) 均纯新增零冲突；范围变更=mcs_itemintvalue1/2 下限放宽、city 必填性 Required→None、city_text Required（UAT 侧同名字段 ID 不同但导入按逻辑名匹配，无冲突）
- [x] **2.2** 无删除字段/同名重建（diff 实证）
- [x] **2.3** ➖ 段为空
- [x] **2.5** ✅ 生产直连字段终核（Frank 账号，2026-09-10 用户批准后执行，4 实体 5 次轻量只读）：① `mcs_fsm_data` 生产无 contract_ids/contract_nos→纯新建零冲突；② `mcs_fsm_detail_data.mcs_orderid` 生产已存在但 **Lookup→mcs_order 与 DEV1 同型同目标**（MetadataId 不同不影响，导入按逻辑名匹配）；③ `mcs_fsm_resource` city/city_text 生产已在位且类型一致（city 生产 Required 已是 None）；④ `mcs_customer_tag` 2 字段生产均为 Decimal 一致（仅范围值变更非类型）。**生产侧字段冲突风险实证为零**
- [ ] **2.4** Plugin 字段依赖核对：N/A（无字段删除）

## 阶段 3：Plugin / Custom API 核对

- [x] **3.1** 临时 Assembly 零残留（09-10 query-plugin-namespace SanyD365.Plugins = 未找到）✅
- [x] **3.2** 本批零删类
- [x] **3.3** Custom API 实现在 ExtensionApi.Sales（McsCustomAPI 内闭环），本批无新 Custom API
- [x] **3.4** DEV1 Assembly 时效：Extension.Sales modifiedon=09-09 08:10 北京（含 #2189，PR 9272 合并后 3 分钟更新）✅ 我方代码全在；ExtensionApi.Sales modifiedon=09-09 22:10 北京（含当日 20:14 最后一次提交）✅。⚠️ **他队 PR 9277（解款单 N+1）/9280（案例推送）等 09-09 上午后合入 uat 的提交不在 DEV1 Extension.Sales**——封板前是否用最新 uat 重编共享 Assembly 需与刘泞确认（他队责任/封板流程）
- [x] **3.5** 审批人快照 Step（f17f2752）在 McsPlugin ✅、过滤=mcs_bppstatuscode,mcs_nextapprover ✅、PreImage mcs_approve_type 已配 ✅（PreImage 随 Step 走包已实证：FcaProcActivationPlugin PreImage UAT 同 GUID 在场）

## 阶段 4：多语言与配置数据核对

- [x] **4.1** 语言包：FsmContractPicker_* 4 key 仓库 uat 1033/2052 均在（实锤）+ DEV1 ms_languagefile_2052 实读含 4 key（总 6471 keys）✅；两 JS 分支合并后 DEV1 按仓库 uat 版重部署
- [x] **4.2** 新字段双语标签建字段时已写入（delta_scan 标签列实证 1033/2052 均在）
- [x] **4.3** 配置数据：0907 评分卡（316 条五环境）、额度参数 48 行五环境、枚举映射（行业风险/客户评级/经销商分级/国别风险 #2190）、哨兵边界修复——均五环境已执行完毕（看板 162/163/164/173/181/182/184），本次无新增配置数据

## 阶段 5：UAT 发布与验证（发版日执行）

- [ ] **5.1** 按顺序矩阵 n8n 发布（用户操作）；模拟导入 UAT 已全绿（6692 条依赖边闭合，🚨0 ⚠️0，09-10 上午跑）
- [ ] **5.2** 发布后查导入历史 `list-failed-imports 50 20260910` 全部 progress=100
- [ ] **5.3** UAT 功能验证（重点：融资合同多选 picker、审批人快照、#2189 厂端分类、Coface 解析器量纲/行业风险）
- [ ] **5.4** 发版包原样归档 `Backups/Solutions/Releases/`（entity_20260902 等 8 包导出或字段快照）
- [ ] **5.5** Ribbon 按钮核对：本批无 RibbonDiffXml 变更，跳过

## 阶段 6：生产发布交接

- [ ] **6.1** 交接材料：包清单+顺序矩阵（0.3）+ 配置数据已就绪说明（4.3）+ 手动核对项（下）
- [ ] **6.2** 生产手动核对项提醒（《上线核对清单》2.4）：① 2.4.14 停用「融资落实提醒」Step；② 2.4.17 停用 TradeStPayTermSharePlugin Step；③ 2.4.19 3 个退休批量按钮若被包复活需停用；④ 2.4.25 融资管理 2 个旧 App Action 已在 prod 停用、prod-eu/prod-na 上线时核对；⑤ 2.4.16 按钮显隐不生效则 Regenerate ribbon metadata；⑥ 2.4.4 通知清站点数据；⑦ 角色用户分配手动（2.4.5）
- [ ] **6.5** 生产发布后直连实证（Frank 账号逐步请示）：Assembly ModifiedOn 刷新 + 新字段/视图在场抽查 + 冒烟

## 阶段 7：发版后收尾

- [ ] **7.1** 包/快照归档 + README 更新
- [ ] **7.2** Memory.md 更新发版记录
- [ ] **7.3** Code/INDEX.md 同步
- [ ] **7.4** 本清单存档
- [ ] **7.5** 看板 34 项按包归档（POST /api/release-items/release）

---

## 封板前待办（⚠️ 阻塞项）

1. **今晚实体包 = PRE 环境 `entity_bugfix_0910`（IT 已建）**：用户拍板范围=bug 修复+已验功能都带（1.3 清单 21 个组件；0906 批次 64 个不带）——⚠️ 关键风险：PRE 是 09-02 左右的 DEV 副本，09-02 后创建的元数据（合同多选 2 字段/city_text/orderid 等）可能不存在于 PRE，需逐一核对后决定在 PRE 补建还是改从 DEV1 导出；待 PRE 登录后执行核对
2. ~~两个 JS 分支合并 uat~~ ✅ 2026-09-10 用户已合并（PR 9371 MRU / PR 9372 picker 滚动）；AI 已拉仓库 uat 版部署 DEV1 三个 WebResource（回读 MD5 逐字节一致 ✅）。**JS 今晚走 PRE 直改**（用户明确，同 0903 account_profile_tab 先例）：待 PRE 登录后将仓库 uat 版 6 个 WebResource（mcs_credit_record.js/mcs_fsm_data.js/mcs_fsm_detail_data.js/mcs_fsm_contract_multiselect.html/mcs_fsm_resource_multiselect.html/mcs_fsm_resource.js）部署 PRE + 语言包追加 FsmContractPicker 4 key
3. **DLL 补丁模式（用户拍板）**：① `bugfix-20260903-peter-backend-batch`（7 提交）PR 待用户创建→Bugfix-20260903；② `bugfix-20260910-peter-2189-approver` ✅ 已建并推送（cherry-pick 审批人快照 83162f2676a→59185b31118；#2189 两提交实锤发版分支已含、空 pick 跳过；FinancingManagement 目录与 uat 零 diff；Release 编译通过）——PR 待用户创建→Bugfix-20260903；③ 两 PR 合并后 tx-windows 拉 Bugfix-20260903 重编 DLL→三项校验→更新 PRE→生产
4. **审批人快照 Step 配置同步改（用户拍板今晚带上）**：PRE/生产的 FsmDataBppCallbackPlugin Step（Update of mcs_fsm_data）需手工改：过滤属性加 `mcs_nextapprover` + 新增 PreImage（mcs_approve_type）——DLL 到生产前必须先改，否则回调必抛异常；DEV1 已配好可参照（Step f17f2752 / PreImage 0dfe51d3）
5. ~~DEV1 共享 Assembly 时效~~ ✅ 用户明确：他队代码不管
6. **角色**：Financing Resource Manager（#1433 配套）若今晚在范围，需随角色包或手动；bugfix 范围待用户确认是否含角色
