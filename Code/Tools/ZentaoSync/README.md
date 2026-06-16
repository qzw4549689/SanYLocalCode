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
