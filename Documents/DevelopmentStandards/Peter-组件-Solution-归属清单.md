# Peter 负责组件 Solution 归属清单

## 基本信息

| 项目 | 内容 |
|---|---|
| 检查日期 | 2026-07-07 |
| 检查环境 | DEV1 (`https://dev1.crm5.dynamics.com`) / UAT (`https://sany-uat.crm5.dynamics.com`) |
| 规范依据 | 《三一D365开发规范文档》4.4 解决方案发布规范 |
| 检查范围 | 信用评估 + 厂端授信/成交条件相关组件 |

## 规范要求速查

| 组件类型 | 应属 Solution | 包类型 | 备注 |
|---|---|---|---|
| WebResource（js/html/json 等） | **McsWebResource** | 托管全量包 | 不要混入实体、站点地图、OptionSet 等 |
| Plugin Assembly | **McsPlugin** | 托管全量包 | 统一放在大 DLL 中 |
| Custom API | **McsCustomAPI** | 托管全量包 | 含请求参数/响应属性 |
| Entity（实体/站点地图/视图等） | **entity_XX** | 非托管增量包 | XX 为版本号，每次迭代新建 |
| OptionSet | **McsOptionSet** | 托管包 | 实体关联的下拉选项 |
| 角色 | **role_XX** | 非托管包 | 每次角色变更新建 |
| 工作流 | **McsAutomate** | 托管包 | 自定义工作流 |

> 注：DEV 环境当前多为 **非托管** Solution，UAT 环境对应为 **托管** Solution。只要 Solution 名称匹配规范，即视为 Solution 归属正确。

---

## 关于 Active Layer 的检查说明

> **更新**：已找到并通过官方 `msdyn_componentlayer` 虚拟实体实现可编程检测。
>
> **可靠检测方式**：
> 1. **D365 Maker Portal【解决方案层】UI**：仍是最权威的判断方式。打开组件 → 点击【解决方案层】/【See solution layers】，若只有 Managed Solution 层（如 `McsWebResource`）则无 Active Layer；若顶层出现 `Active` / `Default` 等非托管层，则有 Active Layer。
> 2. **`msdyn_componentlayer` 虚拟实体（Organization Service / Web API）**：
>    - 查询条件：`msdyn_solutionname = "Active"` + `msdyn_solutioncomponentname = "WebResource"` + `msdyn_componentid = "<组件ID>"`
>    - 关键字段：`msdyn_changes`（JSON，记录 Active Layer 的变更属性）
>    - 参考：Microsoft Docs [Component Layer (msdyn_componentlayer) table/entity reference](https://learn.microsoft.com/power-apps/developer/data-platform/reference/entities/msdyn_componentlayer)、PowerDataOps `Test-XrmComponentCustomization`
>
> **已被证明不可靠的方式**：
> - `modifiedon` 比较法——发布、Managed Solution 升级等都会更新 `modifiedon`，但不一定产生 Active Layer。
> - `solutioncomponent` 在 `Active` / `Default` Solution 中是否有记录——每个组件默认都有系统 Solution 引用记录，不能作为判断标准。
> - `RetrieveSolutionMetadataForComponent` 内部消息——返回的是包含组件的 Solution 列表，不是层列表。
>
> 当前 `MetadataTool` 已实现 `check-webresource-active-layer <WebResource名称>` 命令，可对 WebResource 进行批量 Active Layer 检测。

---

## 1. WebResource

| 组件名称 | 类型 | 规范应属 | DEV 实际 | UAT 实际 | UAT Active Layer（以 UI 为准） | Solution 归属是否一致 | 备注 |
|---|---|---|---|---|---|---|---|
| mcs_credit_profile.html | HTML | McsWebResource | McsWebResource（非托管） | McsWebResource（托管） | ✅ 已确认无 | ✅ | MetadataTool 检测通过；待 n8n 发版验证 |
| mcs_credit_wheel.html | HTML | McsWebResource | McsWebResource（非托管） | McsWebResource（托管） | ✅ 已确认无 | ✅ | 同上 |
| mcs_credit_record.js | JScript | McsWebResource | McsWebResource（非托管） | McsWebResource（托管） | ✅ 已确认无 | ✅ | 同上 |
| mcs_credit_items.js | JScript | McsWebResource | McsWebResource（非托管） | McsWebResource（托管） | 待 UI 确认 | ✅ | 无异常反馈 |
| mcs_credit_scoringcard.js | JScript | McsWebResource | McsWebResource（非托管） | McsWebResource（托管） | 待 UI 确认 | ✅ | 无异常反馈 |
| mcs_credititem_value.js | JScript | McsWebResource | McsWebResource（非托管） | McsWebResource（托管） | ✅ 已确认无 | ✅ | 曾检测到 Active Layer（`webresourceidunique`、`contentfileref`），用户已删除 |
| mcs_credit_wheel_echarts.js | JScript | McsWebResource | McsWebResource（非托管） | McsWebResource（托管） | 待 UI 确认 | ✅ | 无异常反馈 |
| mcs_credit_wheel_vue.js | JScript | McsWebResource | McsWebResource（非托管） | McsWebResource（托管） | 待 UI 确认 | ✅ | 无异常反馈 |

### 说明
- 所有 WebResource 当前都已归入 `McsWebResource`，**Solution 归属本身正确**。
- `mcs_credit_profile.html`、`mcs_credit_wheel.html`、`mcs_credit_record.js` 三个文件已通过 `MetadataTool check-webresource-active-layer` 在 UAT 检测，**确认无 Active Layer**。
- `mcs_credititem_value.js` 曾在 UAT 检测到 **Active Layer**（变更属性 `webresourceidunique`、`contentfileref`），用户已删除，复测确认无 Active Layer。
- 其余 4 个 WebResource 暂无异常反馈，但建议发版前通过 UI 的【解决方案层】或 `check-webresource-active-layer` 逐一确认。

---

## 2. Plugin Assembly

| 组件名称（DLL） | 包含代码 | 规范应属 | DEV 实际 | UAT 实际（推断） | UAT Active Layer（以 UI 为准） | Solution 归属是否一致 | 备注 |
|---|---|---|---|---|---|---|---|
| SanyD365.D365Extension.Sales | TradeStPayTerm 相关 Plugin、信用评估部分 Plugin | McsPlugin | McsPlugin（非托管） | McsPlugin（托管） | 待 UI 确认 | ✅ | 代码库中独立项目最终打包到此 DLL |
| SanyD365.D365ExtensionApi.Sales | TradeStPayTerm 相关 Custom API | McsCustomAPI | McsCustomAPI（非托管） | McsCustomAPI（托管） | 待 UI 确认 | ✅ | 含 `mcs_QueryTradeStPayTerm` 等 |

### 说明
- DEV 中实际注册的 Plugin Assembly 为大 DLL（`SanyD365.D365Extension.*`、`SanyD365.D365ExtensionApi.*`）。
- 代码库中存在的 `SanyD365.Plugins.CreditScore`、`SanyD365.Plugins.CreditRecord`、`SanyD365.Plugins.TradeStPayTerm` 等独立项目**仅用于本地测试**，与项目正式发布无关，无需纳入 Solution 归属检查范围。
- 项目正式发布的 Plugin 代码最终统一打包到上述大 DLL 中，归属 `McsPlugin` / `McsCustomAPI`。
- Active Layer 状态无法通过工具批量准确判断，建议必要时在 UI 中查看 Plugin Assembly 的解决方案层。

---

## 3. Custom API

| 组件名称 | 实现 Plugin Type | 规范应属 | DEV 实际 | UAT 实际（推断） | UAT Active Layer（以 UI 为准） | Solution 归属是否一致 | 备注 |
|---|---|---|---|---|---|---|---|
| mcs_QueryTradeStPayTerm | `SanyD365.D365Extension.Sales.Plugins.TradeStPayTerm.QueryTradeStPayTermPlugin` | McsCustomAPI | McsCustomAPI（非托管） | McsCustomAPI（托管） | 待 UI 确认 | ✅ | 请求参数/响应属性均在 McsCustomAPI 中 |

### 说明
- `mcs_QueryTradeStPayTerm` 归属 `McsCustomAPI`，Solution 归属正确。
- Active Layer 状态无法通过工具批量准确判断，建议必要时在 UI 中查看 Custom API 的解决方案层。

---

## 4. Entity（实体）

| 实体逻辑名 | 规范应属 | DEV 实际 | UAT 实际 | Solution 归属是否一致 | 备注 |
|---|---|---|---|---|---|
| mcs_credit_record | entity_XX | entity_20260603_peter, entity_20260701_peter, entity_20260622 等 | entity_20260603_peter, entity_20260701_peter, entity_20260622 | ✅ | 分散在多个版本增量包中，符合规范 |
| mcs_credit_items | entity_XX | entity_20260603_peter, entity_20260701_peter, entity_20260622 等 | entity_20260603_peter, entity_20260701_peter, entity_20260622 | ✅ | 同上 |
| mcs_credit_scoringcard | entity_XX | entity_20260603_peter, entity_20260622 等 | entity_20260603_peter, entity_20260622 | ✅ | 同上 |
| mcs_credititem_value | entity_XX | entity_20260603_peter, entity_20260701_peter, entity_20260622 等 | entity_20260603_peter, entity_20260701_peter, entity_20260622 | ✅ | 同上 |
| mcs_customer_tag | entity_XX | entity_20260603_peter, entity_20260701_peter, entity_20260622 等 | entity_20260603_peter, entity_20260701_peter, entity_20260622 | ✅ | 同上 |
| mcs_dd_data | entity_XX | entity_20260628_wy, entity_20260629_qy, SanyEntity 等 | entity_20260615_qy, entity_20260628_wy, entity_20260623_qy 等 | ✅ | 同一名称实体在不同迭代中可能属于不同人负责的版本包 |
| mcs_dd_evaluation | entity_XX | entity_20260625_qy, entity_20260629_qy, SanyEntity 等 | entity_20260615_qy, entity_20260625_qy, entity_20260629_qy 等 | ✅ | 同上 |

### 说明
- 所有实体均已在 `entity_XX` 类型的非托管增量包中，**Solution 归属正确**。
- DEV 和 UAT 中实体所属的具体 `entity_XX` 包略有差异，属于正常现象（UAT 通常只安装了部分版本的包）。

---

## 5. 异常项汇总

| 序号 | 组件 | 类型 | 问题 | 建议处理 |
|---|---|---|---|---|
| 1 | `McsWebResource` 版本号 | Solution | UAT 版本号 `1.6.0.921` 与 DEV 相同，Managed Solution 可能不触发升级 | n8n 发布时确保版本号高于 `1.6.0.921` |
| 2 | `mcs_credit_profile.html` | WebResource | MetadataTool 已确认 UAT 无 Active Layer | 发布 `McsWebResource` 新版本后强制刷新验证 |
| 3 | `mcs_credit_wheel.html` | WebResource | 同上 | 同上 |
| 4 | `mcs_credit_record.js` | WebResource | 同上 | 同上 |
| 5 | `mcs_credititem_value.js` | WebResource | 曾存在 Active Layer，用户已删除，复测确认无 | 无需进一步处理 |

---

## 6. 后续建议

1. **UAT 发版前检查清单**
   - 在 D365 Maker Portal 中通过【解决方案层】确认要发布的组件没有 Active Layer。
   - 确认 `McsWebResource` / `McsPlugin` / `McsCustomAPI` 等 Managed Solution 版本号已递增。
   - 发布完成后在 UAT 强制刷新，验证客户画像、信用飞轮、信用评估记录 JS 逻辑正常。

2. **本地修改同步流程**
   - 当前本地 Mac 上的 `SanYi` 目录仅用于个人开发与备份。
   - 正式发布需将修改同步到远程服务器 `tx-windows`（`122.51.232.70`，`C:\Projects\D365`）上的 Azure DevOps 主仓库，并重新打包 Solution。

3. **组件归属变更**
   - 如后续新增实体，按规范新建 `entity_XX` 非托管增量包。
   - 如新增 WebResource/Plugin/CustomAPI，分别加入 `McsWebResource` / `McsPlugin` / `McsCustomAPI`。
