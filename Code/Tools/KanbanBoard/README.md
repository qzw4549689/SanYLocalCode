# 任务进度看板（KanbanBoard）

轻量任务进度看板，纯 Python 标准库实现（**无第三方依赖**），SQLite 单文件存储。
部署在远程服务器 `tx-windows`（122.51.232.70），浏览器访问看板，AI 通过 curl 操作任务。

## 工作流程

```
待处理(pending) → 开发中(in_progress) → 待发布(pending_release) → 已发布(released)
```

1. 在 Zed 里告知 AI 任务内容 → AI 调 `POST /api/tasks` 录入，任务进入「待处理」
2. 在 Zed 里告知 AI 任务编号开始处理 → AI 调 `PATCH` 更新为「开发中」
3. 开发完成确认后告知 AI → AI 更新为「待发布」
4. 发布完成后告知 AI → AI 更新为「已发布」，任务进入历史记录

## 本地 / 服务器启动

```bash
python app.py              # 默认 0.0.0.0:8100
python app.py --port 8100
```

- 看板页面：`http://<服务器IP>:8100/`
- 数据文件：`kanban.db`（首次启动自动创建）

## 视图二：发布清单（待发布内容唯一登记处）

聚合当前批次所有待发布内容，按 Solution 包分组展示：

- **entity 类**：package 留空时默认归入 `entity_当天日期_peter`（新建包，随查看日期自动变化）；已指定包名（如 `entity_20260727_peter`）时用指定名
- **固定包**：McsWebResource / McsPlugin / McsCustomAPI
- **配置数据 / 手动步骤**：不随 Solution，单独区块展示各环境状态

每项显示是否已加入发版包（✅ 已在包 / ⏸️ 待加入）。分组默认折叠；发版完成后点该组的「✅ 已发布」逐个包归档，或顶部「本批已全部发布」一键清空。

### 登记 API（AI 开发完成后逐项登记，唯一入口）

```bash
# 登记一项
curl -X POST http://122.51.232.70:8100/api/release-items/item \
  -H 'Content-Type: application/json' \
  -d '{"section":"webresource","component":"mcs_fsm_data.js",
       "summary":"#1560 状态3放行合同编号可编辑","ref":"禅道 #1560",
       "package":"McsWebResource","in_package":true}'

# section 取值：entity / webresource / plugin / customapi / config / manual
# 同一文件多个 Bug 改动合并为一项（PATCH 更新 summary/ref），勿重复登记
```

发布清单 API：

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/release-items` | 当前批次（含 rowid；`?status=released` 查归档） |
| `POST` | `/api/release-items/item` | **逐项登记（主入口）** |
| `PATCH` | `/api/release-items/item/<rowid>` | 更新单项（摘要/入包状态/状态） |
| `DELETE` | `/api/release-items/item/<rowid>` | 删除误登记项 |
| `POST` | `/api/release-items` | 全量替换 `{"items": [...]}`（批量导入用） |
| `POST` | `/api/release-items/release` | 归档：body `{"package":"McsPlugin"}` 按包 / `{"section":"config"}` 按分区 / 空 body 全部 |

> 2026-08-05 起本视图取代原《待发布内容清单.md》（已按用户指示删除，历史见 git）。

## 服务器部署（tx-windows 122.51.232.70）

服务器无 Python，使用 **Python 嵌入式包**（`python/` 目录，免安装、不污染系统）：

- 部署目录：`C:\Projects\KanbanBoard`
- 常驻方式：计划任务 `KanbanBoard`（SYSTEM 账户、开机自启），调用 `start.bat`
- 管理命令：
  ```cmd
  schtasks /run /tn KanbanBoard      :: 启动
  schtasks /end /tn KanbanBoard      :: 停止
  schtasks /query /tn KanbanBoard    :: 查看状态
  ```
- Windows 防火墙已放行 8100（规则名 `KanbanBoard-8100`）

> ⚠️ 腾讯云安全组默认仅放行 22/3389。公网访问需在腾讯云控制台放行 TCP 8100；
> 不放行时可用 SSH 隧道访问：`ssh -N -L 8100:127.0.0.1:8100 tx-windows`，
> 然后浏览器打开 `http://127.0.0.1:8100/`。

## REST API（AI 操作入口）

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/tasks` | 全部任务；`?status=released` 查已发布历史 |
| `POST` | `/api/tasks` | 录入任务 `{"title": "...", "description": "..."}` |
| `GET` | `/api/tasks/T-0001` | 详情 + 状态历史 |
| `PATCH` | `/api/tasks/T-0001` | 更新 `{"status"/"title"/"description", "note"}` |
| `DELETE` | `/api/tasks/T-0001` | 删除误录任务 |

状态值：`pending` / `in_progress` / `pending_release` / `released`

示例：

```bash
# 录入
curl -X POST http://122.51.232.70:8100/api/tasks \
  -H 'Content-Type: application/json' \
  -d '{"title":"修复Bug #1560","description":"DEV1 已验证"}'

# 更新状态为开发中
curl -X PATCH http://122.51.232.70:8100/api/tasks/T-0001 \
  -H 'Content-Type: application/json' \
  -d '{"status":"in_progress","note":"开始开发"}'
```

## 文件说明

| 文件 | 说明 |
|------|------|
| `app.py` | 后端服务（API + 页面托管 + SQLite） |
| `index.html` | 三栏看板页面（原生 HTML/JS，10 秒自动刷新） |
| `start.bat` | 服务器启动脚本（计划任务调用，注意必须保持 ANSI/纯英文，中文注释会导致 cmd 解析错乱） |
| `kanban.db` | SQLite 数据库（运行时生成，勿提交 Git） |
