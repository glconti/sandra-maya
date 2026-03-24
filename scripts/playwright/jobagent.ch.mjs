import { chromium } from 'playwright';
(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const searchUrl = process.env.SEARCH_URL;

  await page.goto(searchUrl, { waitUntil: 'networkidle', timeout: 30000 });
  await page.waitForTimeout(2000);

  const jobs = await page.evaluate(() => {
    const items = Array.from(document.querySelectorAll('[data-job], .job, .search-result, .result, article'));
    return items.slice(0,50).map(el => {
      const link = el.querySelector('a[href]');
      const title = el.querySelector('h2, h3, .title');
      const company = el.querySelector('.company, .employer');
      const loc = el.querySelector('.location, .ort, .city');
      const desc = el.querySelector('.description, p');
      return {
        title: title?.textContent?.trim() || link?.textContent?.trim() || '',
        company: company?.textContent?.trim() || '',
        location: loc?.textContent?.trim() || '',
        url: link?.href || '',
        description: desc?.textContent?.trim() || ''
      };
    }).filter(j => j.title && j.url);
  });

  console.log(JSON.stringify(jobs));
  await browser.close();
})();
