# ZentaoSync — 禅道数据同步工具

> 将 D365 项目数据同步到禅道项目管理系统

---

## 文件说明

| 文件 | 用途 |
|------|------|
| `zentao_client.py` | 禅道 API 客户端封装（登录/Bug/任务/用例/查询） |
| `sync_bugs.py` | Bug 同步脚本 |
| `sync_cases.py` | 测试用例同步脚本（待实现） |
| `sync_tasks.py` | 开发任务同步脚本（待实现） |

---

## 环境要求

```bash
pip install requests
```

---

## 使用步骤

### 1. 查询禅道中的产品和执行ID

```bash
cd Code/Tools/ZentaoSync
python -c "
from zentao_client import ZentaoClient
client = ZentaoClient('https://peterqiu.chandao.net', '你的用户名', '你的密码')
client.login()
print('产品:', client.get_products())
print('项目:', client.get_projects())
"
```

### 2. 同步Bug

```bash
python sync_bugs.py \
  --account "你的用户名" \
  --password "你的密码" \
  --product-id 1
```

### 3. 同步测试用例（待实现）

```bash
python sync_cases.py \
  --account "你的用户名" \
  --password "你的密码" \
  --product-id 1
```

### 4. 同步开发任务（待实现）

```bash
python sync_tasks.py \
  --account "你的用户名" \
  --password "你的密码" \
  --execution-id 1
```

---

## 禅道 API 参考

| 操作 | API | 方法 |
|------|-----|------|
| 登录 | `/api.php/v1/tokens` | POST |
| 产品列表 | `/api.php/v1/products` | GET |
| 项目列表 | `/api.php/v1/projects` | GET |
| 执行列表 | `/api.php/v1/executions` | GET |
| 创建Bug | `/api.php/v1/products/{id}/bugs` | POST |
| 创建任务 | `/api.php/v1/executions/{id}/tasks` | POST |
| 创建用例 | `/api.php/v1/products/{id}/cases` | POST |

---

## 注意事项

- 禅道版本需 ≥ 16.5（支持 RESTful API v1）
- Token 有效期有限，长时间操作可能需要重新登录
- 创建Bug时需要指定 `openedBuild`，通常为 `trunk`
- 严重程度和优先级：1=最高，4=最低

---

## 禅道 Bug 处理 CLI（2026-07-30 新增）

`zentao_bug.py`：读取我的待处理 Bug、查看单个 Bug 详情、标记已解决并指派给提出人。

### 公司禅道对接架构（开源版16.0 定制部署，develop.deepcomplus.cn:22913）

| 能力 | 通道 | 说明 |
|------|------|------|
| 我的待处理 Bug 列表 | Web 会话 + 页面解析（`zentao_web.py`） | REST API 路由不支持 query string，产品 Bug 列表无法翻页、无 /my/bugs 接口 |
| 单个 Bug 详情 | REST API（`zentao_client.py`） | `GET /api.php/v1/bugs/{id}`，Token 认证（请求头为 `Token`，非 Bearer） |
| 标记已解决 | Web 表单提交（`zentao_web.py`） | 16.0 REST API 无 `/bugs/{id}/resolve` 动作端点；提交后 API 回读验证 |

Web 登录要点（复用禅道前端逻辑）：`GET /user-refreshrandom.html` 拿随机数 →
密码 `md5(md5(密码)+rand)` → POST `/user-login.html`（**必须带 `Referer` 头**，否则静默返回登录页）。

### 认证配置（三选一，命令行参数优先）

```bash
# 方式 1：环境变量
export ZENTAO_URL="https://公司禅道地址"
export ZENTAO_ACCOUNT="你的账号"
export ZENTAO_PASSWORD="你的密码"

# 方式 2：本目录 .zentao.json（已加入 .gitignore，当前已配置公司禅道 peter 账号）
{"url": "...", "account": "...", "password": "..."}
```

运行使用目录内 venv：`venv/bin/python zentao_bug.py ...`

### 命令

```bash
# 1. 读取指派给我的待处理 Bug 列表
venv/bin/python zentao_bug.py list

# 2. 查看单个 Bug 详情（标题/状态/提出人/重现步骤）
venv/bin/python zentao_bug.py get 1433

# 3. 标记已解决（先预览，确认后加 --yes 才真正执行）
venv/bin/python zentao_bug.py resolve 1433 --comment "根因xxx，已修复并部署DEV1验证通过"
venv/bin/python zentao_bug.py resolve 1433 --comment "..." --yes
```

### resolve 行为说明

- 自动读取 Bug 提出人（openedBy），解决后指派回提出人验证关闭
- 解决方案默认 `fixed`；解决版本默认取 Bug 影响版本，可用 `--build` 覆盖
- 仅 active 状态的 Bug 可解决；不加 `--yes` 时只打印预览不执行
- 执行后自动 API 回读验证状态是否变为 resolved
- 流程约束（来自 AGENTS.md）：由用户提供 Bug 号，且明确授权后才执行 resolve
