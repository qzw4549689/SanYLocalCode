---
name: d365-tools
description: D365 项目部署工具使用指南。适用于 MetadataTool CLI、DeployTool 已封装方法、Modern Command Bar（App Action）按钮部署。
---

# D365 工具使用指南

## 1. MetadataTool CLI 速查

> **适用场景**：实体/字段/表单/视图/Plugin/JS 的创建、修改、查询、导出

```bash
cd Code/Tools/MetadataTool

# ========== 实体操作 ==========
dotnet run create <json文件>              # 从JSON创建实体+字段（EntityDefinition格式）
dotnet run list-fields <实体名>           # 列出实体所有字段（含类型、必填等）
dotnet run delete-field <实体名> <字段名>  # 删除指定字段
dotnet run add <实体名> <解决方案名>       # 添加实体到解决方案
dotnet run remove <实体名> <解决方案名>    # 从解决方案移除实体

# ========== 字段操作 ==========
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
dotnet run register-plugin <DLL路径> <类名> [实体名]  # 注册Create Plugin
# RegisterPlugin()      - 注册Plugin（C#调用）
# RegisterPluginWithFilter() - 注册带筛选属性的Update Plugin

# ========== 发布 ==========
dotnet run publish [实体名]               # 发布指定实体（不传则PublishAll）
# PublishEntity()       - 发布单个实体
# PublishAll()          - 发布所有自定义项

# ========== 解决方案 ==========
dotnet run export <解决方案名> <路径>      # 导出Solution为ZIP
# ExportSolution()      - 导出解决方案

# ========== 测试数据 ==========
dotnet run create-credit-items            # 创建评分项目测试数据(22条)
dotnet run create-qualitative-enums       # 创建定性枚举值测试数据(30条)
```

---

## 2. DeployTool 已封装方法

```csharp
// 已封装方法，直接使用：
UpdateWebResource(ServiceClient service, string webResourceName, string filePath)
// 更新 JS WebResource 内容（Base64编码自动处理）

DeployAppActions(ServiceClient service)
// 部署 Modern Command Bar 按钮（App Action），替代 RibbonDiff.xml

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

## 3. Modern Command Bar 按钮部署（App Action）

> **核心原则：所有按钮必须通过 C# SDK 创建 App Action，禁止用 RibbonDiff.xml。**

### 3.1 为什么禁止 RibbonDiff.xml

| 问题 | 后果 |
|------|------|
| RibbonDiff.xml 创建的是 **Legacy Ribbon** | 按钮在 D365 界面显示为**只读**，无法编辑 |
| Legacy 按钮无法通过 UI 删除 | 必须用 **Ribbon Workbench**（Windows + XrmToolbox） |
| Solution 导入方式部署 | 阻塞环境 5-60 分钟，无法取消 |
| 与现代 Command Bar 不兼容 | 同一实体上 Legacy + Modern 按钮混用，体验混乱 |

### 3.2 App Action 正确创建方式

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

### 3.3 关键字段类型对照表（必须严格匹配）

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

### 3.4 获取 entityId 的方法

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

### 3.5 按钮参数格式

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
