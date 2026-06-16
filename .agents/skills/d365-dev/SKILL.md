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
| **Plugin Assembly 版本号相同但内容不同** | 误以为 UAT/DEV 代码一致，实际 DLL 已不同步 | **查 `pluginassembly.modifiedon`，不要只看 `version`** |
| **"ISV code reduced the open transaction count"** | Plugin 吞掉了 OrganizationService 异常，破坏事务 | **确保 catch 后必须 rethrow；DEV 正常 UAT 报错先查版本偏差** |
| **用 RibbonDiff.xml 创建按钮** | 生成 Legacy Ribbon（只读，无法删除） | **必须用 C# AppActionDeployer 创建 Modern Command Bar** |

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

# 一键编译
ssh tx-windows "cd C:\\Projects\\D365 && nuget restore D365\\D365.sln && msbuild D365\\D365.sln /p:Configuration=Release"

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

# 编译 Release
msbuild D365\D365.sln /p:Configuration=Release /p:Platform="Any CPU"

# 或编译 Debug
msbuild D365\D365.sln /p:Configuration=Debug /p:Platform="Any CPU"
```

### 7.5 项目结构

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

### 7.6 VS Code Remote-SSH 开发

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

### 8.2 阶段二：DEV 环境注册

开发完成后，在 DEV 环境注册：

```bash
# 1. 编译本地代码
cd ~/Work/AIWorkSpace/SanYi/Code/Customizations/Plugins/CofaceIntegration
dotnet build

# 2. 用 MetadataTool 或 DeployTool 注册到 DEV1
#    - 注册 Plugin Assembly
#    - 注册 SdkMessageProcessingStep
#    - 发布实体
```

### 8.3 阶段三：DEV 测试验证（⚠️ 阻塞点）

> **必须等待测试人确认测试通过后，才能进入阶段四。**

| 事项 | 要求 |
|------|------|
| 测试人 | 由开发负责人或指定测试人员执行 |
| 测试内容 | 在 DEV1 前台创建测试数据，验证业务逻辑 |
| 通过标准 | 关联测试用例全部通过，无阻塞 Bug |
| 产出物 | 测试用例编号 + 执行结果 |

**流程：**
```
DEV 注册完成 → 通知测试人 → 测试人验证 → [通过] → 进入阶段四
                                    ↓
                                  [不通过] → 修复 → 重新注册 → 重新验证
```

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

# 6. 去 Azure DevOps 网页创建 PR → 合并到 uat
#    https://dev.azure.com/SanyGlobalCRM/D365/_git/D365
```

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

*本文件记录 D365 开发规范与流程，跨项目可复用。*
*更新规则：发现新的通用陷阱、前端控件方案或开发流程变更时更新。*
