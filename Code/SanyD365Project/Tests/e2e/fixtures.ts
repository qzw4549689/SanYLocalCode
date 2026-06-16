import { test as base, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const authFile = path.join(__dirname, '../playwright/.auth/user.json');

/**
 * 扩展的 test fixture - 自动加载 D365 认证状态
 */
export const test = base.extend<{
  d365Page: any;
}>({
  // 自动使用已保存的认证状态
  storageState: async ({}, use) => {
    if (fs.existsSync(authFile)) {
      await use(authFile);
    } else {
      console.warn('警告: 未找到认证文件，请先运行 auth.setup.ts');
      await use(undefined);
    }
  },
});

export { expect };

/**
 * D365 页面操作辅助函数
 */
export class D365Helper {
  constructor(private page: any) {}

  /**
   * 等待 D365 页面完全加载
   */
  async waitForPageLoad() {
    await this.page.waitForLoadState('networkidle');
    // D365 有 loading spinner
    await this.page.waitForSelector('.pa-bp.loading', { state: 'hidden', timeout: 60000 }).catch(() => {});
  }

  /**
   * 通过导航打开实体列表
   */
  async navigateToEntity(entityDisplayName: string) {
    // 点击"..."更多按钮或搜索
    const searchBox = this.page.locator('[aria-label*="搜索"], [placeholder*="搜索"]').first();
    if (await searchBox.isVisible({ timeout: 5000 }).catch(() => false)) {
      await searchBox.fill(entityDisplayName);
      await this.page.keyboard.press('Enter');
      await this.waitForPageLoad();
      return;
    }

    // 尝试通过左侧导航
    const navButton = this.page.locator(`text=${entityDisplayName}`).first();
    if (await navButton.isVisible({ timeout: 5000 }).catch(() => false)) {
      await navButton.click();
      await this.waitForPageLoad();
      return;
    }

    throw new Error(`无法导航到实体: ${entityDisplayName}`);
  }

  /**
   * 点击"新建"按钮
   */
  async clickNew() {
    const newButton = this.page.locator('button[title*="新建"], button:has-text("新建"), [aria-label*="新建"]').first();
    await newButton.waitFor({ state: 'visible', timeout: 10000 });
    await newButton.click();
    await this.waitForPageLoad();
  }

  /**
   * 在表单中查找字段
   */
  async findField(fieldLabel: string) {
    // D365 表单字段通常有 label 和对应的 input
    const field = this.page.locator(`text=${fieldLabel}`).first();
    await field.waitFor({ state: 'visible', timeout: 10000 });
    return field;
  }

  /**
   * 获取字段值
   */
  async getFieldValue(fieldLabel: string) {
    const field = await this.findField(fieldLabel);
    // 找到对应的 input 或 readonly 元素
    const input = this.page.locator(`[aria-label*="${fieldLabel}"], [title*="${fieldLabel}"]`).first();
    return await input.inputValue().catch(() => input.textContent());
  }

  /**
   * 保存记录
   */
  async saveRecord() {
    const saveButton = this.page.locator('button[title*="保存"], button:has-text("保存"), [aria-label*="保存"]').first();
    await saveButton.click();
    await this.waitForPageLoad();
    // 等待保存完成（loading 消失）
    await this.page.waitForTimeout(2000);
  }

  /**
   * 刷新记录
   */
  async refreshRecord() {
    const refreshButton = this.page.locator('button[title*="刷新"], [aria-label*="刷新"]').first();
    await refreshButton.click();
    await this.waitForPageLoad();
  }

  /**
   * 检查子网格记录数
   */
  async getSubgridRecordCount(subgridName: string) {
    const subgrid = this.page.locator(`[data-id*="${subgridName}"], [aria-label*="${subgridName}"]`).first();
    const countText = await subgrid.locator('.record-count, [data-id*="count"]').textContent().catch(() => '');
    const match = countText.match(/(\d+)/);
    return match ? parseInt(match[1]) : 0;
  }

  /**
   * 截图保存
   */
  async takeScreenshot(name: string) {
    await this.page.screenshot({ 
      path: `playwright-report/screenshots/${name}.png`,
      fullPage: false 
    });
  }
}
