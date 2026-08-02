const { chromium } = require('/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/SanyD365Project/Tests/node_modules/playwright-core');
(async () => {
  const profileDir = '/Users/peterqiu/Library/Caches/ms-playwright-mcp/mcp-chrome-bf9ee5c';
  const executablePath = '/Users/peterqiu/Library/Caches/ms-playwright/chromium-1217/chrome-mac-arm64/Google Chrome for Testing.app/Contents/MacOS/Google Chrome for Testing';
  const context = await chromium.launchPersistentContext(profileDir, {
    headless: false, executablePath, viewport: { width: 1600, height: 1000 },
    args: ['--disable-blink-features=AutomationControlled'],
  });
  const page = context.pages()[0] || await context.newPage();
  const url = 'https://dev1.crm5.dynamics.com/main.aspx?appid=4268989a-aede-45e0-93ec-f07b02f3e383&pagetype=entityrecord&etn=mcs_credit_record&id=bc1e42c2-a78d-f111-8077-6045bd1c0e3b';
  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 90000 });
  await page.waitForFunction(() => (document.title || '').includes('SCO202608010001'), null, { timeout: 60000 }).catch(()=>{});
  await page.waitForTimeout(4000);
  const btn = page.locator('button[data-id="OverflowButton"]').first();
  await btn.click();
  await page.waitForTimeout(2500);

  const data = await page.evaluate(() => {
    const els = Array.from(document.querySelectorAll('[role="menuitem"]'));
    return els.map(el => {
      const r = el.getBoundingClientRect();
      const visible = r.width > 0 && r.height > 0 && getComputedStyle(el).visibility !== 'hidden';
      const img = el.querySelector('img');
      const iconSpan = el.querySelector('i[data-icon-name], span[data-icon-name], svg');
      return {
        text: (el.innerText || '').trim().replace(/\s+/g,' ').slice(0,100),
        ariaLabel: el.getAttribute('aria-label'),
        title: el.getAttribute('title'),
        visible, w: Math.round(r.width), h: Math.round(r.height),
        imgSrc: img ? img.getAttribute('src') : null,
        iconName: iconSpan ? (iconSpan.getAttribute('data-icon-name') || 'svg') : null,
        id: el.id || null,
      };
    }).filter(x => x.text);
  });
  console.log('ALL_MENUITEMS:', JSON.stringify(data, null, 1));
  const cofaceVisible = data.filter(x => x.visible && x.text === 'Coface 下单');
  console.log('VISIBLE_COFACE_COUNT:', cofaceVisible.length);
  console.log('VISIBLE_COFACE:', JSON.stringify(cofaceVisible, null, 1));
  await page.screenshot({ path: '/Users/peterqiu/Work/AIWorkSpace/SanYi/Backups/TempTest/coface-overflow-menu.png' });
  await page.keyboard.press('Escape');
  await context.close();
})().catch(e => { console.error('SCRIPT_ERROR:', e.message); process.exit(1); });
