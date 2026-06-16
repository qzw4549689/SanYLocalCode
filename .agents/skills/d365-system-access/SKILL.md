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
# 目标环境，默认 DEV1
export D365_URL="https://dev1.crm5.dynamics.com"

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

缓存位置：
- macOS: `~/.local/share/D365MetadataTool/msal_cache.dat`
- Windows: `%LocalAppData%\D365MetadataTool\msal_cache.dat`

### 4.4 切换 DEV / UAT

```bash
# DEV
export D365_URL="https://dev1.crm5.dynamics.com"

# UAT
export D365_URL="https://sany-uat.crm5.dynamics.com"
```

建议加到 `~/.zshrc`：

```bash
alias d365-dev='export D365_URL="https://dev1.crm5.dynamics.com"'
alias d365-uat='export D365_URL="https://sany-uat.crm5.dynamics.com"'
```

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
