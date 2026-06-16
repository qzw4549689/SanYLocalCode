import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright 配置 - D365 前端自动化测试
 * 
 * 测试范围：
 * - 实体导航检查
 * - 表单字段验证
 * - 客户选择带出
 * - 状态流转触发
 * - 子网格数据验证
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false, // D365 测试有依赖，串行执行
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1, // D365 单用户登录，单 worker
  reporter: [
    ['html', { outputFolder: 'playwright-report' }],
    ['list']
  ],
  use: {
    baseURL: 'https://dev1.crm5.dynamics.com',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'on-first-retry',
    headless: false, // D365 需要可视化调试，可改为 true 无头模式
    viewport: { width: 1920, height: 1080 },
    // D365 加载慢，增加超时
    actionTimeout: 30000,
    navigationTimeout: 60000,
  },
  projects: [
    {
      name: 'chromium',
      use: { 
        ...devices['Desktop Chrome'],
        launchOptions: {
          args: ['--disable-blink-features=AutomationControlled'],
        }
      },
    },
  ],
  // 全局超时 10 分钟（D365 加载慢）
  globalTimeout: 600000,
});
