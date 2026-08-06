#!/usr/bin/env node
/**
 * site-link-checker / crawl.mjs
 *
 * Crawls a website starting from a base URL using headless Chromium
 * (so client-side-rendered / SPA links are discovered too), checks every
 * same-site page's HTTP status, records console/page errors, and writes
 * a Markdown report.
 *
 * Usage:
 *   node crawl.mjs <baseUrl> [options]
 *
 * Options:
 *   --max-pages <n>       Max number of pages to visit (default 300)
 *   --max-depth <n>       Max link-hops from the base URL (default 6)
 *   --output <file>       Report output path (default report.md)
 *   --concurrency <n>     Parallel page checks (default 4)
 *   --timeout <ms>        Per-page navigation timeout (default 15000)
 *   --include-external    Also HEAD-check external links (no crawl-in)
 *
 * Loca icin eklenenler
 * --------------------
 *   --channel <ad>          Playwright'in indirdigi tarayici yerine sistemdeki
 *                           tarayiciyi kullanir (orn. chrome). Bu depoda
 *                           `playwright install` hic yapilmadi; Swagger
 *                           goruntuleri de sistem Chrome'uyla aliniyor.
 *
 *   --spa-404 <secici>      TEK SAYFALI UYGULAMALAR ICIN SART. Vite ve nginx
 *                           tanimsiz her adrese index.html donuyor, yani HTTP
 *                           durumu her zaman 200. Bu bayrak olmadan tarayici
 *                           "her sayfa saglikli" der ve kirik rotalari hic
 *                           gormezdi — nitekim /etkinlikler/:id rotasi yoktu
 *                           ve kimse fark etmemisti. Secici sayfada
 *                           bulunursa sayfa KIRIK sayiliyor.
 *
 *   --login <eposta:sifre>  Korumali sayfalari da gezebilmek icin oturum
 *                           acar. Oturum acilmadan taranirsa korumali her
 *                           adres giris ekranina donuyor ve rapor "her sey
 *                           yolunda" diyor — oysa hicbiri denenmemis oluyor.
 *   --api <kok>             Giris istegi icin API koku (varsayilan
 *                           <baseUrl>/api/v1).
 */

import { chromium } from 'playwright';
import { writeFile } from 'node:fs/promises';
import { URL } from 'node:url';

// ---------- CLI parsing ----------
function parseArgs(argv) {
  const args = { maxPages: 300, maxDepth: 6, output: 'report.md', concurrency: 4, timeout: 15000, includeExternal: false, channel: null, spa404: null, login: null, api: null };
  const positional = [];
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--max-pages') args.maxPages = parseInt(argv[++i], 10);
    else if (a === '--max-depth') args.maxDepth = parseInt(argv[++i], 10);
    else if (a === '--output') args.output = argv[++i];
    else if (a === '--concurrency') args.concurrency = parseInt(argv[++i], 10);
    else if (a === '--timeout') args.timeout = parseInt(argv[++i], 10);
    else if (a === '--include-external') args.includeExternal = true;
    else if (a === '--channel') args.channel = argv[++i];
    else if (a === '--spa-404') args.spa404 = argv[++i];
    else if (a === '--login') args.login = argv[++i];
    else if (a === '--api') args.api = argv[++i];
    else positional.push(a);
  }
  args.baseUrl = positional[0];
  return args;
}

const args = parseArgs(process.argv.slice(2));
if (!args.baseUrl) {
  console.error('Usage: node crawl.mjs <baseUrl> [--max-pages N] [--max-depth N] [--output report.md] [--concurrency N] [--timeout ms] [--include-external]');
  process.exit(1);
}

const baseUrl = new URL(args.baseUrl);
const baseHost = baseUrl.hostname;

const SKIP_SCHEMES = new Set(['mailto:', 'tel:', 'javascript:', 'data:']);
const ASSET_EXT = /\.(png|jpe?g|gif|svg|webp|ico|css|js|mjs|woff2?|ttf|eot|mp4|mp3|zip|pdf|xml|json)(\?.*)?$/i;

function normalizeUrl(href, base) {
  try {
    const u = new URL(href, base);
    u.hash = '';
    return u;
  } catch {
    return null;
  }
}

// ---------- Crawl state ----------
const visited = new Set();
const queue = [{ url: baseUrl.toString(), depth: 0, foundOn: null }];
const pageResults = []; // { url, status, ok, foundOn, errors: [], redirected }
const externalLinks = new Map(); // url -> Set(foundOn)
const brokenAssets = [];

async function checkExternalOrAsset(url, foundOn) {
  try {
    const res = await fetch(url, { method: 'HEAD', redirect: 'follow', signal: AbortSignal.timeout(args.timeout) });
    if (!res.ok) brokenAssets.push({ url, status: res.status, foundOn });
  } catch (e) {
    brokenAssets.push({ url, status: 'ERROR: ' + e.message, foundOn });
  }
}

/**
 * Oturum acar ve tarayiciya yazilacak localStorage kaydini uretir.
 *
 * Korumali sayfalar oturum olmadan giris ekranina donuyor. Oturumsuz
 * tarama "her sey yolunda" der ama korumali hicbir sayfa gercekte
 * denenmemis olur.
 *
 * Uygulama oturumu zustand persist ile "loca-oturum" anahtarinda
 * tutuyor; access token bilerek diske yazilmiyor, acilista refresh
 * token'dan yenileniyor. Bu yuzden buraya da yalnizca refresh token
 * yaziliyor — uygulamanin kendi akisiyla ayni.
 *
 * HER BAGLAM ICIN AYRI CAGRILIYOR. Tek bir token butun baglamlara
 * yazildiginda ilk sayfa onu yeniliyor ve token doniyor; kalan
 * baglamlar artik gecersiz bir token sunuyor. Sonuc: her sayfada bir
 * 401 ve sunucunun yeniden kullanim tespitinin tetiklenmesi — yani
 * tarayicinin kendi urettigi, uygulamada olmayan bir "hata".
 */
async function oturumAc() {
  const ayrac = args.login.indexOf(':');
  const eposta = args.login.slice(0, ayrac);
  const sifre = args.login.slice(ayrac + 1);

  const kok = args.api ?? new URL('/api/v1', baseUrl).toString();

  const cevap = await fetch(`${kok}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: eposta, password: sifre }),
  });

  if (!cevap.ok) {
    throw new Error(`Giris basarisiz (${cevap.status}). Adres: ${kok}/auth/login`);
  }

  const veri = await cevap.json();

  return {
    state: { refreshToken: veri.refreshToken, kullanici: veri.user },
    version: 0,
  };
}

async function crawlPage(browser, item) {
  const { url, depth, foundOn } = item;
  if (visited.has(url)) return [];
  visited.add(url);

  const context = await browser.newContext();

  if (args.login) {
    // Sayfa acilmadan ONCE yaziliyor: uygulama ilk render'da oturumu
    // localStorage'dan okuyor, sonra yazmak gec kalirdi.
    await context.addInitScript(
      ([anahtar, deger]) => window.localStorage.setItem(anahtar, deger),
      ['loca-oturum', JSON.stringify(await oturumAc())],
    );
  }

  const page = await context.newPage();
  const consoleErrors = [];

  page.on('console', (msg) => {
    if (msg.type() === 'error') consoleErrors.push(msg.text());
  });
  page.on('pageerror', (err) => {
    consoleErrors.push('Uncaught exception: ' + err.message);
  });

  let status = null;
  let ok = false;
  let newLinks = [];

  try {
    const response = await page.goto(url, { waitUntil: 'networkidle', timeout: args.timeout });
    status = response ? response.status() : null;
    ok = response ? response.ok() : false;

    // TEK SAYFALI UYGULAMADA HTTP DURUMU YALAN SOYLUYOR.
    // Vite ve nginx tanimsiz her adrese index.html donuyor; durum her
    // zaman 200. Kirik bir rota ancak uygulamanin KENDI 404 ekranini
    // gostermesinden anlasiliyor. Bu kontrol olmasaydi tarayici butun
    // kirik baglantilari "saglikli" diye raporlardi.
    if (ok && args.spa404) {
      const bulunamadi = await page.locator(args.spa404).count();

      if (bulunamadi > 0) {
        status = '200 (uygulama 404)';
        ok = false;
      }
    }

    if (ok) {
      const hrefs = await page.$$eval('a[href]', (as) => as.map((a) => a.getAttribute('href')));
      for (const href of hrefs) {
        if (!href) continue;
        const scheme = href.split(':')[0] + ':';
        if (SKIP_SCHEMES.has(scheme)) continue;
        const abs = normalizeUrl(href, url);
        if (!abs) continue;
        const absStr = abs.toString();

        if (abs.hostname === baseHost) {
          if (ASSET_EXT.test(abs.pathname)) {
            newLinks.push({ type: 'asset', url: absStr, foundOn: url });
          } else if (depth < args.maxDepth) {
            newLinks.push({ type: 'page', url: absStr, foundOn: url, depth: depth + 1 });
          }
        } else {
          if (!externalLinks.has(absStr)) externalLinks.set(absStr, new Set());
          externalLinks.get(absStr).add(url);
        }
      }
    }
  } catch (e) {
    status = 'ERROR';
    ok = false;
    consoleErrors.push('Navigation error: ' + e.message);
  } finally {
    await context.close();
  }

  pageResults.push({ url, status, ok, foundOn, errors: consoleErrors, depth });
  return newLinks;
}

async function main() {
  console.log(`Crawling ${baseUrl.toString()} (max ${args.maxPages} pages, depth ${args.maxDepth})...`);

  if (args.login) {
    // Bir kez burada da deneniyor: kimlik bilgisi yanlissa taramanin
    // sonunda "her sey yolunda" raporu gormek yerine hemen ogrenilsin.
    const deneme = await oturumAc();
    console.log(`  oturum acildi: ${deneme.state.kullanici?.email} (${deneme.state.kullanici?.roles?.join(', ')})`);
  }

  // channel verilmisse sistemdeki tarayici kullaniliyor; bu depoda
  // `playwright install` hic yapilmadi.
  const browser = await chromium.launch(args.channel ? { channel: args.channel } : {});
  const pending = [...queue];
  const assetQueue = [];

  while (pending.length > 0 && visited.size < args.maxPages) {
    const batch = pending.splice(0, args.concurrency).filter((i) => !visited.has(i.url));
    if (batch.length === 0) continue;

    const results = await Promise.all(batch.map((item) => crawlPage(browser, item)));
    for (const links of results) {
      for (const link of links) {
        if (link.type === 'page' && !visited.has(link.url)) {
          pending.push(link);
        } else if (link.type === 'asset') {
          assetQueue.push(link);
        }
      }
    }
    console.log(`  visited ${visited.size} pages, ${pending.length} queued...`);
  }

  await browser.close();

  // Check same-site assets (images, css, js, pdfs, etc.) with lightweight HEAD requests
  const seenAssets = new Set();
  for (const asset of assetQueue) {
    if (seenAssets.has(asset.url)) continue;
    seenAssets.add(asset.url);
    await checkExternalOrAsset(asset.url, asset.foundOn);
  }

  // Optionally check external links too
  if (args.includeExternal) {
    for (const [url, foundOnSet] of externalLinks) {
      await checkExternalOrAsset(url, [...foundOnSet][0]);
    }
  }

  await writeReport();
}

async function writeReport() {
  const broken = pageResults.filter((p) => !p.ok);
  const withErrors = pageResults.filter((p) => p.errors.length > 0);
  const okCount = pageResults.length - broken.length;

  let md = `# Site Crawl Report\n\n`;
  md += `**Base URL:** ${baseUrl.toString()}\n`;
  md += `**Generated:** ${new Date().toISOString()}\n\n`;

  md += `## Summary\n\n`;
  md += `| Metric | Count |\n|---|---|\n`;
  md += `| Pages visited | ${pageResults.length} |\n`;
  md += `| Pages OK | ${okCount} |\n`;
  md += `| Pages broken | ${broken.length} |\n`;
  md += `| Pages with console errors | ${withErrors.length} |\n`;
  md += `| Same-site assets checked | ${brokenAssets.length + (pageResults.length ? 0 : 0)} |\n`;
  md += `| Broken assets | ${brokenAssets.filter(a => typeof a.status === 'number' ? a.status >= 400 : true).length} |\n`;
  md += `| External links found | ${externalLinks.size} |\n\n`;

  md += `## Broken Pages\n\n`;
  if (broken.length === 0) {
    md += `None found. ✅\n\n`;
  } else {
    md += `| URL | Status | Found on |\n|---|---|---|\n`;
    for (const p of broken) {
      md += `| ${p.url} | ${p.status} | ${p.foundOn ?? '(start URL)'} |\n`;
    }
    md += `\n`;
  }

  md += `## Broken Assets / External Resources\n\n`;
  const brokenOnly = brokenAssets.filter((a) => typeof a.status !== 'number' || a.status >= 400);
  if (brokenOnly.length === 0) {
    md += `None found. ✅\n\n`;
  } else {
    md += `| URL | Status | Found on |\n|---|---|---|\n`;
    for (const a of brokenOnly) {
      md += `| ${a.url} | ${a.status} | ${a.foundOn ?? '?'} |\n`;
    }
    md += `\n`;
  }

  md += `## Console / JavaScript Errors\n\n`;
  if (withErrors.length === 0) {
    md += `None found. ✅\n\n`;
  } else {
    for (const p of withErrors) {
      md += `### ${p.url}\n\n`;
      for (const e of p.errors) {
        md += `- ${e.replace(/\n/g, ' ')}\n`;
      }
      md += `\n`;
    }
  }

  md += `## All Pages (appendix)\n\n`;
  md += `| URL | Status | Depth | Errors | Found on |\n|---|---|---|---|---|\n`;
  for (const p of pageResults.sort((a, b) => a.depth - b.depth)) {
    md += `| ${p.url} | ${p.ok ? '✅ ' + p.status : '❌ ' + p.status} | ${p.depth} | ${p.errors.length} | ${p.foundOn ?? '(start URL)'} |\n`;
  }
  md += `\n`;

  if (externalLinks.size > 0 && !args.includeExternal) {
    md += `## External Links Found (not checked — rerun with --include-external to verify status)\n\n`;
    md += `| URL | Found on (example) |\n|---|---|\n`;
    for (const [url, foundOnSet] of externalLinks) {
      md += `| ${url} | ${[...foundOnSet][0]} |\n`;
    }
    md += `\n`;
  }

  await writeFile(args.output, md, 'utf-8');
  console.log(`\nReport written to ${args.output}`);
  console.log(`${okCount}/${pageResults.length} pages OK, ${broken.length} broken, ${withErrors.length} with console errors.`);
}

main().catch((err) => {
  console.error('Fatal error:', err);
  process.exit(1);
});
