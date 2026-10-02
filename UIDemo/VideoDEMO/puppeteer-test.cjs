const puppeteer = require('puppeteer');

(async () => {
  const browser = await puppeteer.launch({ headless: 'new', args: ['--no-sandbox', '--disable-setuid-sandbox'] });
  const page = await browser.newPage();
  
  page.on('request', request => {
    if (request.url().includes('api/video/keys') || request.url().includes('key')) {
      console.log('KEY REQUEST URL:', request.url());
      console.log('KEY REQUEST HEADERS:', request.headers());
    }
  });

  page.on('response', response => {
    if (response.url().includes('api/video/keys') || response.url().includes('key')) {
      console.log('KEY RESPONSE STATUS:', response.status());
    }
  });

  page.on('console', msg => {
    console.log('PAGE LOG:', msg.text());
  });

  console.log("Navigating to video page...");
  await page.goto('http://localhost:5173/video/2c584771-c78b-44d6-914e-8ab1f60b0346', { waitUntil: 'networkidle2' });
  
  await new Promise(r => setTimeout(r, 8000));
  
  await browser.close();
})();
