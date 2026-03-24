import { chromium } from 'playwright';

// schuljobs.ch search: embed keywords and location directly in the URL via
// query params so results are present on the initial page load without form interaction.
const BASE_SEARCH = process.env.SEARCH_URL || 'https://www.schuljobs.ch/suche';
const KEYWORDS = (process.env.KEYWORDS || '').trim();
const LOCATION = (process.env.LOCATION || '').trim();

function normalize(value) {
  return (value || '').replace(/\s+/g, ' ').trim();
}

function resolveUrl(base, href) {
  if (!href) return '';
  try { return new URL(href, base).href; } catch { return href; }
}

const searchUrl = (() => {
  const u = new URL(BASE_SEARCH);
  if (KEYWORDS) u.searchParams.set('what', KEYWORDS);
  if (LOCATION) u.searchParams.set('where', LOCATION);
  return u.toString();
})();

const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage();
  await page.goto(searchUrl, { waitUntil: 'networkidle', timeout: 30000 });
  await page.waitForTimeout(1000);

  const jobs = await page.evaluate(() => {
    const normalize = value => (value || '').replace(/\s+/g, ' ').trim();

    const cards = Array.from(document.querySelectorAll('div.content-container'))
      .filter(card => card.querySelector('a.js-joboffer-detail, a[data-cy="joboffer-detail"]'));

    return cards.map(card => {
      const link = card.querySelector('a.js-joboffer-detail, a[data-cy="joboffer-detail"]');
      const title = normalize(link?.textContent || '');
      const addition = normalize(card.querySelector('p.addition')?.textContent || '');
      const parts = addition.split('·').map(part => normalize(part)).filter(Boolean);
      const workload = normalize(card.querySelector('span.workload')?.textContent || '');
      const date = normalize(card.querySelector('time.date')?.textContent || '');

      return {
        title,
        url: link?.getAttribute('href') || '',
        company: parts[2] || '',
        location: parts[1] || '',
        description: [workload, date].filter(Boolean).join(' · ')
      };
    }).filter(job => job.title && job.url);
  });

  const unique = Array.from(
    new Map(jobs.map(job => [resolveUrl(BASE_SEARCH, job.url), {
      title: normalize(job.title),
      url: resolveUrl(BASE_SEARCH, job.url),
      company: normalize(job.company),
      location: normalize(job.location),
      description: normalize(job.description)
    }])).values()
  );

  console.log(JSON.stringify(unique));
} finally {
  await browser.close();
}
