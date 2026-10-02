import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  const page = await context.newPage();

  page.on('request', request => {
    const url = request.url();
    if (url.includes('/api/video/keys')) {
      console.log('KEY REQUEST CAUGHT!');
      console.log('URL:', url);
      console.log('HEADERS:', request.headers());
    }
  });

  console.log('Navigating to http://localhost:5173 ...');
  await page.goto('http://localhost:5173');
  
  // Wait for 5 seconds to let the player load and request the key
  await page.waitForTimeout(5000);

  await browser.close();
})();
