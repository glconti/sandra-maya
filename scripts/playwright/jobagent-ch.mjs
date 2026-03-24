const KEYWORDS = (process.env.KEYWORDS || '').trim();
const LOCATION = (process.env.LOCATION || '').trim();
const FALLBACK_TERMS = ['jobs', 'lehrperson', 'erzieherin', 'fachperson betreuung', 'kindergartenlehrperson', 'kindergarten', 'kita'];

function normalize(value) {
  return (value || '').replace(/\s+/g, ' ').trim();
}

function decodeDuckDuckGoUrl(url) {
  if (!url) return '';
  try {
    const parsed = new URL(url);
    return parsed.searchParams.get('uddg') || url;
  } catch {
    return url;
  }
}

function decodeMarkdownText(value) {
  return normalize(value)
    .replace(/\[Image\s+\d+\]\([^)]+\)/g, '')
    .replace(/\[([^\]]+)\]\(([^)]+)\)/g, '$1')
    .replace(/&amp;/g, '&')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/&apos;/g, "'")
    .replace(/&nbsp;/g, ' ');
}

function buildQuery() {
  const terms = KEYWORDS ? [KEYWORDS] : FALLBACK_TERMS;
  const termExpr = terms.length === 1 ? terms[0] : `(${terms.join(' OR ')})`;
  const parts = ['site:jobagent.ch/job/', termExpr];
  if (LOCATION) parts.push(LOCATION);
  return parts.join(' ');
}

async function fetchSearchMarkdown(query) {
  const url = `https://r.jina.ai/http://duckduckgo.com/html/?q=${encodeURIComponent(query)}`;
  const res = await fetch(url, {
    headers: {
      'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36'
    }
  });

  const text = await res.text();
  if (!res.ok) {
    throw new Error(`DuckDuckGo search proxy failed with status ${res.status}`);
  }

  return text;
}

function parseResults(markdown) {
  const results = [];
  const sections = markdown.split(/\n## \[/).slice(1);

  for (const section of sections) {
    const block = '## [' + section;
    const titleMatch = block.match(/^## \[([^\]]+)\]\(([^)]+)\)/m);
    if (!titleMatch) continue;

    const title = decodeMarkdownText(titleMatch[1]);
    const titleUrl = decodeDuckDuckGoUrl(titleMatch[2]);
    if (!titleUrl.includes('jobagent.ch/job/')) continue;

    const links = [...block.matchAll(/\[([^\]]+)\]\((https?:[^)]+)\)/g)];
    const lastLinkText = links.length > 0 ? links[links.length - 1][1] : '';
    const snippet = decodeMarkdownText(lastLinkText);

    results.push({
      title,
      url: titleUrl,
      company: '',
      location: '',
      description: snippet
    });
  }

  return results;
}

const markdown = await fetchSearchMarkdown(buildQuery());
const jobs = parseResults(markdown);

const unique = Array.from(
  new Map(jobs.map(job => [job.url, {
    title: normalize(job.title),
    url: job.url,
    company: normalize(job.company),
    location: normalize(job.location),
    description: normalize(job.description)
  }])).values()
);

console.log(JSON.stringify(unique));
