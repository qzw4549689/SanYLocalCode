---
name: d365-dev
description: D365 项目开发规范与流程。适用于命名规范、状态值映射、常见陷阱、代码复用、前端控件、自定义进度条、远程开发服务器连接与 Git 工作流。
---

# D365 开发规范与流程

## 1. 命名规范

| 类型 | 规范 | 示例 |
|------|------|------|
| 实体前缀 | `mcs_` (Microsoft Custom Sany) | `mcs_credit_record` |
| 编码规则 | 业务缩写 + YYYYMMDD + 4 位序号 | `SC202506040001`, `SCO202506040001` |
| 解决方案 | `entity_YYYYMMDD_peter` | `entity_20260603_peter` |
| Plugin 隔离 | Sandbox 模式，严格校验 `target.LogicalName` | — |

---

## 2. 状态值映射（重要！）

PRD 中的状态顺序（1-8）是**业务编号**，D365 选项集实际值为 **9-16**：

| 业务状态 | 选项集值 |
|---------|---------|
| 发起信用评估 | 9 |
| 关联客户代码 | 10 |
| 数据集成 | 11 |
| 人工复核 | 12 |
| 信用分计算 | 13 |
| 审核申请 | 14 |
| 审批通过 | 15 |
| 审批未通过 | 16 |

> ⚠️ **教训**：修改选项集相关代码前，必须先查询实际的选项集值。PRD 编号 ≠ 技术实现值。

### 选项集值格式

| 场景 | 格式 | 注意 |
|------|------|------|
| 创建记录 | `100000000` / `100000001` | 标准 D365 选项集值 |
| `categoryid` 查询返回 | `1` ~ `7` | 创建用 100000000 格式，查询返回短值 |
| `datatype` / `typeid` | 创建和查询一致 | 无转换问题 |

---

## 3. 已知陷阱（血泪教训）

| 陷阱 | 后果 | 解决方案 |
|------|------|---------|
| 复制窗体时忘记改 JS 方法名 | Script Error，表单无法加载 | 同步修改 `functionName` + JS 对象名 + 文件名 |
| `LastIndexOf("</rows>")` 匹配 footer | 字段插入到 footer，Power Apps 不显示 | 在第一个 `<control>` 后的 `</row>` 处插入 |
| `cell id` 不是有效 GUID | FormXml schema 验证失败 | 使用 `Guid.NewGuid().ToString("B")` |
| 硬编码不存在的字段名 | 实体未找到 / 编译错误 | 先 `list-fields` 查询实际字段 |
| 直接修改系统角色/原生 Web 资源 | 升级后被重置 | 只扩展自定义组件，不碰原生 |
| Plugin 未校验实体名 | 误触发其他实体 | 代码开头严格 `if (target.LogicalName != "xxx") return;` |
| Coface 接口文档不完整 | 集成开发受阻 | 预留 Mock 数据机制，降低依赖 |
| **用 Solution 导入更新单个 WebResource** | 耗时 5-60 分钟，阻塞环境，无法取消 | **🚫 禁止！必须用 C# DeployTool** |
| **未批准就 PublishAll** | 阻塞整个环境，影响其他用户 | **🚫 禁止！必须只发布当前实体** |
| **直接操作 UAT 元数据** | 与环境管理冲突，可能导致部署失败 | **🚫 红线！必须通过 Solution/发布工具** |
| **重复造轮子写部署代码** | 浪费时间，代码质量参差不齐 | **先查已有 DeployTool/MetadataTool 方法** |
| **临时编写新的元数据创建方法** | 公共方法无法统一管理，后续升级/维护困难 | **🚫 红线！必须使用 `D365ToolCommon`/`MetadataTool` 公共方法；缺失须先申请，获准后方可扩展** |
| **Plugin Assembly 版本号相同但内容不同** | 误以为 UAT/DEV 代码一致，实际 DLL 已不同步 | **查 `pluginassembly.modifiedon`，不要只看 `version`** |
| **"ISV code reduced the open transaction count"** | Plugin 吞掉了 OrganizationService 异常，破坏事务 | **确保 catch 后必须 rethrow；DEV 正常 UAT 报错先查版本偏差** |
| **用 RibbonDiff.xml 创建按钮**（2026-07-27 规则修订） | 生成 Legacy Ribbon（UI 只读，无法编辑/删除）；但 appaction 的经典显隐规则（N:N 关联）不随 Solution 导入 | **默认用 C# AppActionDeployer 创建按钮；仅当按钮需要「显隐规则随包走」（如列表批量按钮 SelectionCountRule）时改用 RibbonDiffXml**（案例：`Code/Customizations/Ribbon/mcs_trade_stpayterm.ribbon.xml`，生产零手动步骤） |
| **直接覆盖公共语言包等通用 WebResource** | 冲掉其他模块 key，导致大面积功能异常 | **🚫 绝对禁止！必须在原文件基础上追加 key，严禁覆盖** |
| **翻译导入改 BPF 阶段名的基础语言（1033）**（2026-08-19 #1928 实锤） | 导入成功、回读 1033 仍是旧文本，英文用户依旧看到旧标签 | **BPF 阶段/步骤名的基础语言归 xaml `mcwo:StepLabel` 所有**，翻译导入和 customizations.xml steplabels 只能改非基础语言；必须改 xaml：`LanguageCode="1033"` 的 Description 改为英文，并追加同 LabelId 的 `LanguageCode="2052"` 条目（xaml 支持同 LabelId 多语言条目），随 Solution 导入即双语且随包走。BPF **名称**不受此限（普通标签存储，翻译导入即可） |
| **sitemap 随包导入后「发布成功但导航没变」**（2026-08-19 #1928 实锤） | 误判导入/发布失败，反复重试 | sitemap 导入后进 App **未发布草稿**（App 设计器可见新内容）；对单个 App 点「发布」即可物化，**无需 PublishAll**；但**服务端导航缓存有 5-10 分钟延迟**，发布成功后立刻验证会误判没生效——等几分钟硬刷再看。托管基底的 sitemap 记录 modifiedon/sitemapxml 不会因为非托管层发布而更新，**别用回读记录当验证依据，只认运行时导航 UI**<br>🆕 程序化直改 sitemap（2026-09-08 #2172 实锤）：`update-record sitemap`/Web API PATCH 写 sitemapxml **返回成功但回读永远旧值**——直改进的是 App 未发布草稿（诊断写入会全部累积进草稿，慎做），必须再调 Web API action `POST /api/data/v9.2/PublishAppModule {"AppModuleId":"<appmoduleid>"}` 才物化到 sitemap 记录并生效；改 App 菜单就是改该 App 的 isappaware sitemap（集合查询默认隐藏，需按 ID 直取或 filter isappaware eq true） |

---

## 4. 代码复用原则

### 4.1 已有方法优先原则

**开发新功能前，先检查是否已有现成方法可用：**

1. **查 DeployTool** (`Code/Tools/DeployTool/Program.cs`) — 部署相关操作
2. **查 MetadataTool** (`Code/Tools/MetadataTool/`) — 实体/字段/表单/视图操作
3. **查 Plugin 公共类** (`Code/Customizations/Plugins/*/Plugin/`) — 业务逻辑复用
4. **查 JS 公共函数** (`Code/Customizations/WebResources/JS/`) — 表单逻辑复用

**复用流程：**
```
需求 → 查已有方法 → 有直接调用 → 完成
           ↓
        没有 → 开发新方法 → 添加到对应工具类 → 记录到 SKILL.md → 完成
```

---

## 5. 可编辑子网格（Editable Grid）阶段控制

### 5.1 场景

评估记录表单上有子网格（如"客户信用标签"），需要根据评估阶段（`mcs_status`）控制子网格的可编辑状态：
- **特定阶段可编辑**（如状态 12 人工复核）
- **其他阶段只读**

### 5.2 双层控制方案（JS + Plugin）

| 层级 | 控制方式 | 作用 | 代码位置 |
|-----|---------|------|---------|
| **JS 前端** | `formContext.getControl("Subgrid_xxx").setDisabled(true/false)` | 即时反馈，用户体验 | `mcs_credit_record.js` |
| **Plugin 后端** | Update PreOperation 校验关联记录状态 | 防止绕过前端（API/SDK 直接修改） | `CustomerTagValidationPlugin.cs` |

> ⚠️ **两层都必须做**：只做 JS 会被 API 绕过，只做 Plugin 用户体验差。

### 5.3 JS 实现

```javascript
// 控制子网格可编辑性
CreditRecordForm.setGridEditable = function (formContext, gridName, editable) {
    var gridControl = formContext.getControl(gridName);
    if (gridControl) {
        gridControl.setDisabled(!editable);
    }
};

// 在 toggleByStatus 中调用
case CreditRecordForm.STATUS.MANUAL_REVIEW: // 12
    CreditRecordForm.setGridEditable(formContext, "Subgrid_new_1", true);
    break;
default:
    CreditRecordForm.setGridEditable(formContext, "Subgrid_new_1", false);
```

**获取子网格控件名称：**
```bash
cd Code/Tools/MetadataTool
dotnet run export-formxml mcs_credit_record /tmp/form.xml
# 解析 XML 查找 classid="{F9A8A302-114E-466A-B582-6771B2AE0D92}" 的 control id
```

### 5.4 Plugin 实现

```csharp
public class CustomerTagValidationPlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider)
    {
        // Update PreOperation on mcs_customer_tag
        var target = (Entity)context.InputParameters["Target"];
        
        // 获取关联的评估记录
        var creditRecordId = target.GetAttributeValue<EntityReference>("mcs_credit_record").Id;
        var creditRecord = service.Retrieve("mcs_credit_record", creditRecordId, 
            new ColumnSet("mcs_status"));
        var status = creditRecord.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;
        
        // 非状态12时阻止修改
        if (status != 12)
        {
            throw new InvalidPluginExecutionException(
                "只有在人工复核阶段才能修改信用标签数据");
        }
    }
}
```

---

## 6. 自定义进度条方案

### 6.1 背景

D365 默认 BPF（Business Process Flow）已被禁用，原因：
1. **BPF 快速编辑面板允许直接修改状态字段** — 用户可以跳过按钮，直接下拉选择状态并跳转阶段
2. **Mac 无法使用 Power Apps 表单编辑器** — 登录过期后无法点击 Sign In，无法手动调整表单
3. **BPF 样式不可定制** — 无法隐藏面板里的字段或禁用下拉框

**解决方案：** 禁用 BPF，用 HTML WebResource 实现自定义进度条，嵌入表单顶部。

### 6.2 实现方式

**文件位置：** `Code/Customizations/WebResources/HTML/mcs_credit_record_progress.html`

**表单嵌入：** 用 C# SDK 修改 FormXml，在 General tab 的"基本信息" section 上方插入 WebResource

**JS 同步：** 表单状态变更时，通过 `postMessage` 通知 iframe 内的进度条更新

### 6.3 进度条特性

- **8 个阶段**：发起信用评估 → 关联客户代码 → 内外部数据集成 → 人工复核 → 信用分计算 → 审核申请 → 审批通过 → 审批未通过
- **状态显示**：
  - ✅ 已完成（蓝色圆点 + 数字）
  - 🔵 当前进行（白底蓝字 + 阴影）
  - ⚪ 未开始（灰色）
- **蓝色进度线**：随当前状态动态填充
- **响应式**：适配不同屏幕宽度
- **只读**：用户只能查看，不能点击跳转

---

## 7. 远程开发服务器（腾讯云 Windows）

> 用于编译 D365 Plugin Assembly，本地 Mac 无法直接编译 .NET Framework 4.6.2 项目。

### 7.1 连接信息

| 项目 | 内容 |
|------|------|
| IP 地址 | `122.51.232.70` |
| 用户名 | `administrator` |
| 密码 | `Qzw@123456789` |
| 系统 | Windows Server (OpenSSH_for_Windows) |
| SSH 别名 | `tx-windows` |

### 7.2 本地 SSH 配置

```bash
# 写入新私钥（文档已更新为修复后的密钥）
cat > ~/.ssh/id_ed25519 << 'EOF'
-----BEGIN OPENSSH PRIVATE KEY-----
b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW
QyNTUxOQAAACCcQu2DsZpE3gVbxFAe6s6a+mYiUkgis6i2l2KvBi2xjgAAAJAHet/OB3rf
zgAAAAtzc2gtZWQyNTUxOQAAACCcQu2DsZpE3gVbxFAe6s6a+mYiUkgis6i2l2KvBi2xjg
AAAEDu79LP3IV2JAnTi96dYh2iHNjd2iimOVkmcRtDNuzKRJxC7YOxmkTeBVvEUB7qzpr6
ZiJSSCKzqLaXYq8GLbGOAAAACHBldGVycWl1AQIDBAU=
-----END OPENSSH PRIVATE KEY-----
EOF
chmod 600 ~/.ssh/id_ed25519

# ~/.ssh/config
Host tx-windows
    HostName 122.51.232.70
    User administrator
    StrictHostKeyChecking no
```

**常用远程命令：**
```bash
# 查看当前分支
ssh tx-windows "cd C:\\Projects\\D365 && git branch -a"

# 拉取最新 uat
ssh tx-windows "cd C:\\Projects\\D365 && git checkout uat && git pull origin uat"

# 一键编译（只编译 Sales Plugin 项目，避免 Test 项目 Secret.json 错误）
ssh tx-windows "cd C:\\Projects\\D365 && nuget restore D365\\D365.sln && msbuild D365\\SanyD365.D365Extension.Sales\\SanyD365.D365Extension.Sales.csproj /p:Configuration=Release /p:Platform=AnyCPU"

# 交互式 PowerShell
ssh tx-windows "powershell"
```

### 7.3 Git 仓库

- **Azure DevOps 项目**：`https://dev.azure.com/SanyGlobalCRM/D365`
- **Git 仓库**：`https://dev.azure.com/SanyGlobalCRM/D365/_git/D365`
- **PAT 令牌**：`<YOUR_AZURE_DEVOPS_PAT_HERE>`

**Clone 到服务器：**
```bash
ssh tx-windows "mkdir C:\\Projects 2>nul && git clone \"https://anything:<YOUR_AZURE_DEVOPS_PAT_HERE>@dev.azure.com/SanyGlobalCRM/D365/_git/D365\" \"C:\\Projects\\D365\""
```

### 7.4 编译环境

| 组件 | 版本 | 验证命令 |
|------|------|----------|
| Git | 2.49.0 | `git --version` |
| MSBuild | 18.7.1 | `msbuild -version` |
| NuGet | 7.6.0 | `nuget help` |
| .NET Framework | 4.8 + 4.6.2 Targeting Pack | — |
| VS IDE 2022 Community | — | — |

**编译命令：**
```powershell
cd C:\Projects\D365

# 还原 NuGet 包
nuget restore D365\D365.sln

# 编译 Release（推荐：直接编译目标 csproj，不要全编译 D365.sln）
# 注意：Platform 必须是 AnyCPU，不能写成 "Any CPU"；/t:SanyD365_D365Extension_Sales 目标不存在
msbuild D365\SanyD365.D365Extension.Sales\SanyD365.D365Extension.Sales.csproj /p:Configuration=Release /p:Platform=AnyCPU

# 或编译 Debug
msbuild D365\SanyD365.D365Extension.Sales\SanyD365.D365Extension.Sales.csproj /p:Configuration=Debug /p:Platform=AnyCPU
```

### 7.5 远程编译踩坑记录

| 坑点 | 现象 | 正确做法 |
|------|------|----------|
| `Platform="Any CPU"` | `没有为项目设置 BaseOutputPath/OutputPath 属性` | 用 `Platform=AnyCPU`（无空格） |
| `/t:SanyD365_D365Extension_Sales` | `该项目中不存在目标“SanyD365_D365Extension_Sales”` | 直接编译 csproj 文件，不要用 `/t:` |
| 全编译 `D365.sln` | `MSB3030 无法复制文件 Secret.json` | 只编译目标 Plugin 项目，避免 Test 项目 |
| 在 bash 中传递 PowerShell 多行脚本 | here-string 解析失败 | 优先把脚本写成 `.ps1` 文件再 scp 到远程执行 |
| 项目扩展方法冲突 | `CS0411 无法从用法中推断出 Contains<T> 的类型参数` | 用 `Array.IndexOf(array, value) >= 0` 替代 `array.Contains(value)` |

**一键复制命令（SSH 从本地 Mac 执行）：**
```bash
ssh tx-windows "powershell -Command \"cd 'C:\\\\Projects\\\\D365'; msbuild 'D365\\\\SanyD365.D365Extension.Sales\\\\SanyD365.D365Extension.Sales.csproj' /p:Configuration=Release /p:Platform=AnyCPU\""
```

### 7.6 项目结构

```
C:\Projects\D365
├── D365\                    # 主项目目录
│   ├── D365.sln             # 主解决方案
│   ├── D365\D365.csproj     # .NET Framework 4.6.2
│   └── ...
├── Common\                  # 公共库
│   ├── MSLibrary.D365.Extension
│   └── ...
├── Service\                 # Service 项目
│   └── Service.sln
└── Bot\                     # Bot 项目
    └── Bot.sln
```

### 7.7 VS Code Remote-SSH 开发

1. 安装 VS Code 扩展：**Remote - SSH**
2. `Cmd+Shift+P` → `Remote-SSH: Connect to Host...` → 选择 `tx-windows`
3. 连接后点击 **Open Folder** → 输入 `C:\Projects\D365`

---

## 8. 开发工作流（本地 → 远程 → Git）

> **核心原则**：本地 Mac 快速开发 + DEV 独立测试 → 远程 Windows 编译集成 → Azure DevOps PR 合并。

### 8.1 阶段一：本地开发（当前机器）

**适用场景**：Plugin、JS WebResource、业务逻辑代码。

| 事项 | 规范 |
|------|------|
| 命名空间 | **使用自己的命名空间**，不要和主代码（`SanyD365.D365Extension.Sales.Plugins` 等）冲突 |
| 项目结构 | 在当前机器的独立目录开发，不直接修改主代码 |
| 引用方式 | 引用本地独立的 D365 SDK 包，不与主代码的 Common/Service 等项目耦合 |
| 编译目标 | .NET 6/8（本地可编译，仅用于快速验证语法），业务逻辑和主代码保持一致 |

**本地开发目录示例：**
```
~/Work/AIWorkSpace/SanYi/Code/Customizations/Plugins/
├── CofaceIntegration/Plugin/        # 本地独立命名空间
├── CreditScore/Calculator/          # 本地独立命名空间
└── BppIntegration/Plugin/           # 本地独立命名空间
```

### 8.2 阶段二：单元测试验证功能

开发完成后，先在本地通过单元测试验证功能正确性：

```bash
# 1. 编译本地代码
cd ~/Work/AIWorkSpace/SanYi/Code/Customizations/Plugins/CofaceIntegration
dotnet build

# 2. 运行单元测试
#    - 对 Plugin 核心逻辑编写单元测试
#    - 使用 mock 或内存中 OrganizationService 验证输入输出
#    - 优先覆盖状态流转、字段计算、异常分支
```

**单元测试原则：**

| 事项 | 要求 |
|------|------|
| 测试范围 | Plugin 业务逻辑、计算类、解析类、校验类 |
| 依赖隔离 | 对 D365 SDK、外部 API、数据库访问使用 mock/stub |
| 通过标准 | 新增功能必须伴随单元测试，核心路径覆盖率不低于 80% |
| 产出物 | 测试项目 + 测试方法 + 执行结果 |

### 8.3 阶段三：DEV 集成测试验证（⚠️ 阻塞点）

> **必须等待测试人确认测试通过后，才能进入阶段四。**

单元测试通过后，再在 DEV 环境注册 Plugin 并进行集成测试：

```bash
# 1. 用 MetadataTool 或 DeployTool 注册到 DEV1
#    - 注册 Plugin Assembly
#    - 注册 SdkMessageProcessingStep
#    - 发布实体
```

| 事项 | 要求 |
|------|------|
| 测试人 | 由开发负责人或指定测试人员执行 |
| 测试内容 | 在 DEV1 前台创建测试数据，验证业务逻辑 |
| 通过标准 | 关联测试用例全部通过，无阻塞 Bug |
| 产出物 | 测试用例编号 + 执行结果 |

**流程：**
```
单元测试通过 → DEV 注册 → 通知测试人 → 测试人验证 → [通过] → 进入阶段四
                                              ↓
                                            [不通过] → 修复 → 重新跑单元测试 → 重新注册 → 重新验证
```

### 8.3.1 🚨 新增组件必须及时加入主清单 Solution（强制）

> **主清单 `AllComponent_Peter_NoUAT`（显示名「AllComponent_Peter_整理_禁止导入UAT」）是发版分布核对的唯一基准，禁止导入 UAT。**
> 发版前以其为真相源跑 `check-solution-coverage` 核对组件是否都进了发版包；主清单缺组件 = 核对失效 = 发版漏组件。

**规则：在 DEV1 新增任何 D365 组件后，必须在 DEV 功能验证通过的第一时间将其加入主清单。**

| 需要加入的组件 | 示例 |
|---|---|
| 实体 / 字段 / 表单 / 视图 / 关系 | `mcs_xxx` 新实体、新加自定义字段 |
| BPF（流程定义 + BPF 实体） | `Credit Assessment`、`mcs_credit` |
| WebResource | 新建 js/html/语言包 |
| Plugin Assembly / Step | 新注册的 Step、新 Assembly |
| Custom API（本体+参数+响应） | `mcs_QueryXxx` |
| App Action | 新的命令栏按钮 |

**不得加入的组件：**
- 本地临时独立 Assembly（如 `SanyD365.Plugins.CustomerFile` 等测试用 Assembly，测试完应注销，不发版）
- 纯测试/实验组件、他人负责的组件

**组件从环境删除时**（如注销废弃 Step），必须同步从主清单移除，避免孤儿组件。

**工具（幂等，已存在自动跳过）：**
```bash
cd Code/Tools/MetadataTool
# 单个组件（常用类型：1=实体 2=字段 10=关系 29=工作流/BPF 60=表单 61=WebResource 91=Assembly 92=Step 10023=CustomAPI）
dotnet run --no-build -- add-solution-component <componentType> <objectId> AllComponent_Peter_NoUAT
# 批量（按清单 JSON）
dotnet run --no-build -- add-manifest-to-solution <清单.json> AllComponent_Peter_NoUAT
```

> AI 代为增删主清单组件必须获得用户明确授权；发版前核对见 `/skill:d365-deploy` 5.1。

### 8.3.2 🚨 新增组件必须主动通知用户（2026-07-24 新增，每次写代码必查）

> **每次开发新增任何 D365 组件后，AI 必须在完成汇报中显式列出新增组件清单并提醒用户加包。**

| 要求 | 说明 |
|---|---|
| 通知时机 | 完成汇报时（不是等发版前才提） |
| 通知内容 | 组件类型 + 名称 + 建议归属 Solution（entity_XX / McsWebResource / McsPlugin / McsCustomAPI / 主清单） |
| 组件范围 | 实体/字段/表单/视图/关系/BPF/WebResource/Plugin Assembly/Step/Custom API/App Action/工作流/文档模板等一切新组件 |
| 加包责任人 | **Step 类组件由 AI 直接加入对应发版包**（2026-08-03 用户指示，长期有效：Step→McsPlugin，登记看板发布清单时同步完成）；其余组件（实体/App Action/WebResource 等）仍由用户加入对应的包，AI 仅提醒和（经授权后）代为操作 |
| 失误定级 | 漏报 = 发版漏组件 = 严重失误 |

> 本要求与 8.3.1（主清单强制维护）配合：8.3.1 管「加什么」，本节管「AI 必须主动说」。

### 8.3.3 🚨 开发完成必须登记任务看板「发布清单」（2026-08-03 用户明确，2026-08-05 起改为看板登记，强制执行）

> **每次开发/修 Bug，在开发内容就绪（DEV 元数据/代码部署完毕、远程分支已推送）后、完成汇报前，必须把本次待发布内容逐项登记到任务看板发布清单（`http://122.51.232.70:8100/`，📦 发布清单 Tab）——不得等 DEV 端到端验证通过才登记**（2026-08-10 #1713 教训：AI 以「等 DEV 验证」为由推迟登记被用户批评；验证阻塞时照常登记，验证状态写在任务卡片 description 里）。
> 看板服务运维（2026-08-20 加固）：tx-windows 计划任务 `KanbanBoard`（已改 ExecutionTimeLimit=PT0S 不限时+RestartOnFailure；原为默认 3 天限时，到点被杀是「经常不能访问」根因）+ 看门狗 `KanbanBoard-Watchdog`（每 5 分钟检测 8100 未监听自动拉起，实测 kill 后约 4 分钟自愈）。仍不可访问时手动：`ssh tx-windows "schtasks /run /tn KanbanBoard"`，README 见 `C:\Projects\KanbanBoard\README.md`。
> 历史教训：AI 多次漏登（如 2026-08-03 #1507/#1508 改 `mcs_fsm_data.js` 未登记，被用户发现并批评）。
> 原《待发布内容清单.md》已于 2026-08-05 按用户指示废弃删除（历史见 git），**看板是唯一登记处**。

**登记节点（开发流程固定一步，不得跳过）：**

```
本地开发 → 本地验证 → DEV 部署（元数据/代码就绪、分支推送）→ ①加主清单(8.3.1) → ②通知用户(8.3.2)
         → ③登记看板发布清单+任务卡片(本节) → 完成汇报 → DEV 端到端验证（阻塞不影响③）
```

**登记 API（逐项登记，幂等性由 AI 自查 GET 后判断）：**

```bash
# 登记一项（开发完成即执行）
curl -X POST http://122.51.232.70:8100/api/release-items/item \
  -H 'Content-Type: application/json' \
  -d '{"section":"webresource","component":"mcs_fsm_data.js",
       "summary":"#1560 状态3放行合同编号可编辑","ref":"禅道 #1560",
       "package":"McsWebResource","in_package":true}'

# 更新单项（如补入包状态）：PATCH /api/release-items/item/<rowid>
# 删除误登记：               DELETE /api/release-items/item/<rowid>
# 查询当前批次（含 rowid）：  GET /api/release-items
# 发版后按包归档：           POST /api/release-items/release {"package":"McsWebResource"}
```

**同步创建任务卡片（T-xxxx，2026-08-06 用户发现漏建后补充）：**

> 看板有两个列表：**📦 发布清单**（按组件，上表）和**任务卡片**（按 Bug/任务，「待发布」Tab 展示）。每个 Bug 修复除登记发布清单外，还必须同步建任务卡片并置「待发布」，否则用户在任务 Tab 看不到。

```bash
# 建卡（默认 status=pending）
curl -X POST http://122.51.232.70:8100/api/tasks \
  -H 'Content-Type: application/json' \
  -d '{"title":"禅道#1635 标题 [用例#546]","description":"反馈来源/根因/修复/验证/发布清单 rowid"}'
# 置「待发布」（DEV 验证通过后）
curl -X PATCH http://122.51.232.70:8100/api/tasks/T-0013 \
  -H 'Content-Type: application/json' -d '{"status":"pending_release"}'
# 状态取值：pending / pending_release（待发布）/ released（已发布）；删除：DELETE /api/tasks/<id>
```

| 要求 | 说明 |
|---|---|
| 登记时机 | 开发内容就绪（DEV 部署完毕+分支推送）后立即登记，**不等端到端验证**；是完成汇报的**前置条件**（汇报中须复述登记情况） |
| 登记范围 | **一切待发布内容都要登记，不止新组件**：①新增组件（实体/字段/Step/Custom API/App Action 等）②既有组件的代码/内容变更（JS 改动、Plugin 代码改动、语言包 key 变更）③元数据变更（必填级别/字段范围/标签）④配置数据（各环境手动项）⑤手动步骤（Command Designer/角色/BPP 模板） |
| section 取值 | `entity`（实体/字段/表单/视图/关系/BPF/Ribbon/App Action）/ `webresource`（McsWebResource）/ `plugin`（McsPlugin）/ `customapi`（McsCustomAPI）/ `config`（配置数据，不随 Solution）/ `manual`（手动步骤 + **Azure 发布环节**） |
| 🚨 看板归组规则（2026-08-10 #1713 教训，登错过一次） | 看板前端 FIXED_PACKAGES 写死：`webresource→McsWebResource`、`plugin→McsPlugin`、`customapi→McsCustomAPI`，**这三个 section 的 package 字段会被忽略**；`entity` 按 package 归组（空=默认新包）。**Azure 环节（MessageHandler/ClientAPI/ExtensionAPI/InnerAPI/CommonMessageHandle）必须用 `section=manual` + `package=环节名`** 才能独立成组（如 📁 MessageHandler）；误用 section=plugin 会被错误并入 McsPlugin 组。**登记后必须 GET 回读并模拟归组核对**（核对脚本见下） |
| 登记口径 | 每项：component（组件名）+ summary（变更内容/原因）+ ref（禅道号）+ package + in_package；**同一文件/组件被多个 Bug 改动时合并为一项**（PATCH 更新 summary/ref），ref 并列所有禅道号 |
| entity 类 package | 用户已指定发版包名（如 `entity_20260727_peter`）则填指定名；未指定留空，看板自动归入「entity_当天日期_peter」默认新包 |
| Step 类组件 | 登记时同步由 AI 直接 `add-solution-component` 加入 McsPlugin（8.3.2 用户指示），in_package 标 true |
| 不需要登记 | 本地临时独立 Assembly、纯测试组件、他人组件（红线，同样不得加主清单） |
| 发版闭环 | 用户发布某包后告知 AI → AI 调 `POST /api/release-items/release` 按包归档 + 任务看板对应任务置「已发布」 |

**登记后强制核对（防归组错误，2026-08-10 新增）：**

```bash
# 登记/修改后必须跑一次，确认每项的看板归组符合预期
curl -s http://122.51.232.70:8100/api/release-items | python3 -c "
import json,sys
FIXED={'webresource':'McsWebResource','plugin':'McsPlugin','customapi':'McsCustomAPI'}
for i in json.load(sys.stdin)['items']:
    sec=i['section']; pkg=i.get('package') or ''
    if sec=='entity': g='📁 '+(pkg or 'entity_当天_peter(默认新包)')
    elif sec in FIXED: g='📁 '+FIXED[sec]
    elif pkg: g='📁 '+pkg
    else: g='sec:'+sec
    print(f\"rowid {i['rowid']}: section={sec} package={pkg or '(空)'} -> {g}\")"
```

> 看板发布清单是「本次发什么」的视角，主清单是「全部资产」的视角，两者都要维护；发版核对统一按 `/skill:d365-deploy` 4.1 的 14 环节固定顺序矩阵执行，配合《发版检查清单.md》逐项核对。

### 8.4 阶段四：远程服务器集成（tx-windows）

DEV 测试没问题后，把代码转移到远程服务器的主代码中：

**转移时需要修改的内容：**

| 修改项 | 说明 | 示例 |
|--------|------|------|
| 命名空间 | 改为项目主命名空间 | `MyPlugin.Coface` → `SanyD365.D365Extension.Sales.Plugins.Coface` |
| 项目引用 | 改为引用主项目的 Common/Service | 移除本地独立引用，改为引用 `..\..\Common\MSLibrary.D365.Extension` |
| csproj 配置 | **必须改为 .NET Framework 4.6.2**（主项目统一版本） | `<TargetFramework>net462</TargetFramework>` |
| 类名/文件名 | 保持和本地一致（或按主项目规范调整） | — |
| **业务逻辑** | **严禁修改，必须和本地测试通过的逻辑完全一致** | — |

**转移流程：**
```bash
# 1. SSH 到远程服务器
ssh tx-windows

# 2. 进入主项目目录
cd C:\Projects\D365\D365

# 3. 拉取最新 uat
git checkout uat
git pull origin uat

# 4. 把本地代码文件复制到远程对应位置
#    （通过 scp 或直接在远程编辑）
#    例如：CofaceIntegration 相关文件复制到主项目的 Plugins/Coface/ 目录

# 5. 修改命名空间、引用、csproj 等适配主代码

# 6. 编译验证
nuget restore D365.sln
msbuild D365.sln /p:Configuration=Release /p:Platform="Any CPU"
```

### 8.5 阶段五：Git 提交与合并

远程编译通过后，走标准 Git 工作流：

```powershell
cd C:\Projects\D365\D365

# 1. 确认当前在 uat 且最新
git checkout uat
git pull origin uat

# 2. 创建个人开发分支（命名规范：uat-日期-姓名缩写-功能简述）
git checkout -b uat-260610-peter-coface-fix

# 3. 添加修改的文件
git add .

# 4. 提交（commit message 规范：中文描述 + 关联 PR/需求编号）
git commit -m "修复 Coface 数据同步 Plugin 的行业风险映射逻辑"

# 5. 推送到远程
git push -u origin uat-260610-peter-coface-fix

# 6. 🚨 PR 由用户创建（2026-08-25 用户明确）：AI 只推分支，禁止 API/任何方式自建 PR；
#    推送后把分支名告知用户，用户在 Azure DevOps 网页创建 PR → 合并到 uat
#    https://dev.azure.com/SanyGlobalCRM/D365/_git/D365
```

#### 8.5.1 推送判断标准（2026-08-15 固化，禁止再逐次询问用户）

**一句话标准：远程仓库（tx-windows）是否跟踪该文件、该资产的发布是否以合并后 uat 代码为准。拿不准时先用 `git ls-files | findstr <文件名>` 在 tx-windows 核实，不要问用户。**

| 改动类型 | 是否推送远程仓库 | 原因 |
|---|---|---|
| C# Plugin / Custom API 代码 | ✅ 必须（用户说「提交/推送」后执行分支+PR） | DEV Assembly 必须用合并后 uat 编译（红线） |
| 语言包 `ms_languagefile_*.json` | ✅ 必须 | 仓库是语言包修改的唯一通道（红线 16） |
| 远程仓库已跟踪的其他文件 | ✅ 同上 | 随仓库发布 |
| **表单 JS / HTML WebResource（`mcs_*.js`、`mcs_*.html` 等）** | ✅ **需要（2026-08-15 起，且流程变更见 8.5.2）** | 2026-08-15 用户决策：我方模块 JS/HTML 全部补入仓库 `D365/SanyD365.D365WebResource/WebResource/mcs_/Scripts\|Htmls/Sales/CreditAssessment/`（分支 `uat-20260815-peter-webresource-creditassessment`，25 文件与 DEV1 逐字节一致），与其他团队惯例一致（Sales 320/Service 172/PC 108 均在仓）。注意该 csproj 为 SDK 风格隐式包含，新文件无需登记 csproj |
| 元数据 / 配置数据 | ❌ 不涉及代码仓库 | 走 Solution / 配置清单 |

- 完成汇报中如实说明是否涉及仓库推送；推送类改动在用户说「提交/推送」后执行，JS/HTML 同样适用（见上表）。
- 本地个人 GitHub 备份（origin=SanYLocalCode）的 commit 仍需用户说「提交」，但它与发布链路无关，不作为完成汇报的待办项。

#### 8.5.2 JS/HTML 修改标准流程（2026-08-15 用户明确，强制执行）

> **先推送，合并后拉取，再更新 DEV**。与旧流程（本地改 → 直接部署 DEV1）彻底切割：

```
本地改 JS/HTML
  → ① 本地验证（node --check / Node 仿真）
  → ② 同步到 tx-windows 仓库路径，建分支 commit + push（用户说「提交/推送」后执行）
  → ③ 用户合并 PR 到 uat
  → ④ tx-windows 拉取最新 uat
  → ⑤ 用仓库 uat 版文件部署 DEV1（deploy-webresource）并发布
  → ⑥ DEV 验证 → 看板登记 → 用户 n8n 发布 McsWebResource 到 UAT
```

- **禁止再直连部署 DEV1**（JS/HTML），DEV1 的 JS 必须来自合并后的 uat，与「Plugin 必须用合并后 uat 编译」「语言包唯一通道」同一原则。
- **🚨 公共文件追加 SOP（2026-08-17 新增，历史事故强制执行）**：修改 `ms_languagefile_*.json` 等公共文件前**必须先拉取仓库最新 uat 再追加**，禁止基于本地副本/旧版本追加（事故：未拉取直接追加推送，覆盖导致他人 key 丢失，恢复 commit `bf0c96b0ea`）。步骤：① `git checkout uat && git pull origin uat` → ② 追加前记录 key 总数、确认目标 key 不存在、抽查他人近期 key 在位 → ③ 纯追加 → ④ `git diff` 实锤仅新增行、删除行数为 0 → ⑤ 才允许推送。
- 部署前 diff 核对 DEV1 现版与仓库 uat 版差异仅限本次改动，防覆盖他人并行修改。
- UAT 发布路径不变：n8n 发布 `McsWebResource` Solution。

### 8.6 命名规范对照表

| 场景 | 本地命名 | 远程主代码命名 |
|------|----------|----------------|
| Plugin 类 | `Peter.Coface.CofaceDataSyncPlugin` | `SanyD365.D365Extension.Sales.Plugins.Coface.CofaceDataSyncPlugin` |
| 命名空间 | `namespace Peter.Coface` | `namespace SanyD365.D365Extension.Sales.Plugins.Coface` |
| 项目文件 | `Peter.Coface.csproj` | `D365.csproj`（合并到主项目） |
| Git 分支 | 不涉及 | `uat-日期-姓名-功能` |

### 8.7 注意事项

1. **业务逻辑是底线**：本地测试通过的逻辑，转移到远程后严禁修改。如果远程编译报错，只修编译问题（命名空间、引用、语法兼容），不改业务逻辑。
2. **.NET 版本必须是 4.6.2**：主项目统一使用 .NET Framework 4.6.2，本地开发可用 .NET 6/8 快速验证语法，但远程集成时必须确保代码在 4.6.2 下编译通过。
3. **先本地后远程**：不要在远程服务器上直接写业务逻辑代码，远程只做集成和编译。
4. **DEV 测试是准入条件（强制卡点）**：未经测试人确认通过的代码，严禁进入远程主代码。阶段三不通过，不得开始阶段四。
5. **编译通过后立即提交**：远程编译通过后尽快推分支、建 PR，避免代码在本地积压导致合并冲突。
6. **主分支永远是 `uat`**：个人分支基于 `uat` 创建，PR 目标也是 `uat`。

---

## 9. 实体/模块开发前核对流程（复杂模块强制）

> 适用于厂端授信、成交条件样板库等多实体复杂模块。每开始一个**新实体/新模块**的开发前，必须按以下流程执行，**不可直接写代码**。

### 9.1 重新读取 PRD 文档

- 定位并读取对应 PRD 文件（如 `Documents/BusinessAnalysis/厂端授信PRD.docx`）
- 若 PRD 为 `.docx`，优先请用户导出为可读文本，或在用户授权后使用工具提取内容
- 重点阅读与该实体相关的章节：字段定义、表单设计、业务规则、状态流转

### 9.2 字段定义一致性核对

将当前 D365 实体字段（或 `MetadataTool/Definitions/*.json`）与 PRD 表格**逐字段**对比：

| 核对项 | 要求 |
|--------|------|
| 字段逻辑名 | 必须与 PRD 完全一致，含大小写 |
| 中文显示名 | 必须与 PRD 一致 |
| 英文显示名 | 必须与 PRD 一致 |
| 字段类型 | String/Memo/Decimal/Money/Integer/DateTime/Picklist/Lookup/Boolean 等 |
| 长度 | 文本字段最大长度 |
| 必填 | 是否必填 |
| 默认值 | 系统默认或业务默认 |
| 选项集值 | PRD 中的业务值与 D365 选项集实际值映射 |

**发现任何不一致、笔误、缺字段、语义模糊时，必须第一时间向用户提出疑问，不得自行假设或按经验修正。**

### 9.3 业务逻辑核对

重新阅读 PRD 中该实体对应的业务逻辑，整理以下要点：

1. **触发时机**：Create / Update / 按钮点击 / 状态变更 / BPP 回调
2. **计算规则**：公式、取数来源、聚合方法、兜底逻辑
3. **状态流转**：有哪些状态值、允许的正向/反向流转、谁触发
4. **校验规则**：必填、唯一、范围、组合校验、阻断还是提示
5. **上下游依赖**：依赖哪些实体/字段/外部系统/配置数据
6. **异常处理**：数据缺失、外部接口失败、权限不足时的行为

### 9.4 输出开发思路

以清单或表格形式输出，**不直接写业务代码**：

| 项目 | 内容 |
|------|------|
| 本次目标 | 要实现的功能点 |
| 涉及实体 | 当前实体 + 依赖实体 |
| 涉及文件 | Plugin / JS / 工具命令 / 配置数据 |
| 关键算法 | 计算公式、匹配规则 |
| 状态/事件 | 触发的事件和阶段 |
| 风险点 | 数据缺失、权限、并发、外部依赖 |
| 待确认问题 | 发现的不一致或需要业务决策的点 |

### 9.5 用户确认

将开发思路提交用户确认：

> 我已核对 PRD，发现以下字段/逻辑需要确认：
> 1. ...
> 2. ...
> 开发思路如下：... 
> 确认无误后，我正式开始编码。

**必须得到用户明确确认后，才进入实际编码阶段。**

---

## 10. Bug 修复登记机制（禅道关联，2026-07-24 新增，每次修 Bug 必读）

> 完整规则见根目录 `AGENTS.md` 第 5 节。要点：

1. **先登记后动手**：修 Bug 前先在 `Documents/Tests/BugReports/禅道Bug修复记录.md` 登记禅道编号、标题、模块、日期、状态；AI 接到 Bug 修复指令必须先读该表确认已登记，未登记先补登记。
2. **全过程更新**：根因、改动文件、部署/验证状态记录在同一行；DEV 验证通过置「已修复待验证」，用户验证/UAT 发布后置「已关闭」。
3. **无禅道编号的 Bug** 编号列填「无」并备注来源。
4. **commit message 关联禅道编号**，如 `fix(tradestpayterm): 事业部带出及审批共享修复 (#1151)`。

---

*本文件记录 D365 开发规范与流程，跨项目可复用。*
*更新规则：发现新的通用陷阱、前端控件方案或开发流程变更时更新。*

---

## 10. 🚨 字段问题处理红线：只新建，不删除（2026-07-24 用户明确）

> **后续所有有问题的字段都不允许删除，只能新建。**

- **背景**：「删字段重建」曾导致生产导入失败（`80041A06`：同名字段类型不一致），D365 不允许通过导入改变已存在字段的类型/Lookup 目标。
- **规则**：字段类型错误、Lookup 目标实体错误、命名错误、精度/长度错误等一切字段问题，**一律新建字段（新架构名）替代，旧字段保留不删**。旧字段可从表单/视图移除、显示名加「(废弃)」标记，但**元数据中绝对保留**。
- **适用范围**：DEV / UAT / 生产全部环境；AI 开发、数据修复、Bug 修复全场景。
- **无例外**：如确需删除字段，必须获得用户逐字明确授权（此前 `delete-field` 的 YES 确认机制仅保留作历史代码，默认不推广使用）。

*示例：融资管理 `mcs_fsm_data.mcs_quote_id` 错关联标准 `quote` 实体（应关联配置报价自定义实体）→ 不删旧字段，新建新 Lookup 字段替代。*
