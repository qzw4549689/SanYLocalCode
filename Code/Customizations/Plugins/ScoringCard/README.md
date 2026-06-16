# 评分卡配置表 Plugin

## 文件结构

```
Plugins/
└── ScoringCard/
    ├── AutoNumber/
    │   ├── AutoNumberPlugin.cs      # 编码自动生成Plugin
    │   └── ScoringCardPlugins.csproj # 项目文件
    └── README.md                     # 本文件
```

## Plugin清单

| Plugin类 | 触发实体 | 触发时机 | 功能 |
|---------|---------|---------|------|
| ScoringCardAutoNumberPlugin | mcs_credit_scoringcard | Create前 | 自动生成编码 SC+YYYYMMDD+4位序列号 |

## 编码规则

- 格式：`SC` + `YYYYMMDD` + `4位序列号`
- 示例：`SC202506040001`
- 每天从0001开始计数

## 部署步骤

### 1. 编译Plugin

```bash
cd Plugins/ScoringCard/AutoNumber
dotnet build -c Release
```

### 2. 注册Plugin（使用Plugin Registration Tool）

1. 打开 Plugin Registration Tool
2. 连接到 D365 环境
3. 注册新Assembly：
   - 选择编译后的 DLL：`bin/Release/net462/SanyD365.Plugins.ScoringCard.dll`
   - 勾选 **Sandbox**
   - 勾选 **Isolation Mode: Sandbox**
4. 注册Step：
   - Message: `Create`
   - Primary Entity: `mcs_credit_scoringcard`
   - Event Pipeline Stage: `Pre-operation`
   - Execution Mode: `Synchronous`

### 3. 测试

1. 新建评分卡配置记录
2. 不填写编码，保存
3. 验证编码是否自动生成（如 SC202506040001）
4. 验证编码保存后不可编辑

## JS文件部署

### 文件位置

```
WebResources/JS/mcs_credit_scoringcard.js
```

### 部署步骤

1. 登录 Power Apps → 解决方案 → entity_20260603_peter
2. 点击 **"Web 资源"** → **"+ 新建"**
3. 填写信息：
   - 名称：`mcs_credit_scoringcard.js`
   - 显示名称：`评分卡配置表-表单逻辑`
   - 类型：`脚本(JScript)`
   - 上传文件：选择 `mcs_credit_scoringcard.js`
4. 保存并发布

### 表单绑定

1. 打开 **客户评分卡配置表** 的 Main 窗体
2. 点击 **"表单属性"**
3. 在 **"事件处理程序"** 中添加：
   - **库**：选择 `mcs_credit_scoringcard.js`
   - **OnLoad**：
     - 函数名：`ScoringCardForm.onLoad`
     - 启用：勾选
   - **OnSave**：
     - 函数名：`ScoringCardForm.onSave`
     - 启用：勾选
4. 保存并发布

## 测试用例

详见项目根目录 `TestCases/1.3_评分卡配置表_测试用例.md`
