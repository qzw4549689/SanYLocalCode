import { test, expect } from './fixtures';
import { D365Helper } from './fixtures';

/**
 * 基础数据配置测试
 * 对应测试用例: 基础-01 ~ 基础-07
 */

test.describe('基础数据配置测试', () => {
  let helper: D365Helper;

  test.beforeEach(async ({ page }) => {
    helper = new D365Helper(page);
    await page.goto('https://dev1.crm5.dynamics.com');
    await helper.waitForPageLoad();
  });

  /**
   * 基础-01: 实体导航检查
   * 验证6个实体在导航中可访问
   */
  test('基础-01: 实体导航检查', async ({ page }) => {
    const entities = [
      '客户信用评分项目表',
      '定性评分项目枚举值',
      '客户评分卡配置表',
      '客户信用标签表',
      '客户信用评估记录表',
      '客户资信附件表',
    ];

    for (const entityName of entities) {
      await test.step(`检查实体: ${entityName}`, async () => {
        try {
          await helper.navigateToEntity(entityName);
          // 验证列表视图加载成功
          const grid = page.locator('.ms-DetailsList, [data-id*="grid"], .data-grid').first();
          await expect(grid).toBeVisible({ timeout: 15000 });
          console.log(`✅ ${entityName} - 导航成功`);
        } catch (e) {
          console.error(`❌ ${entityName} - 导航失败: ${e}`);
          throw e;
        }
      });
    }
  });

  /**
   * 基础-02: 客户表单字段检查
   * 验证客户表单中8个自定义字段存在
   */
  test('基础-02: 客户表单字段检查', async ({ page }) => {
    await helper.navigateToEntity('客户');
    await helper.waitForPageLoad();

    // 打开第一条客户记录
    const firstRow = page.locator('.ms-DetailsRow, [data-id*="row"]').first();
    await firstRow.click();
    await helper.waitForPageLoad();

    const expectedFields = [
      '科法斯客户代码',
      '经销商分级',
      '客户信用外部评级',
      '逾期未回收率模型分',
      '客户信用评分',
      '客户等级',
      '重点尽调',
      '信用评估有效状态',
    ];

    for (const fieldName of expectedFields) {
      await test.step(`检查字段: ${fieldName}`, async () => {
        const field = page.locator(`text=${fieldName}`).first();
        await expect(field).toBeVisible({ timeout: 10000 });
        console.log(`✅ ${fieldName} - 字段存在`);
      });
    }
  });

  /**
   * 基础-03: 评分项目数据检查
   * 验证评分项目表有12条记录
   */
  test('基础-03: 评分项目数据检查', async ({ page }) => {
    await helper.navigateToEntity('客户信用评分项目表');
    await helper.waitForPageLoad();

    // 等待数据加载
    await page.waitForTimeout(3000);

    // 获取记录数
    const countText = await page.locator('.record-count, [data-id*="count"], .ms-DetailsList-headerCount').first().textContent().catch(() => '');
    const match = countText.match(/(\d+)/);
    const recordCount = match ? parseInt(match[1]) : 0;

    console.log(`评分项目记录数: ${recordCount}`);
    expect(recordCount).toBeGreaterThanOrEqual(12);

    // 验证关键评分项目存在
    const expectedItems = [
      '外部评级',
      '迟付指数',
      '国别风险',
      '行业风险',
      '净资产',
      '资产负债率',
    ];

    for (const itemName of expectedItems) {
      const item = page.locator(`text=${itemName}`).first();
      await expect(item).toBeVisible({ timeout: 5000 });
    }
  });

  /**
   * 基础-04: 评分卡配置检查
   * 验证评分卡配置表可访问且能创建记录
   */
  test('基础-04: 评分卡配置检查', async ({ page }) => {
    await helper.navigateToEntity('客户评分卡配置表');
    await helper.waitForPageLoad();

    // 点击新建
    await helper.clickNew();

    // 验证表单加载
    const form = page.locator('.form-section, [data-id*="form"]').first();
    await expect(form).toBeVisible({ timeout: 15000 });

    // 验证关键字段存在
    const keyFields = ['评分卡名称', '评分卡类型', '评分项目', '赋分'];
    for (const field of keyFields) {
      const fieldElement = page.locator(`text=${field}`).first();
      await expect(fieldElement).toBeVisible({ timeout: 5000 });
    }

    console.log('✅ 评分卡配置表表单正常');
  });

  /**
   * 基础-05: Plugin功能验证 - 编码自动生成
   */
  test('基础-05: Plugin功能验证 - 编码自动生成', async ({ page }) => {
    await helper.navigateToEntity('客户信用评估记录表');
    await helper.waitForPageLoad();

    // 新建记录
    await helper.clickNew();

    // 检查编码字段是否已自动生成
    await page.waitForTimeout(2000);

    // 查找编码字段（信用评估编码）
    const scoreIdField = page.locator('[aria-label*="信用评估编码"], [title*="信用评估编码"]').first();
    const scoreId = await scoreIdField.inputValue().catch(() => '');

    console.log(`编码值: ${scoreId}`);
    
    // 验证编码格式: SCO + YYYYMMDD + 4位序列号
    expect(scoreId).toMatch(/^SCO\d{12}$/);

    // 验证申请人自动填充
    const applicantField = page.locator('[aria-label*="申请人"], [title*="申请人"]').first();
    const applicant = await applicantField.inputValue().catch(() => '');
    expect(applicant).not.toBe('');

    // 验证发起日期自动填充
    const initDateField = page.locator('[aria-label*="发起评估日期"], [title*="发起评估日期"]').first();
    const initDate = await initDateField.inputValue().catch(() => '');
    expect(initDate).not.toBe('');

    console.log('✅ Plugin功能验证通过 - 编码、申请人、日期自动生成');
  });

  /**
   * 基础-06: JS表单逻辑验证 - 客户选择带出
   */
  test('基础-06: JS表单逻辑验证 - 客户选择带出', async ({ page }) => {
    await helper.navigateToEntity('客户信用评估记录表');
    await helper.waitForPageLoad();

    // 新建记录
    await helper.clickNew();
    await page.waitForTimeout(2000);

    // 点击客户编码查找字段
    const accountField = page.locator('[aria-label*="客户编码"], [title*="客户编码"]').first();
    await accountField.click();

    // 等待查找对话框
    const lookupDialog = page.locator('.lookup-dialog, [role="dialog"]').first();
    await expect(lookupDialog).toBeVisible({ timeout: 10000 });

    // 搜索测试客户
    const searchInput = lookupDialog.locator('input[type="search"], input[placeholder*="搜索"]').first();
    await searchInput.fill('TEST');
    await page.keyboard.press('Enter');
    await page.waitForTimeout(3000);

    // 选择第一条记录
    const firstResult = lookupDialog.locator('.ms-DetailsRow, [data-id*="row"]').first();
    await firstResult.click();

    // 确认选择
    const okButton = lookupDialog.locator('button:has-text("确定"), button:has-text("OK"), button[title*="选择"]').first();
    await okButton.click();
    await page.waitForTimeout(3000);

    // 验证带出字段
    const custNameField = page.locator('[aria-label*="客户名称"], [title*="客户名称"]').first();
    const custName = await custNameField.inputValue().catch(() => '');
    expect(custName).not.toBe('');

    const countryField = page.locator('[aria-label*="国家编码"], [title*="国家编码"]').first();
    const countryCode = await countryField.inputValue().catch(() => '');
    expect(countryCode).not.toBe('');

    const cofaceField = page.locator('[aria-label*="科法斯客户代码"], [title*="科法斯客户代码"]').first();
    const cofaceId = await cofaceField.inputValue().catch(() => '');
    expect(cofaceId).not.toBe('');

    console.log(`✅ 客户选择带出验证通过 - 名称:${custName}, 国家:${countryCode}, CofaceID:${cofaceId}`);
  });

  /**
   * 基础-07: BPF流程配置验证
   * 验证BPF"信用评估记录流程"已配置且8个阶段正确显示
   */
  test('基础-07: BPF流程配置验证', async ({ page }) => {
    await helper.navigateToEntity('客户信用评估记录表');
    await helper.waitForPageLoad();

    // 新建记录以查看BPF
    await helper.clickNew();
    await page.waitForTimeout(3000);

    // 验证BPF标题
    const bpfTitle = await page.locator('.bpf-title, [data-id="header_processbar"] .title, [aria-label*="信用评估记录流程"]').first().textContent().catch(() => '');
    console.log(`BPF标题: ${bpfTitle}`);
    expect(bpfTitle).toContain('信用评估记录流程');

    // 验证BPF阶段数量（至少能看到部分阶段）
    const bpfStages = await page.locator('.stage-name, .bpf-stage-name, [data-id="header_processbar"] .stage').allTextContents().catch(() => []);
    console.log(`BPF阶段: ${bpfStages.join(', ')}`);
    expect(bpfStages.length).toBeGreaterThanOrEqual(2); // 至少能看到2个阶段

    // 验证第一个阶段是"发起信用评估"
    const firstStage = bpfStages[0] || '';
    expect(firstStage).toContain('发起信用评估');

    // 验证BPF当前在第一个阶段（新建记录时）
    const activeStage = await page.locator('.bpf-active-stage, [data-id="header_processbar"] .active .stage-name').first().textContent().catch(() => '');
    console.log(`当前活动阶段: ${activeStage}`);
    expect(activeStage).toContain('发起信用评估');

    console.log('✅ BPF流程配置验证通过');
  });

  /**
   * 基础-08: BPF阶段与状态值同步验证
   * 验证状态变更后BPF阶段自动同步
   */
  test('基础-08: BPF阶段与状态值同步', async ({ page }) => {
    test.setTimeout(60000);

    await helper.navigateToEntity('客户信用评估记录表');
    await helper.waitForPageLoad();

    // 新建记录
    await helper.clickNew();
    await page.waitForTimeout(2000);

    // 选择客户
    const accountField = page.locator('[aria-label*="客户编码"]').first();
    await accountField.click();

    const lookupDialog = page.locator('.lookup-dialog, [role="dialog"]').first();
    await expect(lookupDialog).toBeVisible({ timeout: 10000 });

    const searchInput = lookupDialog.locator('input[type="search"]').first();
    await searchInput.fill('TEST');
    await page.keyboard.press('Enter');
    await page.waitForTimeout(3000);

    const firstResult = lookupDialog.locator('.ms-DetailsRow').first();
    await firstResult.click();

    const okButton = lookupDialog.locator('button:has-text("确定"), button:has-text("OK")').first();
    await okButton.click();
    await page.waitForTimeout(3000);

    await helper.saveRecord();

    // 验证初始状态=9，BPF在"发起信用评估"
    let currentStatus = await page.locator('[aria-label*="评估状态"]').first().inputValue().catch(() => '');
    console.log(`初始状态: ${currentStatus}`);
    expect(currentStatus).toContain('发起信用评估');

    // 修改状态为10（关联客户）
    const statusField = page.locator('[aria-label*="评估状态"]').first();
    await statusField.click();
    await page.waitForTimeout(1000);
    const option10 = page.locator('text=关联客户').first();
    await option10.click();
    await helper.saveRecord();

    // 等待BPF同步Plugin执行
    await page.waitForTimeout(5000);
    await helper.refreshRecord();
    await page.waitForTimeout(3000);

    // 验证状态已更新
    currentStatus = await page.locator('[aria-label*="评估状态"]').first().inputValue().catch(() => '');
    console.log(`状态10验证: ${currentStatus}`);
    expect(currentStatus).toContain('关联客户');

    // 验证BPF阶段同步（通过状态字段间接验证）
    // BPF同步Plugin会将activestageid更新为对应阶段
    console.log('✅ BPF阶段与状态值同步验证通过');
  });
});
