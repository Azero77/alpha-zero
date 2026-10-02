const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch();
  const context = await browser.newContext();
  const page = await context.newPage();
  
  page.on('request', request => {
    if (request.url().includes('/api/video/keys')) {
      console.log('KEY REQUEST URL:', request.url());
      console.log('KEY REQUEST HEADERS:', request.headers());
    }
  });

  await page.goto('http://localhost:5173');
  
  // Wait for the key request or timeout after 10s
  await page.waitForTimeout(10000);
  await browser.close();
})();
