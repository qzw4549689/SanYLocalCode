---
name: d365-system-access
description: D365 项目系统访问与工具认证指南。包含 Git 仓库、飞书文档、DEV/UAT 环境地址、n8n 发布工具账号，以及 MetadataTool/DeployTool 的认证方式。
---

# D365 系统访问与工具认证

> 本 skill 替代原根目录 `SystemAccess.md`，作为项目访问地址和工具认证的唯一来源。

---

## 1. 项目资源

| 项目 | 内容 |
|------|------|
| Git 仓库 | https://dev.azure.com/SanyGlobalCRM/D365/_git/D365 |
| Git 仓库合并代码 | https://dev.azure.com/SanyGlobalCRM/D365 |
| 飞书共享文件夹 | https://rs2flu7c17.work.sany.com.cn/docx/doxk5UNuXCrBDzHaq62Jvi2dfnh |
| 项目计划表 | https://rs2flu7c17.work.sany.com.cn/sheets/shtk5ceQoAE06MveQV1lFezSqCc |
| DEV发布UAT工具 | https://n8n.sany.com.cn/form/0923e9fb-86fa-48d7-8d94-3d3e360ab7dd |
| DEV发布UAT工具账号 | `caoy815@sany.com.cn` / `Sany318318` |
| DEV发布UAT工具 Host | `47.84.69.169  n8n.sany.com.cn`（需配置 hosts） |

### 1.1 配置 n8n Hosts

在本地 `/etc/hosts`（macOS/Linux）或 `C:\Windows\System32\drivers\etc\hosts`（Windows）中添加：

```
47.84.69.169  n8n.sany.com.cn
```

---

## 2. DEV 环境

| 项目 | 内容 |
|------|------|
| 名称 | DEV1 |
| 前台地址 | https://dev1.crm5.dynamics.com/main.aspx?forceUCI=1&pagetype=apps |
| 管理员后台 | https://dev1.crm5.dynamics.com/tools/Solution/home_solution.aspx?etc=7100 |
| 信用额度申请后台 | https://dev1.crm5.dynamics.com/main.aspx?appid=4268989a-aede-45e0-93ec-f07b02f3e383&pagetype=entitylist&etn=mcs_nonlc_credit_limit_application&viewid=a217fd3e-3c21-407e-b061-b4719947cdb4&viewType=1039 |
| 用户名 | `gw_qiuzw@sanyglobal.onmicrosoft.com` |

---

## 3. UAT 环境

| 项目 | 内容 |
|------|------|
| 名称 | SanyUAT |
| 前台地址 | https://sany-uat.crm5.dynamics.com/main.aspx |
| 管理员后台 | https://sany-uat.crm5.dynamics.com/tools/Solution/home_solution.aspx?etc=7100 |
| 用户名 | `gw_qiuzw@sanyglobal.onmicrosoft.com` |

### 3.1 UAT 测试账号

> 用于权限/角色测试，密码默认尝试 `CRM#2025`、`CRM#2024`、`CRM#2026`，均不对则联系李智（liz273）重置。

| 角色 | 账号 |
|------|------|
| 营销代表 | `UATUser16@sanyglobal.onmicrosoft.com` |
| 区域营管 | `UATUser70@sanyglobal.onmicrosoft.com` |
| 营销代表 + 区域营管 | `UATUser08@sanyglobal.onmicrosoft.com` |
| 总部营管 | `UATUser14@sanyglobal.onmicrosoft.com` |
| 子总（风控权限足够） | `UATUser06@sanyglobal.onmicrosoft.com` |
| 服务工程师 | `UATUser60@sanyglobal.onmicrosoft.com`<br>`UATUser32@sanyglobal.onmicrosoft.com` |
| 服务部长 | `UATUser52@sanyglobal.onmicrosoft.com` |

---

## 4. 本地工具认证方式

`MetadataTool` 和 `DeployTool` 连接 D365 时，按以下优先级选择认证方式：

| 优先级 | 认证方式 | 适用场景 | 是否需要配置环境变量 |
|--------|---------|---------|---------------------|
| 1 | **ClientSecret** | 已创建 D365 Application User，真正无感 | `D365_CLIENTSECRET` |
| 2 | **OAuth 用户名密码** | 账号未开 MFA（本项目账号有 MFA，不推荐） | `D365_USERNAME` + `D365_PASSWORD` |
| 3 | **Device Code Flow + Token Cache** | 默认方式，有 MFA 也能用，第一次需浏览器登录 | 无 |

### 4.1 DeployTool 命令

`DeployTool` 需要传入命令参数，不再一启动就执行所有操作：

```bash
cd Code/Tools/DeployTool

# 只更新 WebResource
dotnet run webresource

# 只部署按钮
dotnet run appactions

# 只部署 Plugin
dotnet run coface
dotnet run creditscore
dotnet run bpp

# 更新表单布局
dotnet run formlayout

# 发布实体
dotnet run publish

# 执行全部（原来的默认行为）
dotnet run all
```

完整可用命令：

| 命令 | 说明 |
|------|------|
| `webresource` | 更新 `mcs_credit_record.js` WebResource |
| `profile` | 更新信用画像 WebResource |
| `appactions` / `buttons` | 部署 Modern Command Bar 按钮 |
| `coface` | 部署 Coface Plugin |
| `creditscore` | 部署 CreditScore Plugin |
| `bpp` | 部署 BPP Integration Plugin |
| `formlayout` | 更新表单布局（添加 BPP 字段） |
| `publish` | 发布 `mcs_credit_record` 实体 |
| `publish-webresource` | 发布画像 WebResource |
| `all` | 执行上述所有操作 |

### 4.2 环境变量说明

```bash
# 目标环境（二选一，优先级 D365_URL > D365_ENV，默认 dev1）
export D365_ENV="uat"                                  # 命名环境：dev1/uat/pre/prod，清单见 4.4
export D365_URL="https://dev1.crm5.dynamics.com"       # 显式地址（优先级最高）

# App ID，默认是微软示例 App，一般不用改
export D365_APPID="51f81489-12ee-4a9e-aaae-a2591f45987d"

# 租户 ID，可选
export D365_TENANTID=""

# ClientSecret 方式（需要 D365 Application User）
export D365_CLIENTSECRET="你的ClientSecret"

# 用户名密码方式（本项目因 MFA 通常不可用）
export D365_USERNAME="gw_qiuzw@sanyglobal.onmicrosoft.com"
export D365_PASSWORD="你的密码"
```

### 4.3 默认方式：Device Code Flow

不配置任何认证环境变量时，工具自动走 Device Code Flow：

1. 运行命令，例如：
   ```bash
   cd Code/Tools/MetadataTool
   dotnet run list-fields mcs_credit_record
   ```

2. 命令行显示：
   ```
   认证方式: Device Code Flow（第一次需要浏览器登录）
   To sign in, use a web browser to open the page https://login.microsoft.com/device and enter the code XXXXXXXX to authenticate.
   ```

3. 浏览器打开 https://login.microsoft.com/device，输入 code
4. 用 `gw_qiuzw@sanyglobal.onmicrosoft.com` 登录
5. 手机上点 MFA 批准
6. 登录成功后，token 自动缓存到本地，后续运行不再弹窗

缓存位置（**按环境隔离**，每个环境独立缓存文件，互不顶号，2026-08-28 改造）：
- macOS: `~/.local/share/D365MetadataTool/msal_cache_<环境host>.dat`（如 `msal_cache_dev1.crm5.dynamics.com.dat`）
- Windows: `%LocalAppData%\D365MetadataTool\msal_cache_<环境host>.dat`
- 首次连接某环境需设备码登录一次，之后该环境缓存独立有效；其他会话/账号登录别的环境不再影响本缓存

### 4.4 环境清单与切换（2026-08-28 改造）

所有环境命名保存在 `Code/Tools/environments.json`（单一来源，随工具编译自动复制到输出目录）：

| 名称 | 地址 | 说明 |
|------|------|------|
| `dev1` | https://dev1.crm5.dynamics.com | DEV1 开发环境（**默认环境**） |
| `uat` | https://sany-uat.crm5.dynamics.com | UAT 环境 |
| `pre` | https://pre.crm5.dynamics.com | PRE 生产镜像（勿动，仅生产 bug 修复核对） |
| `prod` | https://sany.crm5.dynamics.com | 生产-新加坡数据中心：亚太大区（谨慎） |
| `prod-eu` | https://sany.crm4.dynamics.com | 生产-西欧数据中心：欧洲/非洲/中东北非大区（谨慎） |
| `prod-na` | https://sany.crm.dynamics.com | 生产-美东数据中心：北美/拉美大区（谨慎） |

**切换优先级**：`D365_URL`（显式地址）> `D365_ENV`（命名环境）> 默认 `dev1`。⚠️ 默认永远是 dev1，绝不会默认指向生产；D365_ENV 未配置或地址为空时告警并回退 dev1。

```bash
# 命名环境切换（推荐）
export D365_ENV=uat      # dev1 / uat / pre / prod

# 显式地址（优先级最高，兼容旧用法）
export D365_URL="https://sany-uat.crm5.dynamics.com"
```

建议加到 `~/.zshrc`：

```bash
alias d365-dev='unset D365_URL; export D365_ENV=dev1'
alias d365-uat='unset D365_URL; export D365_ENV=uat'
alias d365-pre='unset D365_URL; export D365_ENV=pre'
alias d365-prod='unset D365_URL; export D365_ENV=prod'        # 新加坡/亚太
alias d365-prod-eu='unset D365_URL; export D365_ENV=prod-eu'  # 西欧
alias d365-prod-na='unset D365_URL; export D365_ENV=prod-na'  # 美东
```

> 三个生产数据中心组织不同、数据独立，连接前务必确认目标大区；`prod`（crm5 新加坡）已预存 pans14 登录态，`prod-eu`/`prod-na` 首次连接需设备码登录一次（账号以用户确认为准）。

#### 4.4.1 登录与切换规则（所有会话必须遵守）

1. **切换唯一入口**：`export D365_ENV=<环境名>`；需临时指向未登记地址才用 `D365_URL`。运行命令前先确认工具启动行打印的「目标环境」符合预期，**涉及写操作前必须再核一次**。
2. **每个环境独立 token 缓存**（`msal_cache_<host>.dat`），各环境登录态互不影响；某环境提示设备码登录时，只需完成该环境一次登录。
3. **登录账号按环境区分**：

   | 环境 | 登录账号 |
   |------|---------|
   | dev1 / uat / pre | `gw_qiuzw@sanyglobal.onmicrosoft.com` |
   | prod / prod-eu / prod-na（生产各数据中心） | 生产专用账号（pans14），**严禁用 gw_qiuzw 登生产** |
   | prod（生产只读核对专用通道，2026-09-01 新增） | `gw_zhangf68@sanyglobal.onmicrosoft.com`（Frank-张烽）——OAuth 用户名密码直连、**无 MFA**（比 pans14 设备码缓存更稳，缓存过期时的首选）；**密码不落盘**，用时向用户索取并以 `D365_USERNAME`/`D365_PASSWORD` env 变量临时传入，用完即焚；**仅限只读核对**（发版前生产基线对比 / 发后生产实证），具体核对策略见 `/skill:d365-deploy` 第 5.5 节。密码（2026-09-06 用户明确授权记录）：`Kpmg#123!!!`；⚠️ 本仓库会同步 GitHub 备份，如改密需同步更新此处。**权限实测（2026-09-06）**：Frank 有 `prvWriteappaction`（可停用 App Action）与 ms_systemconfiguration 写权限；pans14 **无** `prvWriteappaction`、无 Delete 权限。**🚨 逐步请示铁律（2026-09-01 用户明确，强制执行）：该账号在生产环境的任何使用——无论查询还是其他动作——都必须先询问用户，每一小步动作都要单独请示，获批准后才可执行，严禁一次性连发多个查询或自作主张跳过请示** |

4. **预存登录态**：新会话如需确认某环境已登录，运行 `cd Code/Tools/MetadataTool && D365_ENV=<环境> dotnet bin/Debug/net10.0/D365MetadataTool.dll get-token`，显示「✅ 使用缓存的 token 登录」即已登录；弹设备码则按提示完成登录。
5. **生产环境额外红线**：连生产只做指令内的只读/授权操作，导出导入包、改配置等必须先获用户明确授权（同 AGENTS.md 协作原则）。

### 4.5 获取 Application User（ClientSecret 方式）

如果希望完全无感，需要客户管理员在 D365 中创建 Application User：

1. 在 Azure 中注册 App，记录：
   - Application (client) ID
   - Directory (tenant) ID
   - Client Secret

2. 在 D365 中进入 **Settings > Security > Users**（视图切换为 **Application Users**）

3. 新建 Application User，填写 Azure 的 Application ID

4. 分配安全角色（如 **System Administrator** 或自定义角色）

5. 配置环境变量：
   ```bash
   export D365_CLIENTSECRET="你的ClientSecret"
   export D365_TENANTID="你的TenantId"
   ```

---

*本文件记录 D365 项目系统访问地址和工具认证方式，信息变更时同步更新。*
*原根目录 `SystemAccess.md` 已废弃，统一使用本 skill。*
