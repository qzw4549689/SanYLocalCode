# D365 客户信用评估系统 — UAT 发布状态追踪表

> **用途**：跟踪在 DEV 环境已完成开发、但尚未发布到 UAT 的功能清单
> **最后更新**：2026-06-15
> **依据**：[`d365-deploy` skill](../../.agents/skills/d365-deploy/SKILL.md) 中 DEV → UAT 发布流程

---

## 发布流程说明

从 DEV 到 UAT 的完整发布环节：

```
DEV 开发完成
    ↓
DEV 测试通过
    ↓
同步远程服务器（tx-windows，改写命名空间/引用/csproj）
    ↓
远程服务器编译通过（D365.sln / Service.sln）
    ↓
推送代码（Git 提交 → PR → 合并到 uat 分支）
    ↓
DEV Assembly 更新（用已合并 uat 编译的 DLL 更新 DEV Plugin）
    ↓
DEV 最终验证
    ↓
UAT 实体发布（涉及新增实体/字段时，用户通过 n8n 手动先发布元数据）
    ↓
UAT Plugin 发布（用户通过 n8n 手动发布 Plugin）
```

**图例**：
- ✅ 已完成
- 🔄 进行中 / 待确认
- ⏸️ 未开始
- ❌ 失败 / 阻塞
- N/A 不涉及

---

## 功能清单

| 功能编号 | 功能名称 | 涉及组件 | DEV 开发 | DEV 测试 | 同步远程 | 远程编译 | 推送代码 | DEV Assembly 更新 | DEV 最终验证 | UAT 实体发布 | UAT Plugin 发布 | 阻塞点/备注 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| F6.x | Coface API 配置化（从 `ms_systemconfiguration` 读取 `CofaceApiConfig`） | Plugin：`CofaceIntegrationDataSyncPlugin`、`CofaceApiService`、`CofaceTokenManager` | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ⏸️ | N/A | ⏸️ | DEV Assembly 更新失败：DEV 当前 Assembly 包含 `LeadMainPostUpdate2IMCS`，最新 `uat` 已删除该 Type，导致 DLL 无法更新 |
| F6.8 | 多币种汇率转换配置化（16 种货币 → USD） | Plugin：`Urba360Parser`、`FullReportParser`、`CofaceExchangeRateHelper`<br>实体：`mcs_coface_exchange_rate` | ✅ | ✅ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ✅ | ⏸️ | 本地 mock 测试已通过（EUR/CNY/PLN/USD/未配置货币）；DEV 临时 Assembly 已注销；DEV 实体已发布且汇率数据已导入；Plugin 代码待同步远程/推送；待 DEV Assembly 更新后做最终集成验证 |
| F6.9 | NACE 行业映射配置化 | Plugin：`Urba360Parser`、`CofaceNaceMappingHelper`<br>实体：`mcs_coface_nace_mapping` | ✅ | ✅ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ✅ | ⏸️ | 本地 mock 测试已通过（含 Division 23 精确匹配优先）；DEV 实体已创建并导入 10 条映射数据；DEV 临时 Assembly 已注销；评分卡配置中未包含 `Sectors` 项目，未生成 Sectors 标签；待评分卡补充 Sectors 后做最终集成验证 |
| F6.11 | 定性指标值映射配置化（复用 `mcs_credititem_value`） | Plugin：`CofaceDataSyncPlugin`、`CofaceQualitativeMappingHelper`<br>数据：`mcs_credititem_value` | ✅ | ✅ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ✅ | ⏸️ | DEV 数据已修复（CountryRisk 改为 A1-A4/B-E、SectorRisk 补充 4、ExternalRating 新增 0-10、Sectors 改为中文）；DEV 临时 Assembly 测试通过（ExternalRating=3→高风险、CountryRisk=A3→中风险、SectorRisk=2→中风险）；临时 Assembly 已注销 |
| F6.10 | Coface 字段→D365 评分项目映射表 | Plugin：`CofaceDataSyncPlugin`、`CofaceFieldMappingHelper` | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | 用户决定暂缓：当前硬编码字典相对稳定，配置化价值以规范化和未来扩展为主，待后续需要时再做 |
| F6.12 | 客户主数据外部评级回写（mcs_externalrate） | Plugin：`CofaceDataSyncPlugin.UpdateAccountExternalRate` | ✅ | ✅ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | N/A | ⏸️ | DEV1 验证通过（PL 记录回写为 "3"）；Coface 代码仍在本地独立 Assembly，待用户通知后合并到远程主项目并部署 UAT |
| F4.5.3 | BPP 审批通过后回写 Account 信用分/等级/有效状态 | Plugin：`CreditRecordBppCallbackPlugin.UpdateAccountCreditInfo` | ❌ | ❌ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | N/A | ⏸️ | DEV1 测试失败：`mcs_creditgrade` 为 Picklist，代码按字符串赋值导致 `Incorrect attribute value type System.String`。本地独立 Assembly 已修复，待与其他代码一并推送 UAT / 更新 DEV 主 Assembly 后复测 |

---

## 阻塞点汇总

| 优先级 | 阻塞项 | 影响范围 | 下一步行动 |
|---|---|---|---|
| P0 | DEV Assembly `SanyD365.D365Extension.Sales` 中 `LeadMainPostUpdate2IMCS` Type 差异 | Coface API 配置化、汇率表配置化 | 等待相关人员处理 DEV 上 `LeadMainPostUpdate2IMCS` 的 Type 对齐 |
| P1 | `mcs_coface_exchange_rate` 实体 DEV 最终集成验证 | F6.8 汇率转换 | 待 DEV Assembly 更新后，触发状态 11 验证 TC-CFPI-016 / TC-CFPI-017 |
| P1 | `mcs_coface_nace_mapping` 实体 DEV 创建 | F6.9 NACE 行业映射 | 等 DEV 上 `LanguageProvision` 锁释放后重试 `create` + `seed-coface-nace-mappings` |

---

## 下一步行动（按依赖排序）

1. **处理 DEV Assembly Type 差异** → 解决 `LeadMainPostUpdate2IMCS` 问题
2. **更新 DEV 主 Assembly** → 用最新 `uat` 编译的 DLL 更新 `SanyD365.D365Extension.Sales`
3. **DEV 最终验证** → Coface API 配置化 + 汇率表配置化
4. **用户手动 n8n 发布实体** → 将 `mcs_coface_exchange_rate` 等新增实体/字段先发布到 UAT
5. **用户手动 n8n 发布 Plugin** → 将 `MCSPlugin` Solution 中的 Plugin 发布到 UAT

---

## 更新规则

- 每次完成一个发布环节后，更新对应单元格
- 发现新阻塞点时，加入"阻塞点汇总"
- 功能上 UAT 后从本表移除，或标记为归档
