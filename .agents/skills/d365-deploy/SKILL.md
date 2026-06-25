---
name: d365-deploy
description: D365 项目发布部署指南。适用于 Service 项目编译、D365 Plugin 注册、WebResource 部署、n8n 发布工具使用等场景。
---

# D365 发布部署指南

## 0. AI 行为红线（发布相关）

> **除非用户明确说出"提交"或"推送"二字，否则 AI 不得执行任何 `git add` / `git commit` / `git push` / `git merge` / `git reset --hard` 操作。**
>
> 数据修复、环境诊断、API 测试、记录查询等操作属于 D365 数据层，**不触碰 Git**。

### 0.1 McsPlugin 解决方案红线

> **`McsPlugin` 解决方案中永远只能放插件（Plugin）和 Plugin Step，不能放其他任何组件（实体、字段、WebResource、角色、工作流等）。**
>
> 违反此规则会导致 n8n Release Tool 发布 Plugin 时因解决方案依赖冲突而失败。

### 0.2 Git 推送约定

- 主集成分支是项目 Git 的 `uat`（Azure DevOps）
- 基于项目 `uat` 创建个人分支：`uat-<日期>-<姓名>-<功能简述>`
- **默认推送目标：项目 Git（Azure DevOps）**；只有用户明确说"推送到个人 Git/GitHub"时才备份到个人仓库
- **不要直接 push `uat` 分支，必须走 PR！**

---

## 1. 部署方式优先级

**所有系统更新（仅在 DEV 环境，UAT 严禁直接操作）优先使用 C# 直连 Dataverse 部署，尽量避免 Solution 导入。**

| 方式 | 优先级 | 适用场景 | 工具位置 |
|------|--------|---------|---------|
| **C# DeployTool** | ⭐ P0 | WebResource更新、App Action按钮、数据修复、批量操作、发布 | `Code/Tools/DeployTool/` |
| **MetadataTool CLI** | P1 | 实体/字段/表单/视图/Plugin 部署 | `Code/Tools/MetadataTool/` |
| **n8n Release Tool** | P1 | Azure 代码发布（ClientAPI / MessageHandler / ExtensionAPI / InnerAPI） | 网页端 |
| **PAC CLI** | P2 | Solution 导出（备份用） | `pac solution export` |
| **D365 界面** | P3 | 权限设置 | make.powerapps.com |

**🚫 禁止事项：**
- **未经用户明确同意，不得使用 `pac solution import` 导入 Solution**
- Solution 导入慢（通常 5-60 分钟），且会阻塞整个环境的发布操作
- 一旦开始无法取消，所有其他部署操作都必须等待
- **🚨 严禁在 UAT/生产环境直接操作元数据（CreateAttribute/DeleteAttribute/PublishAll 等）**

**发布范围规则（AI 执行时）：**
| 场景 | 发布范围 | 是否需要用户批准 |
|------|---------|----------------|
| 更新单个实体/WebResource | 只发布该实体 | 否 |
| 更新多个相关实体 | 发布涉及的实体/WebResource 列表 | 否 |
| 全局发布所有自定义项 | `PublishAllXmlRequest` | **🚫 AI 禁止执行，必须向用户请求，由用户手动执行** |

> **硬性规定**：AI 在任何情况下都不得调用 `PublishAllXmlRequest`/`PublishAllXml`。即使多个组件需要发布，也应使用 `PublishXmlRequest` 列出具体实体/WebResource；如确需全局发布，必须向用户说明理由，由用户在工具外手动执行。

```csharp
// ✅ 正确：只发布当前操作的实体
var request = new PublishXmlRequest
{
    ParameterXml = @"<importexportxml><entities><entity>mcs_credit_record</entity></entities></importexportxml>"
};
service.Execute(request);

// ✅ 正确：一次发布多个明确的实体/WebResource
var request = new PublishXmlRequest
{
    ParameterXml = @"<importexportxml><entities><entity>mcs_credit_record</entity><entity>mcs_trade_stpayterm</entity></entities><webresources><webresource>mcs_trade_stpayterm.js</webresource></webresources></importexportxml>"
};
service.Execute(request);

// ❌ 错误：AI 禁止全局 PublishAll（会阻塞整个环境）
service.Execute(new PublishAllXmlRequest());
```

---

## 2. 项目结构区分

| 解决方案 | 路径 | 框架 | 部署目标 |
|---|---|---|---|
| `D365.sln` | `C:\Projects\D365\D365\D365.sln` | .NET Framework 4.6.2 | D365 插件程序集 |
| `Service.sln` | `C:\Projects\D365\Service\Service.sln` | .NET 8.0 | Azure 独立服务 |

**关键项目对应关系：**
| 项目 | n8n Azure代码选项 | 用途 |
|---|---|---|
| `SanyD365.D365ClientApi` | `ClientAPI` | Web API，不消费消息 |
| `SanyD365.MessageHandler` | `Messagehandler` | **消息队列消费者，BPP 流程由它处理** |
| `SanyD365.D365ExtensionApi` | `ExtensionAPI` | D365 Custom API（如 `mcs_bppstartapi`） |
| `SanyD365.InnerApi` | `InnerAPI` | 内部 API |
| `SanyD365.CommonMessageHandler` | `CommonMessageHandle` | 公共消息处理 |
| `SanyD365.Main` | 无（被引用） | 核心业务逻辑库 |

**BPP 审批流程部署要点：**
- Plugin 端：`D365.sln` 中的 `SanyD365.D365Extension.Sales`（`CreditRecordBppIntegrationPlugin`）
- Consumer 端：`Service.sln` 中的 `SanyD365.Main`（`BPPService` + `BPPHandlerServiceForCreditRecord`）
- **Consumer 由 `SanyD365.MessageHandler` 宿主运行**，n8n 中勾选 `Messagehandler`
- `SanyD365.Main` 同时被 `ClientAPI` 和 `Messagehandler` 引用，建议两者一起部署保持版本一致

---

## 3. 远程服务器编译（tx-windows）

> 服务器：`122.51.232.70`，路径：`C:\Projects\D365`。完整连接信息见 `/skill:d365-dev`。

### 3.1 Service 项目（.NET 8）

```powershell
cd C:\Projects\D365\Service\SanyD365.Main
dotnet build SanyD365.Main.csproj -c Release
```

输出：
```
C:\Projects\D365\Service\SanyD365.Main\bin\Release\net8.0\SanyD365.Main.dll
```

### 3.2 D365 Plugin 项目（.NET 4.6.2）

> **⚠️ 不要直接编译整个 `D365.sln`**
>
> `D365.sln` 包含大量 Test 项目，这些项目依赖 `Secret.json` 等本地配置文件，直接编译会导致 `MSB3030 无法复制文件 Secret.json` 错误。
> 实际发布时**只需编译目标 Plugin 项目**（如 `SanyD365.D365Extension.Sales`）。

```powershell
cd C:\Projects\D365
nuget restore D365\D365.sln

# 只编译目标项目，避免 Test 项目因缺少 Secret.json 失败
msbuild D365\D365.sln /p:Configuration=Release /p:Platform="Any CPU" /t:SanyD365_D365Extension_Sales
```

输出示例：
```
C:\Projects\D365\D365\SanyD365.D365Extension.Sales\bin\Release\SanyD365.D365Extension.Sales.dll
```

---

## 4. n8n Release Tool 使用

打开 n8n 部署工具网页，填写：

- **实体**：不填或填相关实体包名
- **解决方案**：按需选择（Plugin 更新选 `McsPlugin`）
- **Azure代码**：
  - 修改了 BPP Consumer 逻辑 → **勾选 `Messagehandler`**
  - 修改了 Web API 接口 → 勾选 `ClientAPI`
  - 修改了 Custom API → 勾选 `ExtensionAPI`
- **MessageHandler分组**：BPP 消息分组（不填则默认全部组，或问同事具体分组号）

**常见组合：**
| 修改内容 | n8n 勾选 |
|---|---|
| BPP Handler / BPP Service（`SanyD365.Main`） | `Messagehandler`（+ `ClientAPI` 保持版本一致） |
| Plugin（`CreditRecordBppIntegrationPlugin` 等） | `McsPlugin` |
| JS WebResource | `McsWebResource` |

---

## 5. Plugin 同步检查

跨环境对比 Plugin 是否同步时，**不要只看 `version`**（可能都是 `1.0.0.0`），要对比 `pluginassembly.modifiedon`：

```bash
cd Code/Tools/MetadataTool

# 查询某 Plugin 的注册步骤
dotnet run query-plugin-steps <类名>

# 查询某前缀命名空间下的 Plugin
dotnet run query-plugin-namespace <前缀>

# 查询程序集版本与修改时间
dotnet run query-assembly-version <名称>
```

---

## 6. DEV 环境测试-UAT发布流程

> 适用于本地代码与远程服务器代码存在结构/命名空间差异，需要先在 DEV 环境验证 Plugin 的场景。

### 6.1 本地编译 Plugin

在本地开发环境编译 D365 Plugin 项目（.NET Framework 4.6.2）：

```powershell
# 打开 Visual Studio 或使用 msbuild
msbuild Code/Customizations/Plugins/SanyD365.Plugins.csproj /p:Configuration=Release /p:Platform="Any CPU"
```

### 6.2 部署到 DEV 环境测试

使用 MetadataTool 或 Plugin Registration Tool 将本地编译的 Plugin 注册到 DEV 环境：

```bash
cd Code/Tools/MetadataTool
export D365_URL="https://dev1.crm5.dynamics.com"

# 注册/更新 Plugin Assembly
dotnet run register-plugin <dll路径> <Plugin类名>
```

### 6.3 DEV 测试验证

在 DEV 环境完成功能验证，确认：

- Plugin Step 正常触发
- 业务逻辑符合预期
- 无异常报错

### 6.4 测试完成后清理本地临时 Assembly

如果本地代码结构与远程不一致（例如本地 Assembly 名为 `SanyD365.Plugins.CofaceIntegration`，远程为 `SanyD365.D365Extension.Sales`），DEV 测试通过后需要把本地临时注册的 Assembly 从 DEV 环境移除：

1. 在 DEV 环境中 **Unregister** 本地临时 Plugin 的所有 Step
2. 删除本地临时 Plugin Assembly
3. 确认 DEV 环境中不再保留该临时 Assembly

```bash
cd Code/Tools/MetadataTool
export D365_URL="https://dev1.crm5.dynamics.com"

# 注销并删除指定 Assembly
dotnet run unregister-plugin <Assembly名称>
```

### 6.5 远程服务器集成

DEV 测试通过后，将本地代码转移到远程服务器主项目：

1. **将本地代码同步到远程服务器**（`tx-windows`）的 `C:\Projects\D365\D365` 目录
2. **按主项目规范改写命名空间、引用、csproj**（参见 `/skill:d365-dev` 第 8.4 节）
3. **在远程服务器编译目标项目**（如 `SanyD365.D365Extension.Sales`），确认无编译错误
4. **编译通过后，在远程项目仓库创建个人分支并提交推送**，**不要先拉取 `uat` 再编译**

```powershell
ssh tx-windows

cd C:\Projects\D365

# 确认当前在 uat 分支（远程仓库默认分支）
git status

# 创建个人开发分支
git checkout -b uat-<日期>-<姓名>-<功能简述>

# 添加修改的文件并提交
git add .
git commit -m "<功能简述>"

# 推送到项目 Git（Azure DevOps）
git push -u origin uat-<日期>-<姓名>-<功能简述>
```

### 6.6 Git 工作流 / 代码归并

> 📌 Git 推送核心约定见本文 **0.2 Git 推送约定**。

远程编译通过后，按以下顺序走 Git PR 流程：

1. **在远程服务器创建个人开发分支并 push**（参见 6.5 节）
2. **用户在 Azure DevOps 网页 Create PR → 合并到 `uat`**
   - 仓库地址：`https://dev.azure.com/SanyGlobalCRM/D365/_git/D365`
3. **用户通知 AI "PR 已合并完成"**
4. **AI 在远程服务器拉取已合并的 `uat` 最新代码，重新编译目标项目**
5. **将重新编译后的 DLL 传回本地，更新 DEV Assembly**

```powershell
# 步骤 1：远程服务器推送分支后，用户手动在 Azure DevOps 创建 PR 并合并

# 步骤 4：用户通知合并完成后，在远程服务器执行
cd C:\Projects\D365
git checkout uat
git pull origin uat

# 重新编译目标项目（不要全编译 D365.sln）
nuget restore D365\D365.sln
msbuild D365\SanyD365.D365Extension.Sales\SanyD365.D365Extension.Sales.csproj /p:Configuration=Release /p:Platform=AnyCPU /verbosity:minimal
```

**⚠️ 不要直接 push `uat` 分支，必须走 PR！**
**⚠️ 不要默认推送到个人 GitHub 仓库，除非用户明确说"备份到个人 Git"或"推送到 GitHub"。**
**⚠️ PR 合并前不要拉取 `uat` 重新编译；必须等用户确认合并完成后再执行步骤 4。**

### 6.7 UAT 实体/元数据发布

> **⚠️ 本步骤必须由用户手动操作，AI 不得擅自执行。**

如果本次修改涉及**新增实体、新增字段、选项集变更、表单/视图调整**等元数据变更，在发布 Plugin 到 UAT 之前，必须先将实体/元数据发布到 UAT。否则 Plugin 在 UAT 访问不存在的实体或字段时会报错。

执行方式：通过 **n8n Release Tool** 发布 `MCSPlugin` Solution（仅发布元数据部分）：

- 打开 n8n 部署工具：`https://n8n.sany.com.cn/form/0923e9fb-86fa-48d7-8d94-3d3e360ab7dd`
- 账号：`caoy815@sany.com.cn` / `Sany318318`
- **解决方案**：勾选 `McsPlugin`
- **Azure代码**：不勾选（本次仅发布实体/元数据，不发布 Plugin）
- **数据中心**：选择 UAT 对应的数据中心
- 提交发布后等待 UAT 元数据部署完成

> 💡 **顺序提醒**：UAT 实体发布 → 本地注册 DEV Assembly（验证 Plugin）→ UAT Plugin 发布。不要先发布 Plugin 再补发实体。

### 6.8 本地注册 DEV Assembly

> **必须在用户确认 uat PR 已合并后，再从远程服务器拉取最新 `uat` 重新编译，最后用该 DLL 更新 DEV。**

**🚨 Assembly 差异红线（更新前必读）：**

用新 DLL 更新 DEV 的现有 Assembly 时，如果 D365 报错类似：

```
PluginType [xxx] not found in PluginAssembly [xxx] which has a total of [N] plugin/workflow activity types.
```

说明 **DEV 当前注册的 Assembly 与新 DLL 的 PluginType 集合不一致**（DEV 上可能包含新 DLL 中没有的 Type）。

**必须立即停止，第一时间通知用户，严禁擅自处理。** 因为：

- 这些差异 Type 是别人写的代码，不能随意删除、合并或 rebase 到当前分支；
- 擅自合并他人代码会导致分支污染、UAT 发布风险、PR 混乱；
- 需要由用户确认 DEV 当前 Assembly 的来源分支（main / dev / 其他），再决定如何对齐。

正确做法：把差异 Type 列表、DEV 当前 Assembly 的 `modifiedon`、远程编译分支告诉用户，等待决策。

**更新 DEV Assembly 流程：**

1. 收到用户"PR 已合并"通知后，在远程服务器拉取已合并后的 `uat` 最新代码
2. 重新编译目标项目（如 `SanyD365.D365Extension.Sales`），**不要全编译 `D365.sln`**
3. 将远程服务器编译出的最新 DLL 传回本地
4. 使用 MetadataTool 在 DEV 环境注册/更新该 Assembly
5. 如 Step 不存在则注册新 Step
6. 将 Plugin Assembly / Step 加入 DEV 的 `MCSPlugin` Solution
7. 在 DEV 环境做最终验证

```bash
# 示例：从远程服务器复制 DLL
scp 'tx-windows:/C:/Projects/D365/D365/SanyD365.D365Extension.Sales/bin/Release/SanyD365.D365Extension.Sales.dll' /tmp/

# 注册/更新 Assembly + Step
cd Code/Tools/MetadataTool
export D365_URL="https://dev1.crm5.dynamics.com"
dotnet run register-plugin-update /tmp/SanyD365.D365Extension.Sales.dll \
  SanyD365.D365Extension.Sales.Plugins.CofaceIntegration.CofaceIntegrationDataSyncPlugin \
  mcs_credit_record mcs_status
```

### 6.9 UAT Plugin 发布

> **⚠️ 本步骤必须由用户手动操作，AI 不得擅自执行。**

DEV Assembly 更新完成且验证通过后，由用户通过 **n8n Release Tool** 将 Plugin 发布到 UAT：

- 打开 n8n 部署工具：`https://n8n.sany.com.cn/form/0923e9fb-86fa-48d7-8d94-3d3e360ab7dd`
- 账号：`caoy815@sany.com.cn` / `Sany318318`
- **解决方案**：勾选 `McsPlugin`
- **Azure代码**：不勾选（本次仅更新 Plugin，实体已在 6.7 发布）
- **数据中心**：选择 UAT 对应的数据中心
- 提交发布后等待 UAT 部署完成

AI 在此步骤的职责：整理好 n8n 需要填写的信息，告知用户后停止，等待用户操作。

---

*本文件记录 D365 发布部署指南，跨项目可复用。*
*更新规则：发现新的部署流程或工具变化时更新。*
