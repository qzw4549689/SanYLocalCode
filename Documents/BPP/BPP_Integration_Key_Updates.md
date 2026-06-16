# BPP 集成关键认知更新纪要

> 日期：2026-06-10
> 主题：SanyD365.Main 部署方式纠正 + 最终发布流程确认

---

## 1. 重大纠正：SanyD365.Main 不是 Plugin

### 之前误解 ❌
- 以为 `SanyD365.Main` 是 Plugin Assembly，需要用 PRT 注册到 D365
- 以为需要 Solution 导入或手动注册 DLL

### 正确理解 ✅
- `SanyD365.Main` 位于 `Service/` 目录下，是**服务层/接口层代码**
- 通过 `[Injection(InterfaceType = ..., Scope = InjectionScope.Singleton)]` 使用 DI 容器注入
- 包含 `BPPHandlerServiceMain`、`BPPService`、`AppBPPFlowService` 等服务层组件
- **不是 D365 Plugin**，不能直接通过 PRT 注册

---

## 2. SanyD365.Main 的真实部署方式

### 部署载体
随 **`SanyD365.D365ExtensionApi`**（Azure App Service）一起打包部署：

```yaml
# CI 流程（来自 D365ExtensionApi-UAT.yml）
cd Service/SanyD365.D365ExtensionApi
dotnet publish --configuration Release
Compress-Archive -Path net8.0/publish/* -DestinationPath D365ExtentionApi.zip
az webapp deployment source config-zip ... --name D365ExtensionAPI-uat
```

- `SanyD365.D365ExtensionApi` 引用了 `SanyD365.Main`
- `dotnet publish` 自动将 `SanyD365.Main.dll` 打包进输出目录
- 最终部署到 **Azure App Service**（UAT）

### 部署流程
```
代码合并到 uat 分支
    │
    ▼
D365 版本发布工具
    https://n8n.sany.com.cn/form/0923e9fb-86fa-48d7-8d94-3d3e360ab7dd
    │
    ▼
选择 Extension API
    │
    ▼
CI/CD 自动编译 SanyD365.D365ExtensionApi（含 SanyD365.Main.dll）
    │
    ▼
部署到 Azure App Service (UAT)
    │
    ▼
UAT 环境测试
```

---

## 3. 两种代码的本质区别

| 项目 | 类型 | 部署方式 | 运行位置 |
|------|------|----------|----------|
| **SanyD365.Main** | Service 层 / 接口层 | 随 Extension API 打包，Azure App Service 部署 | Azure 云端 |
| **SanyD365.Plugins.BppIntegration** | D365 Plugin | Plugin Registration Tool / Solution 导入 | D365 Sandbox |

---

## 4. 我们的 Plugin（BppIntegrationPlugin / BppCallbackPlugin）

### 当前状态
- 已在 **DEV1** 注册（通过 MetadataTool）✅
- **UAT 注册状态待确认**

### 关键问题
如果 UAT 没有注册这两个 Plugin：
- Service 层（Handler）能正常工作 ✅
- 但 `mcs_status` 13→14 **不会自动触发** `mcs_bppstartapi` ❌
- `mcs_bppstatus` 变化**不会自动更新** `mcs_status` ❌

**必须确认**：UAT 是否已有这两个 Plugin Step？

---

## 5. DEV1 不需要手动注册 Service 层

### 之前计划 ❌
- 在 DEV1 用 PRT 注册 SanyD365.Main DLL
- 在 DEV1 测试验证

### 正确流程 ✅
- DEV1 的 Service 层代码（SanyD365.Main）随 DEV Extension API 自动部署
- 或 DEV1 本身就有对应的 Extension API 环境
- **DEV1 不需要手动注册 Service 层**
- 测试直接在 **UAT** 进行

---

## 6. UAT 发布前必须手动配置的系统配置

以下配置**不会随代码自动发布**，需要手动在 UAT D365 中配置：

| 配置 Name | 必须操作 | 当前状态 |
|-----------|----------|----------|
| `BPP_WorkFlowTemplateCode` | JSON 中新增 `"CustomerCreditEvaluation": "781094754802827383"` | 用户已确认 DEV1 已加，UAT 待确认 |
| `D365BaseUrl` | 确认值为 UAT 地址 | 已存在 ✅ |
| `Bpp_ApprovalFlowBaseUrl` | 确认值为 UAT BPP 门户 | 已存在 ✅ |

---

## 7. 最终发布流程（已确认）

```
特性分支 (uat_20260606_qzw)
    │
    ▼
git commit → git push → PR → 合并到 uat
    │
    ▼
D365 版本发布工具（选 Extension API）
    │
    ▼
自动编译 + 部署到 Azure App Service (UAT)
    │
    ▼
UAT 环境测试验证
    • 13→14 触发 BPP
    • mcs_bppstatus = Submitted
    • 审批通过/驳回
    • 回调更新状态
```

**DEV1 完全不用管**，所有操作都在 UAT。

---

## 8. 待确认事项

- [ ] UAT 的 `BPP_WorkFlowTemplateCode` JSON 是否已加 `CustomerCreditEvaluation`？
- [ ] UAT 是否已注册 `BppIntegrationPlugin` 和 `BppCallbackPlugin`？
- [ ] UAT 的 `mcs_credit_record` 实体字段是否与 DEV1 一致？
- [ ] UAT 是否有完整的测试数据（含 mcs_creditscore、mcs_creditgrade）？

---

## 9. 代码修改清单（已推送/待推送）

| # | 文件 | 变更 | 项目 |
|---|------|------|------|
| 1 | `Service/SanyD365.Main/DTO/BPP/BPPFormData.cs` | 新增 `CustomerCreditEvaluation` | SanyD365.Main |
| 2 | `Service/SanyD365.Main/Entities/BPP/BPPHandlerServices/BPPHandlerServiceForCreditRecord.cs` | 新建 Handler | SanyD365.Main |
| 3 | `Service/SanyD365.Main/StartupHelper.cs` | 注册 `mcs_credit_record` Handler | SanyD365.Main |
| 4 | `Code/Customizations/Plugins/BppIntegration/Plugin/BppIntegrationPlugin.cs` | 触发 BPP 发起 | 我们的项目 |
| 5 | `Code/Customizations/Plugins/BppIntegration/Plugin/BppCallbackPlugin.cs` | 处理 BPP 回调 | 我们的项目 |
