# 发版包字段对比工具（防 80041A06 导入失败）

> **用途**：发版前对比「上次发版包」与「本次发版内容」的实体字段差异，提前发现会导致生产导入失败（`80041A06`：同名字段类型不一致）的隐患。
> **原则**：全部只读（文件解析 / Solution 导出 / list-fields 查询），不涉及任何环境修改动作。

## 脚本

| 脚本 | 用法 | 场景 |
|------|------|------|
| `diff_solution_packages.py` | `python3 diff_solution_packages.py <上次发版包.zip> <本次发版包.zip>` | 包 vs 包（标准路径） |
| `diff_package_vs_dev1.py` | `python3 diff_package_vs_dev1.py <上次发版包.zip> <dev1清单目录>` | 包 vs DEV1 实时字段清单（本次包导不出来时的替代） |

输出三段：
- 🚨 **同名字段类型不一致** → 非空 = 必须先通知三一在目标环境删除旧字段，否则导入必败
- ➖ 旧包有、本次已删除的字段 → update 模式导入不报错，建议清理
- ➕ 本次新增字段/实体 → 不冲突，仅核对清单用

## 标准流程（每次发版）

```bash
# 1. 发版时：导出发版包并归档（只读导出；大包需 15-30 分钟）
cd Code/Tools/MetadataTool
dotnet run --no-build -- export entity_YYYYMMDD ../../../Backups/Solutions/Releases/entity_YYYYMMDD_exported<导出日期>.zip

# 1b. 大包导不出（30 分钟超时）时，用字段清单快照替代：
for e in <实体列表>; do dotnet run --no-build -- list-fields "$e" > ../../../Backups/Solutions/Releases/entity_YYYYMMDD_fieldsnapshot_<日期>/$e.txt; done

# 2. 下次发版前：对比上次归档包 vs 本次内容
python3 Code/Tools/release-diff/diff_solution_packages.py \
  Backups/Solutions/Releases/<上次包>.zip \
  Backups/Solutions/Releases/<本次包>.zip
```

## 注意

- **归档必须是「发版时刻」的包**。事后补导出的旧包拿到的是当前元数据，对比会漏掉已发生的类型变更（2026-07-23 生产导入失败事故教训）。
- 多选选项集在 Solution XML 中为 `multiselectpicklist`，在 list-fields 中为 `Virtual`，脚本已统一映射，勿改回。
- DEV1 字段清单通过 `MetadataTool list-fields` 生成（只读查询）。

## 事故背景

2026-07-23 生产导入 `entity_20260722` 批次失败（`mcs_applygenre` Picklist vs String），根因是历史上 3 个字段「删除后同名重建不同类型」。详见 `Documents/Planning/Releases/生产导入失败_字段类型冲突清单_20260723.md`。


---

# 离线导入仿真（预热导入）— simulate_import.py

> **用途**：发版前模拟「按固定顺序把本批包导入目标环境」，提前发现依赖不闭合（Step/CustomAPI 的实现类所在 Assembly 不在本批先发包、也未到过目标环境 → 导入必报「缺少依赖项」）。2026-08-20 新增（#1641 幽灵 Step 生产导入被拦事故防线）。
> **原则**：全部只读（DEV1 元数据查询 + UAT 在线查询 + 本地 zip 解析），不导入、不修改任何环境。

## 用法

```bash
cd Code/Tools/release-diff

# 模拟发 UAT（在线精确判定，推荐每次发 UAT 前跑）
python3 simulate_import.py --target uat

# 模拟发生产（离线注册表 + UAT 镜像基线，台账未覆盖项报 ⚠️ 待确认）
python3 simulate_import.py --target prod --batch McsCustomAPI McsPlugin entity_20260727_peter

# 重建生产注册表（扫 Backups/Solutions 全部归档 zip）
python3 simulate_import.py build-registry

# 生成实体基线种子（UAT 自定义实体全集 − 主清单我方实体；只需跑一次/大版本后重跑）
python3 simulate_import.py seed-from-uat
```

- 默认批次：`McsCustomAPI → McsPlugin`（插件类组件所在包）；实体/角色包每批不同，用 `--batch` 按导入顺序传入。
- 依赖边（v1 范围）：Step→实现类 Assembly、CustomAPI→实现类 Assembly、Step→主实体。

## 判定口径

| 结果 | 含义 |
|------|------|
| ✅ | 闭合：随本批先发包 / 目标环境（或台账/基线）已有 / 平台内置实体 |
| 🚨 | 缺口（仅 UAT 在线模式能下定论）：我方程序集既不在本批也不在目标环境 → **导入必被拦** |
| ⚠️ | 待确认：台账未覆盖（他方组件或未归档），需人工确认；`SanyD365.*` 标【重点确认】 |

## 数据源与精度

- **包内容/依赖边**：DEV1 实时只读查询（solutioncomponent + step/customapi 元数据）。
- **生产状态**：① `registry_prod.json`（归档 zip 的 RootComponents 解析，build-registry 重建）② `prod_baseline_extra.json`（UAT 镜像实体基线，已剔除主清单我方实体防假绿；Assembly 不种子——Assembly 类依赖必须靠归档或本批同发证明）。
- **精度演进**：历史归档缺 McsPlugin/McsCustomAPI 等 n8n 产物包 → 每批交付 IT 的包（含 n8n 产物）必须原样归档 `Backups/Solutions/Releases/`，注册表随批变准；长期最优是 IT 给我方开生产只读权限（届时 prod 模式可改在线精确判定）。

## 实测基线（2026-08-20）

- `--target uat`（McsCustomAPI→McsPlugin，6590 条边）：全绿。
- `--target prod --batch McsPlugin`（复现 #1641 场景）：精确揪出 2 条跨包 Assembly 依赖（MSLibrary 编号插件、WarrantyPlanCopyApi）。
