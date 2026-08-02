const { chromium } = require('/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/SanyD365Project/Tests/node_modules/playwright-core');

(async () => {
  const profileDir = '/Users/peterqiu/Library/Caches/ms-playwright-mcp/mcp-chrome-bf9ee5c';
  const executablePath = '/Users/peterqiu/Library/Caches/ms-playwright/chromium-1217/chrome-mac-arm64/Google Chrome for Testing.app/Contents/MacOS/Google Chrome for Testing';
  const context = await chromium.launchPersistentContext(profileDir, {
    headless: false,
    executablePath,
    viewport: { width: 1600, height: 1000 },
    args: ['--disable-blink-features=AutomationControlled'],
  });
  const page = context.pages()[0] || await context.newPage();
  const url = 'https://dev1.crm5.dynamics.com/main.aspx?appid=4268989a-aede-45e0-93ec-f07b02f3e383&pagetype=entityrecord&etn=mcs_credit_record&id=bc1e42c2-a78d-f111-8077-6045bd1c0e3b';
  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 90000 });

  // 等待表单标题出现 SCO202608010001
  try {
    await page.waitForFunction(() => document.title.includes('SCO202608010001') || document.body.innerText.includes('SCO202608010001'), null, { timeout: 60000 });
    console.log('FORM_TITLE_OK:', document.title || '(innerText matched)');
    console.log('PAGE_TITLE:', await page.title());
  } catch (e) {
    console.log('FORM_TITLE_TIMEOUT. page title =', await page.title());
  }
  await page.waitForTimeout(5000);

  // 找命令栏“更多命令”(…) 按钮
  const overflowSelectors = [
    'button[aria-label="更多命令"]',
    'button[data-id="OverflowButton"]',
    'button[aria-label*="More commands"]',
    'button[aria-label*="更多"]',
  ];
  let overflowBtn = null;
  for (const sel of overflowSelectors) {
    const el = page.locator(sel).first();
    if (await el.count() > 0 && await el.isVisible().catch(() => false)) {
      overflowBtn = el;
      console.log('OVERFLOW_BTN_FOUND:', sel);
      break;
    }
  }
  if (!overflowBtn) {
    // 列出命令栏所有按钮的 aria-label 便于诊断
    const labels = await page.locator('button[aria-label]').evaluateAll(els => els.map(e => e.getAttribute('aria-label')));
    console.log('ALL_BUTTON_LABELS:', JSON.stringify(labels));
    await page.screenshot({ path: '/Users/peterqiu/Work/AIWorkSpace/SanYi/Backups/TempTest/coface-no-overflow.png' });
    await context.close();
    return;
  }
  await overflowBtn.click();
  await page.waitForTimeout(2000);

  // 统计菜单中 Coface 下单 项
  const items = page.locator('[role="menuitem"], [role="menu"] li, .ms-ContextualMenu-item');
  const count = await items.count();
  const results = [];
  for (let i = 0; i < count; i++) {
    const it = items.nth(i);
    const text = (await it.innerText().catch(() => '')).trim();
    if (!text) continue;
    const ariaLabel = await it.getAttribute('aria-label').catch(() => null);
    const title = await it.getAttribute('title').catch(() => null);
    const hasIcon = await it.locator('img, svg, i[data-icon-name], span[data-icon-name]').count().catch(() => 0);
    results.push({ text: text.replace(/\s+/g, ' ').slice(0, 120), ariaLabel, title, hasIcon });
  }
  console.log('MENU_ITEM_COUNT:', results.length);
  console.log('MENU_ITEMS_JSON:', JSON.stringify(results, null, 2));
  const coface = results.filter(r => (r.text || '').includes('Coface') || (r.ariaLabel || '').includes('Coface'));
  console.log('COFACE_COUNT:', coface.length);
  console.log('COFACE_ITEMS_JSON:', JSON.stringify(coface, null, 2));
  await page.screenshot({ path: '/Users/peterqiu/Work/AIWorkSpace/SanYi/Backups/TempTest/coface-overflow-menu.png' });

  // 只读：关闭菜单（按 Esc），不点击任何项
  await page.keyboard.press('Escape');
  await context.close();
})().catch(e => { console.error('SCRIPT_ERROR:', e.message); process.exit(1); });
