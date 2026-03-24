import { chromium } from 'playwright';

// jobs.ch search uses the 'term' query parameter in the URL.
// Navigate directly to the search results URL rather than interacting with the
// search form so results are available immediately on page load.
const BASE_SEARCH = process.env.SEARCH_URL || 'https://www.jobs.ch/en/vacancies/';
const KEYWORDS = (process.env.KEYWORDS || 'Erzieher').trim();
const LOCATION = (process.env.LOCATION || '').trim();

function normalize(value) {
  return (value || '').replace(/\s+/g, ' ').trim();
}

function resolveUrl(base, href) {
  if (!href) return '';
  try { return new URL(href, base).href; } catch { return href; }
}

// Build the full search URL with keywords and location embedded as query params.
const searchUrl = (() => {
  const u = new URL(BASE_SEARCH);
  if (KEYWORDS) u.searchParams.set('term', KEYWORDS);
  if (LOCATION) u.searchParams.set('location', LOCATION);
  return u.toString();
})();

const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage();

  await page.goto(searchUrl, { waitUntil: 'domcontentloaded', timeout: 30000 });

  // Wait for job result cards to appear. Fall back after 15 s if none show up.
  await page.waitForSelector('[data-cy="serp-item"]', { timeout: 15000 }).catch(() => {});

  const jobs = await page.evaluate(() => {
    const normalize = v => (v || '').replace(/\s+/g, ' ').trim();

    return Array.from(document.querySelectorAll('[data-cy="serp-item"]')).map(card => {
      const link = card.querySelector('a[data-cy="job-link"]') || card.querySelector('a[href]');
      const title = normalize(
        link?.getAttribute('title') ||
        card.querySelector('[class*="title"], [class*="heading"], h2, h3')?.textContent ||
        link?.textContent || ''
      );
      const url = link?.getAttribute('href') || '';
      const ps = Array.from(card.querySelectorAll('p')).map(el => normalize(el.textContent)).filter(Boolean);
      return {
        title,
        url,
        company: ps[4] || ps[0] || '',
        location: ps[1] || '',
        description: [ps[2], ps[3]].filter(Boolean).join(' · ')
      };
    }).filter(job => job.title && job.url);
  });

  const base = new URL(BASE_SEARCH).origin;
  const unique = Array.from(
    new Map(jobs.map(job => {
      const full = resolveUrl(base, job.url);
      return [full, {
        title: normalize(job.title),
        url: full,
        company: normalize(job.company),
        location: normalize(job.location),
        description: normalize(job.description)
      }];
    })).values()
  );

  console.log(JSON.stringify(unique));
} finally {
  await browser.close();
}
