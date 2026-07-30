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
