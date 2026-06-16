# DeployTool

D365 部署与 BPP 诊断工具。

## 常用命令

```bash
# 部署 BPP Plugin
D365_URL=https://sany-uat.crm5.dynamics.com dotnet run -- bpp

# 探测 BPP 框架实现（CustomAPI、Assembly、Plugin Steps）
D365_URL=https://sany-uat.crm5.dynamics.com dotnet run -- probe

# 直接调用 mcs_bppstartapi 测试指定 credit record
D365_URL=https://sany-uat.crm5.dynamics.com dotnet run -- test-bpp-start <recordId>

# 下载并反编译指定 Plugin Assembly（用于查看 BppStartApis 等实现）
D365_URL=https://sany-uat.crm5.dynamics.com dotnet run -- download-assembly SanyD365.D365ExtensionApi.Sales
```

## 环境变量

- `D365_URL`：目标 D365 环境 URL，默认 `https://dev1.crm5.dynamics.com`
- `D365_APPID`、`D365_TENANTID`、`D365_CLIENTSECRET` / `D365_USERNAME` / `D365_PASSWORD`：认证信息
- 未提供 ClientSecret/用户名密码时，使用 Device Code Flow（首次需浏览器登录）
