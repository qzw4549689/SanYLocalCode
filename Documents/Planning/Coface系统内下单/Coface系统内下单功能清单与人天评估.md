# Coface 系统内下单 功能清单与人天评估

> **版本**：V1.0
> **日期**：2026-07-31
> **依据**：`Coface系统内下单实施方案.md`（V2.0，2026-07-24）
> **说明**：本清单将实施方案第 8/9/11/12 章拆解为可执行功能项，逐项给出人天评估。
> **人天口径**：1 名熟悉本项目的开发 + AI 辅助；「乐观」= 一切顺利无返工；「基准」= 含正常调试/返工/联调波动。**不含** C1~C5 / B1~B7 等待确认的自然时间，也**不含** Report 就绪 6-7 个工作日的等待（人天 ≠ 日历天）。

---

## 一、后端：CofaceApiService 扩展

| # | 功能项 | 内容说明 | 复用/依赖 | 乐观 | 基准 |
|---|---|---|---|---|---|
| 1 | Token 缓存化改造 | `CofaceTokenManager` 改为缓存 Token（60 分钟有效期），复用 `ms_systemconfiguration.coface_idtoken / coface_token_expiry` 字段，避免单次点击内重复认证 | 现有字段可直接用 | 0.25 | 0.25 |
| 2 | GetInstantReport | GET /companies/reports 即时报告检查（免费复用优先级最高） | 现有 GET 封装模式 | 0.25 | 0.5 |
| 3 | PlaceIdentificationOrder | POST /companies/identifications 下调查单（公司未识别时） | Coface 接口文档；C1 待确认 | 0.25 | 0.5 |
| 4 | GetIdentificationOrders | GET /companies/identifications 查识别结果（identified / initiated / negative 三分支解析） | C1 返回结构待确认 | 0.25 | 0.5 |
| 5 | PlaceUrbaMonitoringOrder | POST /urba360/monitorings/orders 下 URBA 监控单；德国公司附带 `legitimateInterest` | 配置化 LegitimateInterest | 0.25 | 0.5 |
| 6 | PlaceReportOrder | POST /publications/orders 下 Report 单；产品按 `CofaceCountryConfig` 选择；双格式 39 国 JSON/PDF 两单、间隔 5 秒 | 现有产品选择配置；C3 待确认 | 0.5 | 0.75 |
| | **小计** | | | **1.75** | **3.0** |

## 二、Custom API 与 Plugin

| # | 功能项 | 内容说明 | 复用/依赖 | 乐观 | 基准 |
|---|---|---|---|---|---|
| 7 | Custom API 定义与注册 | 新增 `mcs_CofacePlaceOrder`（入参 CreditRecordId，出参 status/message/orderStatus），参照 `mcs_CofaceSearchCompany` 形态，加入 `McsCustomAPI` Solution | 成熟样板照搬 | 0.25 | 0.5 |
| 8 | CofacePlaceOrderPlugin 决策树状态机 | 6 状态分支推进（未下单/调查单已提交/URBA 待就绪/Report 待就绪/已就绪/下单失败），每次点击只做一件事，天然满足 5 秒限流 | 归入 `SanyD365.D365ExtensionApi.Sales` | 1.0 | 1.5 |
| 9 | 订单复用与防重复扣费 | 每阶段先查已有订单再下单（URBA 单/Report 单/即时报告三级复用）；重复点击/并发点击幂等；复用 `ExtractReportOrderInfo` 逻辑 | 现有复用逻辑 | 0.25 | 0.5 |
| 10 | 字段写回与异常边界 | 每步写回「下单状态/下单信息/下单时间」三字段；POST 失败不静默成功；超 N 天未就绪追加超时警告 | 3 个新字段（见 #11） | 0.25 | 0.5 |
| | **小计** | | | **1.75** | **3.0** |

## 三、元数据与配置

| # | 功能项 | 内容说明 | 复用/依赖 | 乐观 | 基准 |
|---|---|---|---|---|---|
| 11 | mcs_credit_record 新增 3 字段 | `mcs_cofaceorderstatus`（选项集 6 值）/ `mcs_cofaceordermsg`（文本 500）/ `mcs_cofaceorderdate`（日期时间），中英双语标签 + 实体发布 | MetadataTool 公共方法（红线：禁止裸 SDK） | 0.25 | 0.5 |
| 12 | CofaceCountryConfig 配置扩展 | JSON 增加 `DualFormatCountries`（双格式 39 国，范围待 C3 确认）+ 可选 `LegitimateInterest`（德国） | 现有配置结构 | 0.25 | 0.25 |
| 13 | 组件加入主清单 | 3 字段 / Custom API 本体+参数+响应 / App Action / WebResource 变更加入 `AllComponent_Peter_NoUAT` | `MetadataTool add-solution-component`（幂等） | 0.25 | 0.25 |
| | **小计** | | | **0.75** | **1.0** |

## 四、前端

| # | 功能项 | 内容说明 | 复用/依赖 | 乐观 | 基准 |
|---|---|---|---|---|---|
| 14 | placeCofaceOrder JS | 点击 → retrieveRecord 查状态 → 调 Custom API → 提示结果并刷新表单 | 照搬【搜索 Coface 企业】链路 | 0.25 | 0.5 |
| 15 | nextStep 就绪校验 | 「关联客户代码 → 内外部数据集成」流转前校验 `mcs_cofaceorderstatus ≠ 4` 时先自动推进一次状态查询，再按结果放行/阻断提示（按 PRD 硬阻断，B1 待业务确认） | 现有 nextStep 钩子 | 0.25 | 0.5 |
| 16 | App Action【Coface 下单】按钮 | DeployTool AppActionDeployer 扩展部署；显隐逻辑同【搜索 Coface 企业】模式（关联客户代码阶段 + 已绑定 Coface ID） | 现有按钮部署模式 | 0.5 | 0.5 |
| 17 | 中英语言包 | 按钮/提示语中英 key：先本地 1033/2052.json **追加**（红线：禁止覆盖），再同步 DEV | 现有语言包机制 | 0.25 | 0.25 |
| | **小计** | | | **1.25** | **1.75** |

## 五、测试（DEV1 + Coface 沙盒）

| # | 功能项 | 内容说明 | 复用/依赖 | 乐观 | 基准 |
|---|---|---|---|---|---|
| 18 | Mock 测试 | 下单/查询接口 Mock 用例（复用 `Tests/CofaceMockTests` 模式） | 现有 Mock 测试工程 | 0.5 | 0.75 |
| 19 | DEV1 独立 Assembly 部署 | 临时独立 Assembly 注册（红线：测试结束立即注销） | `/skill:d365-deploy` 流程 | 0.25 | 0.25 |
| 20 | 10 场景功能验证 | 实施方案第 11 章场景 1~10：首次下单/重复点击/URBA 未就绪/就绪补单/即时报告复用/已有订单复用/未识别公司调查单/阻断/取数不变/限流 | **依赖沙盒就绪速度**（URBA/Report 就绪时长不可控） | 1.0 | 2.0 |
| | **小计** | | | **1.75** | **3.0** |

## 六、发布

| # | 功能项 | 内容说明 | 复用/依赖 | 乐观 | 基准 |
|---|---|---|---|---|---|
| 21 | 归并主项目 | 注销临时 Assembly → 代码归并远程 `SanyD365.D365ExtensionApi.Sales` → PR → 合并 uat → 重编 → 更新 DEV1 主 Assembly → 重绑 Custom API → 回归 | tx-windows 远程服务器操作（红线：本地禁止推送） | 0.5 | 1.0 |
| 22 | 发版核对 | `check-solution-coverage` 核对 + 字段快照对比（防 80041A06） | 现有发版检查清单 | 0.25 | 0.25 |
| 23 | n8n 发布 | `entity_mcs_credit_record`（字段）+ `McsCustomAPI` + `McsWebResource`（JS + 语言包） | 跟随发版窗口 | 0.25 | 0.25 |
| | **小计** | | | **1.0** | **1.5** |

## 七、UAT 与收尾

| # | 功能项 | 内容说明 | 复用/依赖 | 乐观 | 基准 |
|---|---|---|---|---|---|
| 24 | UAT 联调 | 沙盒/生产环境实测下单全链路 | **依赖 C1~C5 书面确认** | 0.5 | 1.0 |
| 25 | 文档收尾 | 更新 Memory.md / 测试执行记录 / 上线核对清单同步 | — | 0.25 | 0.25 |
| | **小计** | | | **0.75** | **1.25** |

---

## 八、汇总

| 阶段 | 乐观 | 基准 |
|---|---|---|
| 一、后端 ApiService | 1.75 | 3.0 |
| 二、Custom API + Plugin | 1.75 | 3.0 |
| 三、元数据与配置 | 0.75 | 1.0 |
| 四、前端 | 1.25 | 1.75 |
| **纯开发小计（一~四）** | **5.5** | **8.75** |
| 五、测试 | 1.75 | 3.0 |
| 六、发布 | 1.0 | 1.5 |
| 七、UAT 与收尾 | 0.75 | 1.25 |
| **合计** | **9.0** | **14.5** |

## 九、与实施方案第 14 章粗估的差异说明

| 口径 | 本地开发 | 测试 | 发布 | UAT | 合计 |
|---|---|---|---|---|---|
| 方案第 14 章（粗估） | 2 ~ 2.5 | 1 ~ 2 | 跟随发版窗口 | 0.5 ~ 1 | 3.5 ~ 5.5 |
| 本清单（详细拆解） | 5.5 ~ 8.75 | 1.75 ~ 3.0 | 1.0 ~ 1.5 | 0.75 ~ 1.25 | 9.0 ~ 14.5 |

**差异原因**：
1. 粗估未计入 Mock 测试、中英双语标签/语言包、临时 Assembly 注册/注销/重绑、归并回归等隐性工作；
2. 粗估按"链路全复用、一次通过"最乐观口径，未留调试返工余量；
3. 决策树状态机（#8）是本次核心复杂点，6 状态分支 + 幂等防重需要充分自测。

**建议**：对内排期按**基准口径 14.5 人天**；若 C1~C5 确认顺利且复用链路无坑，可压缩至 10 人天左右。

## 十、前置依赖与风险提示

| # | 事项 | 影响 |
|---|---|---|
| 1 | C1~C5 待 Coface 书面确认（重点 C2/C3） | 阻塞 #3/#4/#6/#12 细节与 UAT 联调 |
| 2 | B1~B7 待业务确认（重点 B1 硬阻断/B2 下单权限） | 阻塞 #15 阻断口径与按钮权限显隐 |
| 3 | 沙盒 URBA/Report 就绪时长不可控（Report 约 6-7 个工作日） | 测试场景 3/4/9 可能被拉长，人天不变但日历周期变长 |
| 4 | PRD 变更确认（按钮手动下单属需求变更） | 需业务书面确认后方可开发 |

---

*本清单随 C1~C5 / B1~B7 确认结论滚动更新。*
