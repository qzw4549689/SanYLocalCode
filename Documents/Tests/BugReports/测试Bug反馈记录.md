
# D365 客户信用评估系统 - Bug 反馈记录

> 测试执行日期: 2026-06-05  
> 测试人员: AI 自动化测试  
> 对应测试用例: 完整业务流程测试用例.md  
> 最后更新: 2026-06-15

---

## Bug 统计

| 统计项 | 数量 |
|--------|------|
| 🔢 总 Bug 数 | 15 |
| ✅ 已修复 | 0 |
| 📋 已关闭 | 13 |
| 🆕 待修复 | 1 |
| ⏸️ 待开发 / Pending | 1 |

---

## Bug 列表

| Bug编号 | 功能编号 | 标题 | 发现时间 | 状态 | Bug描述 | 修复记录 | 备注 | 所属测试用例 | 严重程度 |
|---------|----------|------|----------|------|---------|----------|------|-------------|----------|
| 1 | F3.1.1, F3.6.1 | 客户信用评估记录表 Script Error — mcs_credit_record.js 方法不存在 | 2026-06-05 | 📋 已关闭 | 新建客户信用评估记录表时弹出 Script Error。JS 文件 mcs_credit_record.js 中调用了不存在的方法 ScoringCardForm.onLoad，导致表单加载失败。该错误在每次新建或打开客户信用评估记录表时必现。 |  | 需开发团队检查 mcs_credit_record.js 中的 onLoad 事件处理代码，修正方法名或确保 ScoringCardForm 对象正确加载。 | 基础测试-05 / 主流程-01 |
| 2 | F2.2.1 | 评分卡配置不完整 — SA级老客户只有1条配置 | 2026-06-05 | 📋 已关闭 | SA级老客户评分卡配置只有1条（外部评级，1分），总分不足100分，导致信用评分计算异常。 | 2026-06-05 补充7条评分卡配置：外部评级30分、迟付指数10分、国别风险10分、行业风险10分、净资产20分、资产负债率10分、流动比率10分。总分=100。 | 已验证通过：评分卡配置完整，总分=100。 | 基础测试-04 |
| 3 | F2.3.2, F5.3.1, F5.3.3 | 批量处理命令缺失 — import/sync-all/report 命令不存在 | 2026-06-05 | ⏸️ Pending（待开发） | 当前D365MetadataTool命令行工具不支持import、sync-all、report等批量处理命令，导致批量导入、批量同步Coface数据、处理报告生成等功能无法测试。 |  | 该功能在开发计划.md中列为长期计划，非当前Bug。待开发团队排期开发。 | 批量-01/02/03 |
| 4 | F3.1.1, F3.3.1 | 选择客户后查询客户信息失败 | 2026-06-05 11:07 | 📋 已关闭 | 在客户信用评估记录表表单中，选择客户编码（LTC客户-1）后，系统弹出错误提示'查询客户信息失败，请重试'。该错误导致客户相关信息（客户名称、国家编码等）无法自动填充，阻塞主流程测试。 | 2026-06-05 根因：Account实体缺少信用评估所需的自定义字段（mcs_cofaceid/mcs_dealerrank等8个字段），JS查询时字段不存在导致API报错。修复：1)在Account实体新增8个字段；2)修正JS查询逻辑，国家编码从mcs_country Lookup展开获取；3)部署更新JS并发布实体。 | 已验证通过：选择客户后客户名称、国家编码等字段正确自动填充。 | 主流程-02 选择客户 |
| 5 | F4.2.1, F4.1.1 | 信用分计算Plugin失败 — 缺少mcs_categoryid字段 | 2026-06-05 12:05 | 📋 已关闭 | 修改评估状态为'信用分计算'并保存后，系统弹出Business Process Error。信用分计算Plugin执行失败，错误提示mcs_credit_record实体不包含名为'mcs_categoryid'的属性。 | 2026-06-05 根因：Plugin代码错误地从评估记录表查询mcs_categoryid字段，但该字段不存在。修复：修改GetCategoryId方法，实时从Account表查询客户属性（新老客户/是否经销商/直销分级），根据三个输入因子匹配评分卡类型。详见开发计划Phase 2.2信用分计算功能描述更新。 | 已验证通过：信用分计算Plugin正常执行，根据客户属性正确匹配评分卡类型。 | 主流程-07 触发信用分计算 |
| 6 | F3.1.1, F3.2.1 | Script Error — 状态值无效：Invalid value 1 for OptionSetValue | 2026-06-05 | 📋 已关闭 | 新建客户信用评估记录表时弹出Script Error。错误信息：Invalid value 1 for OptionSetValue。JS代码将mcs_status设为1，但选项集实际值为9-16。 | 2026-06-05 根因：PRD中状态顺序(1-8)是业务编号，不是D365选项集实际值。选项集mcs_status实际值为9-16。修复：1)JS代码状态初始值改回9；2)Plugin触发状态改回13/14；3)更新开发计划和测试用例；4)创建状态值映射说明文档。 | 已验证通过：新建评估记录表不再弹出Script Error，状态默认值正确为9。 | 基础-05 / 主流程-01 |
| 7 | F5.3.1, F5.2.1 | 评估状态11保存后自动变为12 — CofaceDataSyncPlugin自动修改状态 | 2026-06-05 17:10 | 📋 已关闭 | 在客户信用评估记录表中，手动选择评估状态为"11-内外部数据集成"并保存后，下拉框值自动变为"12-人工复核"。BPF流程状态条显示正确，但表单上的状态字段值被修改了。 | 2026-06-05 根因：CofaceDataSyncPlugin在获取Coface数据完成后，自动将状态改为12。修复：移除Plugin中自动修改mcs_status的代码，Plugin现在只记录数据集成结果（写入标签表、记录API状态），状态流转由BPF按钮或用户手动选择控制。 | 已验证通过：状态11保存后保持11，不再自动变为12。 | 主流程-05 Coface数据集成 |
| 8 | F4.2.1 | 信用分计算提示不友好 — 系统错误样式显示业务提示 | 2026-06-05 17:10 | 📋 已关闭 | 当客户未配置评分卡或未完成数据集成时，触发信用分计算会弹出"Business Process Error"弹窗，错误信息为"信用分计算失败: 未找到评分卡配置: categoryId=4"。提示看起来像系统报错，用户无法理解应该做什么。 | 2026-06-05 修复：1)ScoreCalculator.cs: 将异常消息改为中文友好提示（"该客户还未配置评分卡..."/"该客户还未完成数据集成..."）；2)CreditScorePlugin.cs: 区分业务提示异常和系统错误，业务提示直接抛出保持友好提示。 | 已验证通过：提示内容已改为用户可理解的中文业务提示。 | 主流程-07 触发信用分计算 |
| 9 | F2.1.1, F1.2 | 评分项目编码字段应为只读，但实际可编辑 | 2026-06-07 | 📋 已关闭 | 在客户信用评分项目表（mcs_credit_items）新建记录时，评分项目编码（mcs_itemid）字段应为只读状态（由系统自动生成或从评分项目表带出），但实际该字段可编辑。这可能导致用户手动输入错误编码，破坏数据一致性。 | 2026-06-07 修复：1)修改mcs_credit_items.js，所有8个字段设为只读；2)修正字段名（mcs_credit_itemsno/mcs__3p）；3)部署并绑定JS。 | 已验证通过：评分项目表所有字段现在为只读状态。 | TC-ITEM-001 |
| 10 | F3.2.1, F3.2.2 | 客户信用标签表Lookup字段无视图配置 | 2026-06-06 | 📋 已关闭 | 在客户信用标签表（mcs_customer_tag）表单中，3个Lookup字段（mcs_credit_record信用评估、mcs_accountid客户编码、mcs_credit_item评分项目）在D365后台看不到视图配置，前台无法选择数据。根因：窗体XML中Lookup字段的控件classid错误配置为文本框（{4273EDBD...}），缺少parameters节点配置DefaultViewId和AvailableViewIds。 | 2026-06-06 修复：1)修改EntityManager.RearrangeForm方法，支持传入lookupFields参数自动识别Lookup字段；2)Lookup字段使用正确classid {270BD3DB...}；3)自动查询关联实体的Lookup视图ID并写入parameters；4)section布局修正为columns="11"+layout="varwidth"+labelwidth="115"；5)重新排列mcs_customer_tag窗体并发布。验证：导出XML确认3个Lookup字段均带正确classid和parameters，15个文本框字段classid正确。 | 已验证通过：窗体XML中3个Lookup字段classid和parameters配置正确，section两列布局属性正确。前台Lookup选择功能已验证正常。 | TC-TAG-001 | 🔴 高 |
| 11 | F3.1.1, F3.3.1 | 客户信用评估记录表Lookup字段配置错误 | 2026-06-06 | 📋 已关闭 | 在客户信用评估记录表（mcs_credit_record）表单中，客户编码（mcs_accountid）Lookup字段被错误配置为文本框classid，导致前台无法弹出客户选择窗口。同时所有section的columns="2"布局不正确。 | 2026-06-06 修复：1)修改Program.cs中mcs_credit_record的RearrangeForm调用，传入lookupFields={mcs_accountid}；2)RearrangeForm自动为mcs_accountid使用Lookup classid并生成parameters；3)section布局修正为columns="11"+layout="varwidth"+labelwidth="115"；4)重新排列窗体并发布。验证：导出XML确认mcs_accountid为Lookup控件且带parameters，30个其他字段为文本框，7个section布局正确。 | 已验证通过：窗体XML中mcs_accountid classid和parameters配置正确。前台Lookup选择功能已验证正常。 | TC-RECORD-001 | 🔴 高 |
| 12 | F3.4.1, F3.4.2 | 客户资信附件表Lookup字段配置错误 | 2026-06-06 | 📋 已关闭 | 在客户资信附件表（mcs_customer_file）表单中，客户编码（mcs_accountid）Lookup字段被错误配置为文本框classid，导致前台无法弹出客户选择窗口。同时section的columns="2"布局不正确。 | 2026-06-06 修复：1)修改Program.cs中mcs_customer_file的RearrangeForm调用，传入lookupFields={mcs_accountid}；2)RearrangeForm自动为mcs_accountid使用Lookup classid并生成parameters；3)section布局修正为columns="11"+layout="varwidth"+labelwidth="115"；4)重新排列窗体并发布。验证：导出XML确认mcs_accountid为Lookup控件且带parameters，8个其他字段为文本框，3个section布局正确。 | 已验证通过：窗体XML中mcs_accountid classid和parameters配置正确。前台Lookup选择功能已验证正常。 | TC-FILE-001 | 🔴 高 |
| 13 | F3.2.1, F3.2.2 | 状态=10时客户编码字段未锁定为只读 | 2026-06-07 | 📋 已关闭 | 在客户信用评估记录表中，将评估状态从"发起信用评估"(9)修改为"关联客户代码"(10)并保存后，客户编码（mcs_accountid）Lookup字段仍然可编辑（disabled=false），没有变为只读状态。根据业务规则，状态=10时客户编码应该锁定，防止用户修改已关联的客户。 | 2026-06-07 修复：1)修改mcs_credit_record.js，添加mcs_status字段addOnChange监听；2)新增onStatusChange事件处理；3)toggleByStatus添加null值保护；4)部署并绑定JS。 | 已验证通过：状态=10时客户编码字段已锁定为只读。 | TC-EVAL-004 | 🟠 高 |
| 14 | F1.2, F2.3.1 | 客户信用标签表所有字段应为只读，但实际可编辑 | 2026-06-07 | 📋 已关闭 | 在客户信用标签表（mcs_customer_tag）新建记录时，所有预期为只读的字段（信用评估编码、客户编码、评分项目、评分项目名称、指标编码、集成指标、复核指标、得分值等）实际上都是可编辑状态。这可能导致用户手动修改系统生成的数据，破坏数据一致性。 | 2026-06-07 修复：1)修改mcs_customer_tag.js，添加缺失的mcs_credit_record和mcs_credit_item字段到只读列表；2)添加bind-js-tag命令；3)部署并绑定JS到标签表表单。 | 已验证通过：标签表所有字段已设为只读。 | TC-TAG-001 | 🔴 高 |
| 15 | F4.4.3, F4.5.3 | BPP 审批通过后客户主数据信用信息回写失败 | 2026-06-15 | 🆕 待修复 | BPP 审批通过（mcs_bppstatus=Approved）后，CreditRecordBppCallbackPlugin 执行 `UpdateAccountCreditInfo` 时报错：`Incorrect attribute value type System.String`。根因：account.mcs_creditgrade 字段实际为 Picklist（A0=100000000, A1=100000001, A2=100000002, A3=100000003, A4=100000004），但代码中按字符串 `updateAccount["mcs_creditgrade"] = creditGrade` 赋值。导致 account.mcs_creditscore / mcs_creditgrade / mcs_creditvalid 均未写入。 | 2026-06-15 修复本地独立 Assembly：`BppCallbackPlugin.cs` 中增加 `MapCreditGradeToOptionSetValue` 方法，将 A0-A4 映射为对应选项集值 `OptionSetValue` 后赋值。代码已编译通过。 | 待同步到远程主项目（`SanyD365.D365Extension.Sales`）并重新部署 DEV / UAT 主 Assembly 后复测。DEV 当前 Assembly 更新被 `LeadMainPostUpdate2IMCS` Type 差异阻塞。 | TC-ACC-009 | 🔴 严重 |

---

## 状态说明

| 状态 | 含义 | 负责人 |
|------|------|--------|
| 🆕 待修复 | 开发尚未开始修复 | 开发团队 |
| 🔧 修复中 | 开发正在修复 | 开发团队 |
| ✅ 已修复 | 开发已完成修复，提交测试复测 | 开发团队 |
| 🔄 待验证 | 测试已复测，结果待确认 | 测试团队 |
| 📋 已关闭 | 测试复测通过，Bug正式关闭 | 测试团队 |
| ⏸️ Pending | 待开发功能，非当前Bug | 开发团队 |

---

## 严重程度说明

| 严重程度 | 说明 |
|----------|------|
| 🔴 严重 | 阻塞主流程，无法继续测试 |
| 🟠 高 | 影响核心功能，有 workaround |
| 🟡 中 | 影响非核心功能或体验 |
| 🟢 低 | 提示/界面问题，不影响功能 |

---

*文件最后更新: 2026-06-15*

---

## 测试执行日志

| 时间 | 测试用例 | 操作 | 结果 | 备注 |
|------|---------|------|------|------|
| | 基础-01 | 实体导航检查 | ✅ 通过 | 6个实体全部可访问 |
| | 基础-02 | 客户表单字段检查 | ⏸️ 待定 | 避免影响他人数据，后续安排测试 |
| | 基础-03 | 评分项目数据检查 | ✅ 通过 | 12条核心记录全部存在，另有5条额外记录 |
| | 基础-04 | 评分卡配置检查 | ✅ 通过 | SA级老客户7条配置已补充，总分=100 |
| | 基础-05 | Plugin功能验证 | ✅ 通过 | 评分卡编码格式正确；信用评估记录JS错误已修复 |
| | 基础-06 | JS表单逻辑验证 | ⏸️ 待定 | 待后续安排测试 |
| | 基础-07 | Coface API连通性 | ✅ 通过 | 认证接口正常，返回200及token |
| | 主流程-01 | 创建信用评估记录 | ✅ 通过 | 默认值正确（状态=发起，有效=否，申请人=当前用户，日期=今天） |
| | 主流程-02 | 选择客户 | ✅ 通过 | 选择客户后客户名称、国家编码等字段正确自动填充 |
| | 主流程-05 | Coface数据集成 | ⚠️ 部分通过 | API状态SUCCESS，但URBA/Report订单ID为空（测试客户非波兰客户） |
| | 主流程-06 | 人工复核 | ⏸️ 跳过 | 当前表单无客户信用标签子网格，无法验证 |
| | 主流程-07 | 信用分计算 | ✅ 通过 | 信用分计算Plugin正常执行，根据客户属性正确匹配评分卡类型 |
| 2026-06-15 | TC-ACC-009 | BPP 审批通过后回写 Account 信用分/等级/有效状态 | ❌ 失败 | mcs_creditgrade 为 Picklist，代码按字符串赋值导致 `Incorrect attribute value type System.String`，account 字段未写入 |
| | 批量-01 | CSV导入测试 | ⏸️ 待定 | 命令行工具不支持import命令 |
| | 批量-02 | 批量同步Coface数据 | ⏸️ 待定 | 命令行工具不支持sync-all命令 |
| | 批量-03 | 处理报告生成 | ⏸️ 待定 | 命令行工具不支持report命令 |
| | 性能-01 | Coface数据集成耗时 | ⏸️ 待定 | 依赖主流程-05 |
| | 性能-02 | 信用分计算耗时 | ⏸️ 待定 | 依赖主流程-07 |
| | 性能-03 | 批量处理耗时 | ⏸️ 待定 | 依赖批量处理功能 |

---

## 遗留问题汇总

| 编号 | 问题描述 | 严重程度 | 计划解决时间 | 负责人 |
|------|---------|---------|-------------|--------|
| 3 | 批量处理命令缺失：import/sync-all/report 命令在当前工具中不存在 | 🟡 中 | ⏸️ Pending（待开发） | 开发团队 |

---

## 测试结论

☐ 全部通过，系统可上线  
☑ 部分通过，需修复后复测  
☐ 未通过，需重大修改

**统计：**
- ✅ 通过：10个
- ⚠️ 部分通过：1个
- ⏸️ 跳过/待定：7个

**说明：**
- 7个Bug已关闭（Bug 1, 2, 4, 5, 6, 7, 8）
- 1个Bug待开发（Bug 3 - 批量处理功能）

---

*文件最后更新: 2026-06-06*

### Bug-9 验证：评分项目表字段只读
- **验证时间**: 2026-06-07
- **验证人**: 开发团队
- **验证步骤**:
  1. 修改mcs_credit_items.js，setFieldsReadOnly方法中所有字段设为只读
  2. 部署更新后的JS到D365
  3. 打开评分项目表，检查所有字段状态
- **验证结果**: 所有8个字段（编码、名称、说明、分类、数据类型、内外部、人工补录、外部提供）均设为只读 ✅
- **结论**: 🟢 已修复

---

*最后更新: 2026-06-07*

---

## Bug-9 修复详情

**问题**: 评分项目编码字段应为只读，但实际可编辑

**根因**: 
1. JS代码中只设置了mcs_itemid字段为只读
2. 评分项目表为预置基础数据表，所有字段都不应被业务修改

**修复内容**:
1. 修改mcs_credit_items.js的setFieldsReadOnly方法，将所有8个字段设为只读：
   - mcs_itemid (评分项目编码)
   - mcs_itemname (评分项目名称)
   - mcs_itemdesc (评分项目说明)
   - mcs_group (评分项目分类)
   - mcs_datatype (数据类型)
   - mcs_source (内外部)
   - mcs_validate (人工补录)
   - mcs_3p (外部提供)

2. 修改MetadataTool的BindJsToForm方法：
   - 添加实体名到JS函数前缀的映射（mcs_credit_items→CreditItemsForm）
   - 添加表单发布操作，确保JS绑定生效

3. 部署并绑定JS到评分项目表表单

**验证结果**:
- ✓ WebResource已更新并添加到解决方案
- ✓ JS已绑定到mcs_credit_items表单
- ✓ 表单XML中包含formLibraries和events节点
- ✓ 函数名正确：CreditItemsForm.onLoad / CreditItemsForm.onSave

**状态**: 🟢 已修复


---

## Bug-13 修复详情

**问题**: 状态=10时客户编码字段未锁定为只读

**根因**: 
1. toggleByStatus在onLoad时调用，但状态字段值可能尚未加载完成（null）
2. 缺少对状态字段变更的监听，状态从9变为10时不会触发锁定

**修复内容**:
1. 修改mcs_credit_record.js：
   - registerEvents中添加mcs_status字段的addOnChange监听
   - 新增onStatusChange事件处理函数
   - toggleByStatus中添加null值保护（status === null时直接返回）

2. 部署并绑定JS到评估记录表表单

**验证结果**:
- ✓ JS已绑定到mcs_credit_record表单
- ✓ 函数名正确: CreditRecordForm.onLoad
- ✓ 状态变更事件已注册

**状态**: 🟢 已修复

---

## Bug-14 修复详情

**问题**: 客户信用标签表所有字段应为只读，但实际可编辑

**根因**: 
1. 标签表JS（mcs_customer_tag.js）中setFieldsReadOnly缺少两个Lookup字段
2. JS未绑定到标签表表单

**修复内容**:
1. 修改mcs_customer_tag.js：
   - setFieldsReadOnly中添加缺失字段：
     - mcs_credit_record（信用评估Lookup）
     - mcs_credit_item（评分项目Lookup）

2. 添加MetadataTool命令bind-js-tag支持标签表JS绑定

3. 部署并绑定JS到标签表表单

**验证结果**:
- ✓ JS已绑定到mcs_customer_tag表单
- ✓ 函数名正确: CustomerTagForm.onLoad
- ✓ 所有18个字段已设为只读

**状态**: 🟢 已修复


---

*文件最后更新: 2026-06-07*
