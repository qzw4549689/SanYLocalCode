# 发版包归档（Releases）

> 每次发版导出并存档的发版包快照，用于下次发版前的字段差异对比（防 80041A06）。
> 对比工具：`Code/Tools/release-diff/`

## 命名规范

- `entity_XXX_exported<导出日期>.zip` — 导出归档的 Solution 包
- `entity_XXX_snapshot<内容日期>.zip` — 历史留存的包（内容日期 ≠ 导出日期时标明）
- `entity_XXX_fieldsnapshot_<日期>/` — 大包导不出时的字段清单快照（list-fields 输出）

## 当前存档

| 文件 | 说明 |
|------|------|
| `entity_all_0720_peter_exported20260724.zip` | ⭐ **本次发版（2026-07-22 批次）内容备份**：9 实体（6 fca + 3 fsm），与生产导入的大包中我方实体内容一致（2026-07-24 导出，已验证含 applygenre=nvarchar / fsm_institution_products=multiselectpicklist / usedsellerbalance=money） |
| `entity_20260603_peter_snapshot20260621.zip` | 6/21 时刻的历史快照（15 实体，信用/成交条件模块） |
| `entity_20260701_peter_exported20260723.zip` | 7/1 增量包（4 信用实体），2026-07-23 补导出 |
| `entity_20260703_exported20260723.zip` | ⚠️ 7/3 生产大版本（186 实体），2026-07-23 补导出，内容为**导出时刻**元数据，非 7/3 原貌 |
| `entity_20260722_fieldsnapshot_20260723/` | 本次发版主清单 20 实体字段清单快照（`entity_20260722` 包过大导出超时，用 list-fields 清单替代） |

**下次发版前对比基准**：`entity_all_0720_peter_exported20260724.zip`（fca/fsm 9 实体）+ `entity_20260722_fieldsnapshot_20260723/`（全量 20 实体字段）。
注：`entity_all_0720_peter` 是我方自建的全实体归纳 Solution，实际生产导入的是三一侧大包，但我方实体内容一致，可作为发版内容基准。
