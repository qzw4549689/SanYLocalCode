# 2026-06-13 Coface 配置化代码备份

## 背景

同事曹阳通知需要回滚 `uat` 到 `Merged PR 2982: fix隐藏字段`（2026/6/12 23:44:40），该节点之后的代码可能被覆盖。本备份用于保存今天早上已发布的 **Coface API 配置化（PR 2983）** 以及本地尚未推送的 **F6.8 汇率表配置化** 代码。

## 备份内容

### 1. 本地 F6.8 汇率表配置化代码

路径：`CofaceIntegration/`、`MetadataTool/`

包含：
- `CofaceExchangeRateHelper.cs`：从 `mcs_coface_exchange_rate` 读取汇率
- `Urba360Parser.cs` / `FullReportParser.cs`：删除硬编码汇率 switch，改为调用 Helper
- `CofaceDataSyncPlugin.cs`：实例化 `FullReportParser` 时传入 `service`
- `mcs_coface_exchange_rate.json`：实体定义
- `MetadataTool/Program.cs`：`seed-coface-exchange-rates` 命令 + 移除 `create` 自动加入 Solution 逻辑

### 2. 远程 PR 2983 Coface API 配置化代码

路径：`Remote-PR2983/`

- `0001-Coface-API-config.patch`：PR 2983 的完整 patch（commit `1668a8596f`）
- `files/`：PR 2983 修改的 5 个文件的完整快照
  - `Application/Sales/CofaceIntegration/CofaceApiConfig.cs`
  - `Application/Sales/CofaceIntegration/CofaceApiService.cs`
  - `Application/Sales/CofaceIntegration/CofaceConfigHelper.cs`
  - `Application/Sales/CofaceIntegration/CofaceTokenManager.cs`
  - `SanyD365.D365Extension.Sales.csproj`

### 3. 远程 Git 备份分支

已在远程服务器 `tx-windows` 的 `C:\Projects\D365` 仓库创建备份分支：

```
backup/coface-api-config-20260613  ->  1668a8596f
```

该分支指向 PR 2983 合并后的 commit，即使 `uat` 回滚，仍可从该分支恢复。

## 恢复方式

### 从 patch 恢复

```bash
cd /c/Projects/D365
git checkout uat
git pull
git am < /path/to/0001-Coface-API-config.patch
```

### 从备份分支恢复

```bash
cd /c/Projects/D365
git checkout uat
git pull
git cherry-pick 1668a8596f
# 或合并备份分支
git merge backup/coface-api-config-20260613
```

### 从本地文件快照恢复

直接将 `Remote-PR2983/files/D365/SanyD365.D365Extension.Sales/` 下的对应文件复制到远程项目目录，然后重新编译提交。

## 后续注意

- 回滚后，F6.x Coface API 配置化在 `uat` 上暂时失效，需要等回滚完成后再重新合并/发布。
- F6.8 汇率表配置化尚未进入远程仓库，回滚不影响，后续可继续开发，最后与 F6.x 一起统一发布。
