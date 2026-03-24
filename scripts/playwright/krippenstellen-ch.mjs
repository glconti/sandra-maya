import { chromium } from 'playwright';

const SEARCH_URL = process.env.SEARCH_URL || 'https://krippenstellen.ch/de/inserate?reset';

function normalize(value) {
  return (value || '').replace(/\s+/g, ' ').trim();
}

function resolveUrl(baseUrl, href) {
  if (!href) return '';
  try {
    return new URL(href, baseUrl).href;
  } catch {
    return href;
  }
}

const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage();
  await page.goto(SEARCH_URL, { waitUntil: 'networkidle', timeout: 30000 });
  await page.waitForTimeout(1000);

  const jobs = await page.evaluate(() => {
    const normalize = value => (value || '').replace(/\s+/g, ' ').trim();

    const cards = Array.from(document.querySelectorAll('div.inserate > a[href*="/de/frontend/inserate/"]'));

    return cards.map(card => {
      const listing = card.querySelector('.inserat');
      const title = normalize(listing?.dataset?.funktion || card.querySelector('[data-funktion]')?.getAttribute('data-funktion') || '').replace(/>+$/, '');
      const companyLocation = normalize(listing?.dataset?.kantonkrippe || '');
      const splitIndex = companyLocation.lastIndexOf(',');
      const company = splitIndex > 0 ? normalize(companyLocation.slice(0, splitIndex)) : companyLocation;
      const location = splitIndex > 0 ? normalize(companyLocation.slice(splitIndex + 1)) : '';

      return {
        title,
        url: card.getAttribute('href') || '',
        company,
        location,
        description: normalize(card.textContent || ''),
        sourcePostingId: normalize(listing?.dataset?.nr || '')
      };
    }).filter(job => job.title && job.url);
  });

  const unique = Array.from(
    new Map(jobs.map(job => [resolveUrl(SEARCH_URL, job.url), {
      title: normalize(job.title),
      url: resolveUrl(SEARCH_URL, job.url),
      company: normalize(job.company),
      location: normalize(job.location),
      description: normalize(job.description),
      sourcePostingId: normalize(job.sourcePostingId)
    }])).values()
  );

  console.log(JSON.stringify(unique));
} finally {
  await browser.close();
}
