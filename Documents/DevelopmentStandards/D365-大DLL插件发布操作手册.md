# D365 大 DLL Plugin 发布操作手册

> 经验来源：2026-06-16 在 DEV1 发布 `SanyD365.D365Extension.Sales`（8MB）并注册 `CustomerMasterDataCreditValidationPlugin` Step 的实践。
>
> 适用场景：Plugin Assembly DLL 较大（>5MB），通过 MetadataTool 直接上传反复超时。

---

## 1. 核心问题

大 DLL 发布时常见错误：

```
错误: 请求通道在 00:04:00 以后尝试发送超时。
内部错误: 对“https://dev1.crm5.dynamics.com/XRMServices/2011/Organization.svc/web?...”的 HTTP 请求已超过分配的超时 00:04:00。
```

或：

```
An error occurred while sending the request
```

原因：
- DLL 8MB → Base64 后约 10.7MB，单次 SOAP 请求体过大
- `Microsoft.PowerPlatform.Dataverse.Client.ServiceClient` 默认超时仅 4 分钟
- 网络抖动或 D365 后台负载高时，大文件上传/解析耗时远超默认值

---

## 2. 解决思路

### 2.1 增大 ServiceClient 超时

MetadataTool 使用 Device Code Flow 时，默认未设置 `MaxConnectionTimeout`，会落到 SDK 默认的 4 分钟。**必须显式设置为 10 分钟**。

修改位置：

```
Code/Tools/D365ToolCommon/Connection/D365ConnectionFactory.cs
```

在 `CreateWithDeviceCodeAsync` 方法中，创建 `ServiceClient` 之前设置静态超时：

```csharp
var serviceUri = new Uri(url);
// Device Code Flow 创建的 ServiceClient 默认超时较短，上传大 Assembly 时需要延长
ServiceClient.MaxConnectionTimeout = TimeSpan.FromMinutes(10);
return new ServiceClient(
    serviceUri,
    _ => Task.FromResult(result.AccessToken),
    true,
    null);
```

> 注意：`ServiceClient.MaxConnectionTimeout` 是静态属性，只需设置一次。

### 2.2 使用 `update-assembly` 单独更新 DLL

MetadataTool 已提供独立命令，只更新 `pluginassembly.content`，不注册 Step：

```bash
cd Code/Tools/MetadataTool
dotnet run update-assembly <DLL完整路径>
```

这样可以把"上传大 DLL"和"注册 Step"两件事解耦：
- `update-assembly` 专注上传 DLL，失败后单独重试即可
- `register-plugin-advanced` / `register-step-only` 专注注册 Step，不再重复上传 DLL

### 2.3 后台执行 + 多次重试

大 DLL 更新耗时 3-10 分钟不等，建议用后台任务执行，避免前台超时中断。若失败，等 1-2 分钟再重试，通常能成功。

---

## 3. 完整发布流程

### 3.1 准备工作

1. **合并代码到 `uat` 分支**
   - 本地完成开发后，通过 PR 合并到 `uat`
   - 确保远程主项目（如 `tx-windows`）已拉取最新 `uat`

2. **远程编译主项目**
   - 使用 `Code/Tools/sync-plugin-to-remote.py` 同步本地 Plugin 代码到远程主项目
   - 在远程主项目执行 Release 编译：
     ```powershell
     msbuild SanyD365.D365Extension.Sales.csproj /p:Configuration=Release
     ```
   - 编译成功后，将 DLL 拉回本地备用路径，例如：
     ```bash
     scp administrator@122.51.232.70:C:/Projects/D365/D365/SanyD365.D365Extension.Sales/bin/Release/SanyD365.D365Extension.Sales.dll /tmp/
     ```

3. **确认超时配置已生效**
   - 检查 `D365ToolCommon/Connection/D365ConnectionFactory.cs` 中 Device Code Flow 的 `MaxConnectionTimeout` 是否已设为 10 分钟
   - 重新编译 MetadataTool：
     ```bash
     cd Code/Tools/MetadataTool
     dotnet build -c Release
     ```

### 3.2 更新 Assembly

```bash
cd Code/Tools/MetadataTool
dotnet bin/Release/net10.0/D365MetadataTool.dll update-assembly /tmp/SanyD365.D365Extension.Sales.dll
```

预期输出：

```
更新Assembly: SanyD365.D365Extension.Sales
DLL大小: 7979 KB
✓ Assembly已更新 (ID: 9d6ff315-8c03-4d51-b641-ebeccf9e98b0)
```

**如果失败：**
- 检查是否有人正在 D365 中执行 **Publish All**（会锁环境）
- 等待 1-2 分钟后重试
- 如仍然超时，检查网络或换到与 D365 网络更稳定的环境（如远程编译服务器）执行

### 3.3 注册 Plugin Step

如果新增 Plugin Type，第一次必须用 `register-plugin-advanced`（会同时更新 Assembly、创建 Type、注册第一个 Step）：

```bash
dotnet bin/Release/net10.0/D365MetadataTool.dll register-plugin-advanced \
  /tmp/SanyD365.D365Extension.Sales.dll \
  SanyD365.D365Extension.Sales.Plugins.Account.CustomerMasterDataCreditValidationPlugin \
  mcs_customermasterdata \
  Create \
  10
```

同一 Plugin Type 的后续 Step 可以用 `register-step-only`（不再上传 DLL）：

```bash
dotnet bin/Release/net10.0/D365MetadataTool.dll register-step-only \
  SanyD365.D365Extension.Sales.Plugins.Account.CustomerMasterDataCreditValidationPlugin \
  mcs_customermasterdata \
  Update \
  10
```

> 若 Type 已存在但 Step 未创建，两个命令都会正常工作；`register-plugin-advanced` 会跳过已存在的 Type，只创建 Step。

### 3.4 验证注册结果

```bash
dotnet bin/Release/net10.0/D365MetadataTool.dll query-plugin-steps \
  SanyD365.D365Extension.Sales.Plugins.Account.CustomerMasterDataCreditValidationPlugin
```

预期看到所有 Step 状态为 `✅ 启用`。

---

## 4. 关键命令速查

| 命令 | 用途 | 是否上传 DLL |
|---|---|---|
| `update-assembly <dll>` | 仅更新 Assembly 内容 | ✅ |
| `register-plugin-advanced <dll> <class> <entity> <msg> <stage> [filter]` | 更新 Assembly + 创建 Type + 注册 Step | ✅ |
| `register-step-only <class> <entity> <msg> <stage> [filter]` | 仅注册/更新 Step | ❌ |
| `query-plugin-steps <class>` | 查询某类的所有 Step | ❌ |

阶段数值：

| 阶段 | 数值 |
|---|---|
| PreValidation | 10 |
| PreOperation | 20 |
| PostOperation | 40 |

---

## 5. 常见失败与处理

| 现象 | 原因 | 处理 |
|---|---|---|
| `请求通道在 00:04:00 以后尝试发送超时` | Device Code Flow 默认 4 分钟超时 | 修改 `D365ConnectionFactory.cs` 设置 `ServiceClient.MaxConnectionTimeout = TimeSpan.FromMinutes(10)` |
| `An error occurred while sending the request` | 网络瞬断或 D365 正忙 | 等待 1-2 分钟重试；换网络更稳定的环境 |
| Assembly 更新卡住 5 分钟以上 | 可能有人正在 Publish All | 等 Publish 完成后再试 |
| `未找到 Plugin Type` | Step 注册时 Type 尚未创建 | 先用 `register-plugin-advanced` 创建 Type |
| Step 状态为禁用 | 注册成功但环境未 Publish | 执行 Publish All 或在 PRT 中启用 |

---

## 6. 最佳实践

1. **DLL 只传一次**
   - 先用 `update-assembly` 或 `register-plugin-advanced` 上传 DLL
   - 后续 Step 一律用 `register-step-only`

2. **超时设到 10 分钟**
   - Device Code Flow 必须显式设置 `MaxConnectionTimeout`
   - ClientSecret/OAuth 连接字符串中也可加入 `MaxConnectionTimeout=00:10:00;`

3. **用后台任务跑大操作**
   - 8MB DLL 上传可能需要 3-10 分钟
   - 避免前台 Shell 超时中断

4. **Publish All 期间不要更新 Assembly**
   - Publish All 会锁定制层，导致 Assembly 更新超时
   - 确认 Publish 完成后再执行

5. **保留本地 DLL 备份**
   - 每次发布后将 DLL 放到固定目录（如 `/tmp/` 或 `Backups/Solutions/`）
   - 方便回滚或对比版本

---

## 7. 相关文件

| 文件 | 路径 |
|---|---|
| 连接工厂（超时配置） | `Code/Tools/D365ToolCommon/Connection/D365ConnectionFactory.cs` |
| MetadataTool | `Code/Tools/MetadataTool/` |
| DLL 输出（远程） | `C:\Projects\D365\D365\SanyD365.D365Extension.Sales\bin\Release\` |
| 同步脚本 | `Code/Tools/sync-plugin-to-remote.py` |
| Plugin 注册最佳实践 | `Documents/DevelopmentStandards/D365-Plugin-Registration-Best-Practices.md` |

---

## 8. 本次实践记录

- **Assembly**: `SanyD365.D365Extension.Sales`
- **DLL 大小**: 7,979 KB
- **目标环境**: DEV1 (`https://dev1.crm5.dynamics.com`)
- **新增 Plugin Type**: `SanyD365.D365Extension.Sales.Plugins.Account.CustomerMasterDataCreditValidationPlugin`
- **注册 Step**:
  - `Create of mcs_customermasterdata` — PreValidation / Sync
  - `Update of mcs_customermasterdata` — PreValidation / Sync
- **关键操作**: 将 Device Code Flow 超时从默认 4 分钟调整为 10 分钟后，`update-assembly` 在 3 分 11 秒成功。
