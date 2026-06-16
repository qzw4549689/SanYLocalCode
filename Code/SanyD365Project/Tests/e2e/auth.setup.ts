import { test as setup, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const authFile = path.join(__dirname, '../playwright/.auth/user.json');

/**
 * 认证设置 - 登录 D365 并保存状态
 * 
 * 运行方式:
 * npx playwright test e2e/auth.setup.ts
 * 
 * 注意: 首次运行需要手动处理 MFA 或设备代码认证
 */
setup('authenticate', async ({ page }) => {
  // 检查是否已有有效认证
  if (fs.existsSync(authFile)) {
    const stats = fs.statSync(authFile);
    const ageHours = (Date.now() - stats.mtimeMs) / (1000 * 60 * 60);
    // Token 有效期约 1 小时，超过 50 分钟重新登录
    if (ageHours < 0.8) {
      console.log('使用现有认证（' + Math.round(ageHours * 60) + ' 分钟前）');
      return;
    }
  }

  console.log('开始 D365 认证...');
  
  // 访问 D365 登录页
  await page.goto('https://dev1.crm5.dynamics.com');
  
  // 等待重定向到 Microsoft 登录页
  await page.waitForURL(/login.microsoftonline.com/, { timeout: 30000 });
  
  // 输入用户名
  await page.fill('input[name="loginfmt"]', process.env.D365_USERNAME || 'gw_duanqy@sanyglobal.onmicrosoft.com');
  await page.click('input[type="submit"]');
  
  // 等待密码输入页
  await page.waitForSelector('input[name="passwd"]', { timeout: 10000 });
  
  // 输入密码（从环境变量获取，避免硬编码）
  const password = process.env.D365_PASSWORD;
  if (!password) {
    throw new Error('请设置环境变量 D365_PASSWORD');
  }
  await page.fill('input[name="passwd"]', password);
  await page.click('input[type="submit"]');
  
  // 处理"保持登录"选项
  try {
    await page.waitForSelector('input[value="Yes"]', { timeout: 5000 });
    await page.click('input[value="Yes"]');
  } catch {
    // 可能没有此选项
  }
  
  // 等待 D365 主页加载
  await page.waitForURL(/crm5.dynamics.com/, { timeout: 60000 });
  await page.waitForLoadState('networkidle');
  
  // 验证登录成功 - 检查页面标题或特定元素
  await expect(page.locator('text=Sales Hub')).toBeVisible({ timeout: 30000 });
  
  console.log('D365 认证成功，保存状态...');
  
  // 保存认证状态
  await page.context().storageState({ path: authFile });
});
