# D365 前端自动化测试 - Playwright

## 测试范围

覆盖测试用例文档中的 **23个测试用例**：

| 测试类别 | 用例数 | 测试文件 |
|---------|--------|---------|
| 基础数据配置测试 | 7 | `basic.spec.ts` |
| 主流程测试 | 9 | `main-flow.spec.ts` |
| 异常流程测试 | 4 | （待补充） |
| 批量处理测试 | 3 | （待补充） |

## 环境要求

- Node.js 18+
- Playwright 1.60+
- Chromium 浏览器（已安装）

## 安装

```bash
cd /Users/peterqiu/Work/AIWorkSpace/SanYi/Dev/SanyD365Project
npm install
```

## 配置

### 1. 设置环境变量

```bash
export D365_USERNAME="gw_duanqy@sanyglobal.onmicrosoft.com"
export D365_PASSWORD="your-password"
```

### 2. 首次认证

```bash
npx playwright test e2e/auth.setup.ts
```

首次运行需要手动处理 MFA（如果启用），认证状态会保存到 `playwright/.auth/user.json`。

## 运行测试

### 运行所有测试
```bash
npx playwright test
```

### 运行特定测试文件
```bash
npx playwright test e2e/basic.spec.ts
npx playwright test e2e/main-flow.spec.ts
```

### 运行特定测试用例
```bash
npx playwright test -g "基础-01"
npx playwright test -g "主流程-05"
```

### 有头模式（可见浏览器）
```bash
npx playwright test --headed
```

### 调试模式
```bash
npx playwright test --debug
```

### 生成报告
```bash
npx playwright test
npx playwright show-report
```

## 测试文件说明

### `auth.setup.ts`
- D365 认证设置
- 保存登录状态供后续测试复用

### `fixtures.ts`
- 扩展的 test fixture
- D365Helper 辅助类（导航、表单操作、子网格检查等）

### `basic.spec.ts`
- 基础-01: 实体导航检查
- 基础-02: 客户表单字段检查
- 基础-03: 评分项目数据检查
- 基础-04: 评分卡配置检查
- 基础-05: Plugin功能验证（编码自动生成）
- 基础-06: JS表单逻辑验证（客户选择带出）
- 基础-07: Coface API连通性（命令行测试，不在这里）

### `main-flow.spec.ts`
- 主流程-01: 创建信用评估记录
- 主流程-02: 选择客户
- 主流程-03: 保存记录
- 主流程-04: 状态10关联客户
- 主流程-05: Coface数据集成（状态11→12）
- 主流程-06: 人工复核
- 主流程-07: 信用分计算（状态12→13→14）
- 主流程-08: BPP审批通过（状态14→15）
- 主流程-09: 审批拒绝（状态14→16）

## 注意事项

1. **D365 加载慢**：所有测试已增加超时时间（30-120秒）
2. **Plugin 异步执行**：状态流转测试需要等待 10-30 秒
3. **测试数据依赖**：主流程测试需要预先存在测试客户（有 Coface ID）
4. **认证有效期**：保存的认证状态约 1 小时有效，过期需重新运行 auth.setup.ts
5. **无头模式**：CI 环境建议用无头模式（修改 playwright.config.ts 中 headless: true）

## 与另一个 AI 的分工

| 测试层 | 执行方 | 说明 |
|-------|--------|------|
| Playwright 自动化测试 | 我（开发团队） | 后端功能验证、回归测试 |
| 浏览器手动测试 | 另一个 AI | 前端界面操作、业务流程验证 |

Playwright 测试覆盖核心功能自动化验证，另一个 AI 负责更灵活的界面探索和端到端业务场景验证。
