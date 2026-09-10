# 816 授信池 — PRD 补充设计建议稿（我方侧）

> 目的：816 下一版 PRD 需要明确的字段清单/接口契约/幂等键/落点等，我方先给出完整设计建议，供张烽团队评审后直接并入 PRD。
> 前提（v2.2 会议定调）：复用 `mcs_fca_quota` + `mcs_fca_records`；区别字段一律新增不改旧；哑记账（金额对方算好传入，我方校验+幂等保存+更新余额）。
> 状态：2026-08-20 初稿，未评审。标注【建议】的为我方推荐方案，标注【待定】的需业务/对方确认。

---

## 一、`mcs_fca_records`（台账表）新增字段清单 —— 建议终版

816 数据模型 2.2 `credit_change_detail` → CRM 字段映射（旧字段全部不动，新旧记录靠新字段区分）：

| # | 816 字段 | CRM 新增字段（逻辑名） | 类型 | 必填 | 说明 |
|---|---|---|---|---|---|
| 1 | credit_type | `mcs_credit_type` | Picklist：1=FACTORY 厂端授信 / 2=SINOSURE 中信保授信 | 占用释放必填 | 区分两个池 |
| 2 | biz_type | `mcs_biz_type` | Picklist：1=OCCUPY 占用（含重占） / 2=RELEASE 释放 | 占用释放必填 | 不动旧 `mcs_adjust` |
| 3 | source_type | `mcs_source_type` | Picklist：1=DELIVERY 发货推出 / 2=CANCEL 发货取消 / 3=RETURN 退货 / 4=SETTLE 解款认款 / 5=REDCHARGE 解款红冲 / 9=INITIAL 期初导入 | 占用释放必填 | 9 为上线切换专用 |
| 4 | source_no | `mcs_source_no` | String(50) | 占用释放必填 | 来源单据号（交货单号/退货单号/结款单号/红冲单号） |
| 5 | related_source_id | `mcs_related_recordid` | Lookup→`mcs_fca_records`（自关联） | REDCHARGE 必填 | 红冲溯源，指向被红冲的 SETTLE 台账 |
| 6 | delivery_no | `mcs_delivery_no` | String(50) | DELIVERY/CANCEL/RETURN 必填 | 交货单号（订单号沿用旧 `mcs_orderid` Lookup + 文本回写） |
| 7 | amountCNY | `mcs_amount_cny` | Money(CNY) | 可选 | 金额恒正；USD 金额沿用旧 `mcs_adjustamt` |
| 8 | fx_rate | `mcs_fx_rate` | Decimal(18,6) | 可选 | CNY→USD 汇率快照 |
| 9 | order_amount_A | `mcs_order_amount_a` | Money(USD) | 【建议】必填 | 快照：订单金额 A |
| 10 | return_amount_R | `mcs_return_amount_r` | Money(USD) | 可选 | 快照：累计取消退货 R |
| 11 | settle_amount_S | `mcs_settle_amount_s` | Money(USD) | 【建议】必填 | 快照：累计结款 S（对账依赖 A/R/S 一致性） |
| 12 | biz_date | `mcs_biz_date` | DateTime（日期） | 【建议】必填 | 业务发生日期（过账日），默认当天 |
| 13 | —（幂等） | `mcs_idempotency_key` | String(100) | 系统生成 | 见 §三 |
| 14 | remark | `mcs_remark` | String(500) | 可选 | 备注（特批说明等） |
| 15 | operator | 不新增 | — | — | 用 D365 标准 `createdby`+调用方 App User 即可，无需自建 |

**旧字段兼容【建议】**：新接口写入时同步回写 `mcs_adjust`（OCCUPY→3、RELEASE→4）与 `mcs_asisbalance/adjustamt/tobebalance`（USD 口径），旧视图/旧逻辑零影响、新旧记录可同表展示；`mcs_proccess`（流程环节 1-11）对占用释放事件不再使用，留空。【待定：是否需要在 1-11 中扩展新环节值】

**唯一约束（Alternate Key）【建议】**：`mcs_credit_type + mcs_source_type + mcs_source_no + mcs_biz_type` 四列组合建 Alternate Key，平台级防重 + 插件查重双保险。

## 二、信保侧额度/余额落点 —— 【建议】扩展 `mcs_fca_quota`

两个候选，我方推荐方案 A：

| | 方案 A（推荐）：`mcs_fca_quota` 加信保字段 | 方案 B：另建 `mcs_sinosure_quota` 表 |
|---|---|---|
| 模型 | 客户 1:1 一行 = 完整池（厂端+信保） | 客户 1:2 两行，查询要 join |
| 池总额/余额展示 | 单表单/单视图直出 | 需拼装 |
| 并发控制 | 单行锁天然覆盖双池 | 两表两锁，半更新风险 |
| 对现有影响 | 纯新增字段，旧插件不碰 | 零影响但更碎 |

方案 A 新增字段（授予层插件不写这些字段，信保字段只由占用释放接口/汇总服务维护）：

| 字段 | 类型 | 维护方 | 说明 |
|---|---|---|---|
| `mcs_sinosure_official_limit` | Money(USD) | 系统汇总（只读） | 中信保批复限额汇总（见 §六口径） |
| `mcs_sinosure_limit` | Money(USD) | 系统计算（只读） | 上浮后限额 = min(官方×系数, 封顶)，命中不上浮国家=官方原值 |
| `mcs_sinosure_usedbalance` | Money(USD) | 占用释放接口 | 信保净占用（ΣOCCUPY−ΣRELEASE） |
| `mcs_sinosure_balance` | Money(USD) | 占用释放接口 | 信保上浮余额 = 上浮限额 − 净占用 |
| `mcs_sinosure_ref_balance` | Money(USD) | 系统汇总（只读） | 中信保 T+1 动态余额（`mcs_quotabalance`），**仅参考+对账，不参与池计算** |
| `mcs_sinosure_quota_synctime` | DateTime | 系统 | 限额最后汇总时间 |

批复限额汇总时机【建议】：查询时实时汇总（`mcs_approvedquota` 数据量小，客户级过滤成本低）；后续量大再改每日定时刷冗余字段，接口契约不变。

## 三、占用/释放接口契约 —— 【建议】扩展 `mcs_AdjustFcaQuotaBalance`

唯一名不变：`mcs_AdjustFcaQuotaBalance`（未发 UAT、零调用，扩展无兼容包袱）。原有 6 入参 11 出参全部保留，新增参数全部可选（除标注外）。

### 入参（★=新增）

| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `mcs_accountid` | String | 是 | 客户 SAP 编码（现有） |
| `mcs_usebalance` | Decimal | 是 | 调整金额 USD，**恒正**（现有；哑记账，对方算好传入） |
| `mcs_proccess` | String | 授信池事件免填 | 流程环节 1-11（现有，授予层沿用） |
| `mcs_adjust` | String | 是 | 1=初始化 / 3=占用 / 4=释放（现有；重占用 3） |
| `mcs_contractid` / `mcs_orderid` | String | 条件 | 合同/订单编码（现有） |
| ★`mcs_credit_type` | String | 占用/释放必填 | `FACTORY` / `SINOSURE` |
| ★`mcs_source_type` | String | 占用/释放必填 | `DELIVERY`/`CANCEL`/`RETURN`/`SETTLE`/`REDCHARGE`/`INITIAL` |
| ★`mcs_source_no` | String | 占用/释放必填 | 来源单据号 |
| ★`mcs_delivery_no` | String | 条件必填 | 交货单号（DELIVERY/CANCEL/RETURN 时） |
| ★`mcs_related_source_no` | String | REDCHARGE 必填 | 被红冲的原结款单号（溯源） |
| ★`mcs_idempotency_key` | String | 可选 | 不传则系统按 `credit_type+source_type+source_no+adjust` 生成 |
| ★`mcs_amount_cny` | Decimal | 可选 | CNY 金额（恒正） |
| ★`mcs_fx_rate` | Decimal | 可选 | 汇率快照 |
| ★`mcs_order_amount_a` / `mcs_return_amount_r` / `mcs_settle_amount_s` | Decimal | A/S 建议必填 | A·R·S 快照（USD） |
| ★`mcs_biz_date` | DateTime | 可选 | 业务日期，默认当天 |

### 出参（★=新增）

现有 11 个全部保留（入参回显 + `mcs_usedbalance`/`mcs_sellerbalance`/`mcs_usedflag`/`mcs_recordid`/`mcs_failreason`），新增：

| 参数 | 类型 | 说明 |
|---|---|---|
| ★`mcs_credit_balance` | Decimal | 本次操作池（厂端或信保）调整后余额 |
| ★`mcs_idempotent_replay` | String | "1"=命中幂等重放，未重复记账，返回首次结果 |
| ★`mcs_sinosure_balance` | Decimal | 信保池最新余额（仅 credit_type=SINOSURE 时有值） |

### 行为规则（我方侧，写进 PRD 防止理解偏差）

1. **幂等**：按幂等键查台账，命中→直接返回首次结果 + `mcs_idempotent_replay=1`，不写台账不动余额；
2. **记账**：OCCUPY → 对应池 净占用+=金额、余额−=金额；RELEASE 反之；余额允许为负（释放可越过零）；
3. **发货占用校验（checkAndOccupy）**：`source_type=DELIVERY` 的占用，接口内部先判定授信类型（优先全额信保→全额厂端）并校验对应池余额，余额不足 → 返回失败不记账；
4. **事务**：台账写入+额度更新同事务，失败整体回滚（含补偿回滚）；
5. **校验下限**：客户必须存在；金额必须 >0；必填快照缺失→报错不入账；**不做** F=max(0,A−R−S) 计算、不做 A/R/S 取数、不做事件状态机（全在对方侧）；
6. **红冲溯源**：REDCHARGE 必须能通过 `mcs_related_source_no` 找到原 SETTLE 台账，否则报错。

## 四、池查询接口 `mcs_QueryCreditPool` 契约

| 项 | 内容 |
|---|---|
| 入参 | `mcs_accountid`（客户 SAP 编码） |
| 出参 | `status`/`message` + `factory_limit`（厂端额度）/`factory_used`（厂端净占用）/`factory_balance`（厂端余额） + `sinosure_official_limit`（批复限额）/`sinosure_limit`（上浮限额）/`sinosure_used`（信保净占用）/`sinosure_balance`（信保上浮余额） + `pool_total`（池总额）/`pool_balance`（池余额） + `sinosure_ref_balance`（中信保 T+1 余额，仅供参考） + `asof_time`（数据时点） |

## 五、明细查询接口 `mcs_QueryCreditChangeDetail` 契约（对账/报表用）

| 项 | 内容 |
|---|---|
| 入参 | `mcs_accountid`（可选）、`mcs_credit_type`（可选）、`mcs_source_no`/`mcs_order_no`（可选）、`mcs_date_from`/`mcs_date_to`（可选）、`mcs_page`/`mcs_page_size` |
| 出参 | `status`/`message` + `records`（台账裸数组，含 §一全部新旧字段）+ `total_count` |

## 六、中信保批复限额汇总口径 —— 需业务确认，我方建议

1. **取值**：`mcs_approvedquota`，过滤 `mcs_quotastate=1`（有效）+ 当天在生效/失效区间内；
2. **汇总维度【建议】**：**全申请类型、全支付方式求和**为"官方限额"——816 池是客户维度总池，不按 LC/OA 拆分；【待定：业务是否要排除"自掌额度"（applygenre=6）等特定类型】——我方实现上过滤维度配置化，口径调整零代码；
3. **`*/*` 通用买方代码【建议】**：不归属任何客户，不计入任何池【待定：业务确认】；
4. **买方代码→客户**：`mcs_approvedquota.mcs_buyerno` → `mcs_sinosure_buyercode` → 客户 Lookup；
5. **不取值**：老 `mcs_sinosure_*` 系列（2025 存量）、`mcs_sinosure_quotaapproveinfo`（过程占位表）。

## 七、上浮规则配置（已定调：以 816 最新版为准，全参数化）

- 配置载体：`ms_systemconfiguration` JSON（CofaceCountryConfig 先例）；
- 参数：上浮系数（当前 1.5）、封顶（当前 USD 8M）、不上浮国家清单【待定：816 提供清单】、批复限额过滤维度；
- 对方出运投保 PRD 的 ×1.8/12M/21 国规则归对方投保环节，与池校验不拉通。

## 八、A4 校验 / A5 判定 —— 不设独立接口，均为余额调整接口内部逻辑（2026-08-20 用户明确）

816 IT 角度表发货事件动作本来就是 checkAndOccupy（校验+占用一体），因此 A4/A5 不单独暴露接口：

- **A5 类型判定**：发货占用调用时，`mcs_credit_type` 入参可不传，接口内部按「信保上浮余额 ≥ 金额 → SINOSURE；否则厂端余额 ≥ 金额 → FACTORY；都不够 → 失败」判定，判定结果随出参 `mcs_credit_type` 返回，供上游反写订单；
- **A4 发货前校验**：`source_type=DELIVERY` 的占用，接口内部先判定类型并校验对应池余额，余额不足 → 返回 `mcs_usedflag=0` + 失败原因，不记账；
- **待定**：预警线（三档中的 WARN）与特批 BPP 流程的职责与触发方式，待 PRD 明确。

## 九、风险敞口口径 —— 供拍板（附我方建议）

两套公式对照与四个分歧点见《评估v2_实施方案.md》§6。我方建议讨论路径：
1. 先确认"合同评审时看的敞口"（1641）与"客户监控看的敞口"（816）是否同一个数；
2. 是同一个数 → 1641 按 816 改公式（破坏性变更，已上 UAT，需提前通知合同模块）；不是 → 写清各自定义与使用场景，避免混用；
3. 816 公式"交易风险敞口=客户签约占用+客户风险敞口（=厂端净占用）+信保净占用+合同风险赊销−客户授信池余额"展开后**净占用出现两次**，疑似笔误，请张烽确认本意；
4. 中信保基数建议统一为"批复限额×上浮−CRM 自记净占用"（实时），中信保 T+1 动态余额只作对账参考，避免双口径重复扣减。

## 十、期初数据导入模板（上线切换用）

中信保"签约未发货"存量占用，Excel 列：`客户SAP编码 | 买方代码 | 来源单据号(合同/订单号) | 金额USD | 金额CNY | 汇率 | 业务日期`；导入固定 `credit_type=SINOSURE`、`biz_type=OCCUPY`、`source_type=INITIAL`；厂端无期初（新业务）。导入后核对：Σ期初明细 = 业务确认的在途占用清单总额。

---

## 附：给对方侧的约定（写进 PRD 防扯皮）

1. 金额一律**恒正**，方向由 `biz_type` 表达；
2. 同一单据同一事件的重复推送是常态（重试），必须接受幂等返回而非报错；
3. `A/R/S` 快照以对方订单主数据为准，我方只做一致性对账、不重算；
4. 关键假设（订单:交货单=1:1、无部分取消、无减量变更）由上游系统保障，我方台账不做反向校验；
5. 占用一经入账不可改类型（830 版本排除池流转）。
