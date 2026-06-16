# 设计文档模板

## 使用说明

请按照以下格式提供你的设计文档，我将据此生成完整的 D365 实体、字段、表单和视图定义。

---

## 1. 项目基本信息

```yaml
项目名称: Sany Project Management
解决方案名称: SanyProjectManagement
版本: 1.0.0.0
发布者前缀: sany
环境: https://sanyglobal.crm.dynamics.com
```

---

## 2. 实体清单

### 实体 1：[实体显示名]

```yaml
显示名: 项目任务
架构名: sany_projecttask
复数显示名: 项目任务
描述: 用于管理三一重工项目中的各项任务
主字段:
  显示名: 任务名称
  架构名: sany_name
  类型: Single line of text
  最大长度: 200
  必填: true
```

#### 字段列表

| 显示名 | 架构名 | 类型 | 必填 | 默认值 | 描述 |
|--------|--------|------|------|--------|------|
| 任务编号 | sany_tasknumber | Autonumber | - | - | 自动生成的任务编号，格式：TASK-{YYYY}-{0000} |
| 任务类型 | sany_tasktype | Choice | true | - | 见选项集：任务类型 |
| 优先级 | sany_priority | Choice | false | 中 | 见选项集：优先级 |
| 负责人 | sany_ownerid | Lookup (User) | false | - | 任务负责人 |
| 关联项目 | sany_projectid | Lookup (Project) | false | - | 所属项目 |
| 计划开始日期 | sany_plannedstart | Date and Time | false | - | 计划开始时间 |
| 计划结束日期 | sany_plannedend | Date and Time | false | - | 计划结束时间 |
| 实际开始日期 | sany_actualstart | Date and Time | false | - | 实际开始时间 |
| 实际结束日期 | sany_actualend | Date and Time | false | - | 实际结束时间 |
| 预估工时 | sany_estimatedhours | Decimal | false | 0 | 预估完成所需工时（小时） |
| 实际工时 | sany_actualhours | Decimal | false | 0 | 实际花费工时（小时） |
| 完成百分比 | sany_completionpercent | Whole Number | false | 0 | 0-100 |
| 任务描述 | sany_description | Multiple lines of text | false | - | 详细描述 |
| 是否里程碑 | sany_ismilestone | Two options | false | No | 是否为关键里程碑 |
| 任务状态 | sany_taskstatus | Choice | true | 未开始 | 见选项集：任务状态 |

#### 选项集定义

**任务类型 (sany_tasktype)**

| 标签 | 值 | 颜色 |
|------|-----|------|
| 设计 | 10000 | #0000FF |
| 开发 | 10001 | #008000 |
| 测试 | 10002 | #FFA500 |
| 部署 | 10003 | #800080 |
| 文档 | 10004 | #808080 |

**优先级 (sany_priority)**

| 标签 | 值 | 颜色 |
|------|-----|------|
| 低 | 10000 | #008000 |
| 中 | 10001 | #FFA500 |
| 高 | 10002 | #FF0000 |
| 紧急 | 10003 | #8B0000 |

**任务状态 (sany_taskstatus)**

| 标签 | 值 | 颜色 |
|------|-----|------|
| 未开始 | 10000 | #808080 |
| 进行中 | 10001 | #0000FF |
| 已完成 | 10002 | #008000 |
| 已延期 | 10003 | #FF0000 |
| 已取消 | 10004 | #000000 |

#### 表单设计

**主表单 (Main Form)**

```
Tab: 基本信息
  Section: 任务信息
    - 任务名称 [sany_name]
    - 任务编号 [sany_tasknumber] (只读)
    - 任务类型 [sany_tasktype]
    - 优先级 [sany_priority]
    - 任务状态 [sany_taskstatus]
    - 是否里程碑 [sany_ismilestone]
  
  Section: 时间安排
    - 计划开始日期 [sany_plannedstart]
    - 计划结束日期 [sany_plannedend]
    - 实际开始日期 [sany_actualstart]
    - 实际结束日期 [sany_actualend]
  
  Section: 工时统计
    - 预估工时 [sany_estimatedhours]
    - 实际工时 [sany_actualhours]
    - 完成百分比 [sany_completionpercent]

Tab: 详细信息
  Section: 描述
    - 任务描述 [sany_description]
  
  Section: 关联信息
    - 负责人 [sany_ownerid]
    - 关联项目 [sany_projectid]
    - 创建者 [createdby] (只读)
    - 创建时间 [createdon] (只读)
    - 修改者 [modifiedby] (只读)
    - 修改时间 [modifiedon] (只读)
```

#### 视图设计

**视图 1：活跃项目任务 (Active Project Tasks)**

```yaml
名称: 活跃项目任务
类型: Public
筛选条件: 状态 = 活跃
排序: 优先级 (降序), 计划开始日期 (升序)
列:
  - 任务名称
  - 任务编号
  - 任务类型
  - 优先级
  - 任务状态
  - 计划开始日期
  - 计划结束日期
  - 完成百分比
  - 负责人
```

**视图 2：我的项目任务 (My Project Tasks)**

```yaml
名称: 我的项目任务
类型: Public
筛选条件: 负责人 = 当前用户 AND 状态 = 活跃
排序: 计划开始日期 (升序)
列:
  - 任务名称
  - 任务类型
  - 优先级
  - 任务状态
  - 计划结束日期
  - 完成百分比
```

**视图 3：本周到期任务 (Due This Week)**

```yaml
名称: 本周到期任务
类型: Public
筛选条件: 计划结束日期 = 本周 AND 状态 = 活跃
排序: 计划结束日期 (升序)
列:
  - 任务名称
  - 优先级
  - 计划结束日期
  - 完成百分比
  - 负责人
```

**快速查找视图 (Quick Find)**

```yaml
名称: 快速查找项目任务
搜索字段:
  - 任务名称
  - 任务编号
  - 任务描述
列:
  - 任务名称
  - 任务编号
  - 任务类型
  - 优先级
  - 任务状态
```

---

### 实体 2：[另一个实体]

（同上格式...）

---

## 3. 关系设计

### 1:N 关系

| 父实体 | 子实体 | 关系名称 | 级联行为 |
|--------|--------|----------|----------|
| 项目 | 项目任务 | sany_project_tasks | 全部级联 |
| 用户 | 项目任务 | sany_user_tasks | 无级联 |

### N:N 关系

| 实体 A | 实体 B | 关系名称 | 描述 |
|--------|--------|----------|------|
| 项目任务 | 用户 | sany_task_users | 任务参与者 |

---

## 4. 安全性设计

### 安全角色

**角色 1：项目经理 (Project Manager)**

| 实体 | 创建 | 读取 | 写入 | 删除 | 追加 | 追加到 | 分派 | 共享 |
|------|------|------|------|------|------|--------|------|------|
| 项目任务 | 组织 | 组织 | 组织 | 组织 | 组织 | 组织 | 组织 | 组织 |

**角色 2：团队成员 (Team Member)**

| 实体 | 创建 | 读取 | 写入 | 删除 | 追加 | 追加到 | 分派 | 共享 |
|------|------|------|------|------|------|--------|------|------|
| 项目任务 | 用户 | 业务部门 | 用户 | 无 | 用户 | 用户 | 无 | 无 |

---

## 5. 业务规则（可选）

```yaml
规则 1:
  名称: 任务完成验证
  描述: 当任务状态设为已完成时，实际结束日期必须填写
  条件: 任务状态 = 已完成
  操作: 如果 实际结束日期 为空，则 显示错误 "请填写实际结束日期"

规则 2:
  名称: 自动计算延期
  描述: 当计划结束日期已过且任务未完成时，自动更新状态为已延期
  条件: 计划结束日期 < 今天 AND 任务状态 != 已完成
  操作: 设置 任务状态 = 已延期
```

---

## 6. 业务流程（可选）

```yaml
流程名称: 项目任务生命周期
阶段:
  - 名称: 新建
    步骤:
      - 填写任务基本信息
      - 分配负责人
  - 名称: 执行
    步骤:
      - 更新进度
      - 记录工时
  - 名称: 完成
    步骤:
      - 填写实际完成时间
      - 提交验收
```

---

## 提交格式

你可以使用以下任一格式提交：

1. **Markdown 文件**（推荐）：直接按照上面的模板填写
2. **Excel 文件**：每个实体一个 Sheet，包含字段、选项集、表单、视图
3. **JSON/YAML 文件**：结构化数据格式
4. **纯文本**：按照模板格式描述

请尽量完整填写，缺失的信息我会使用合理的默认值。
