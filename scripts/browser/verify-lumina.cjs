const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium, firefox, webkit } = require('playwright');

const [address = 'http://localhost:8098/', engine = 'edge'] = process.argv.slice(2);
const base = new URL(address);
const screenshots = path.resolve(__dirname, '../../artifacts/browser-checks', engine);
const imageIdentity = (reference, currentPage) => new URL(reference, currentPage).pathname.split('/').slice(0, -1).join('/');

async function verify() {
  assert.ok(['localhost', '127.0.0.1', '[::1]'].includes(base.hostname), 'Only local previews are allowed');
  assert.ok(['edge', 'chromium', 'firefox', 'webkit'].includes(engine));
  const browser = engine === 'edge'
    ? await chromium.launch({ channel: 'msedge', headless: true })
    : await ({ chromium, firefox, webkit }[engine]).launch({ headless: true });
  const results = [];
  try {
    await fs.mkdir(screenshots, { recursive: true });
    for (const settings of [
      { name: 'desktop-light', width: 1440, height: 1000, dark: false, js: true },
      { name: 'desktop-dark-nojs', width: 1440, height: 1000, dark: true, js: false },
      { name: 'mobile-light-nojs', width: 390, height: 844, dark: false, js: false },
      { name: 'mobile-dark', width: 390, height: 844, dark: true, js: true },
    ]) {
      console.error(`Checking ${engine}: ${settings.name}`);
      const context = await browser.newContext({
        viewport: { width: settings.width, height: settings.height },
        colorScheme: settings.dark ? 'dark' : 'light',
        javaScriptEnabled: settings.js,
        reducedMotion: settings.js ? 'no-preference' : 'reduce',
      });
      context.setDefaultTimeout(15000);
      context.setDefaultNavigationTimeout(15000);
      try {
        const failedResponses = [];
        await context.route('**/*', route => {
          const target = new URL(route.request().url());
          return target.origin === base.origin || target.protocol === 'data:' ? route.continue() : route.abort();
        });
        const page = await context.newPage();
        page.on('response', response => {
          if (response.status() >= 400) failedResponses.push(`${response.status()} ${response.url()}`);
        });
        await page.setContent('<script>globalThis.probeExecuted = true;</script>');
        assert.equal(await page.evaluate(() => globalThis.probeExecuted === true), settings.js, 'Actual JS setting');
        const visit = async route => {
          console.error(`  Visiting ${route}`);
          const response = await page.goto(new URL(route, base).href, { waitUntil: 'networkidle' });
          assert.equal(response.status(), 200);
          const environment = await page.evaluate(() => ({
            width: innerWidth,
            dark: matchMedia('(prefers-color-scheme: dark)').matches,
            reduced: matchMedia('(prefers-reduced-motion: reduce)').matches,
            overflow: document.documentElement.scrollWidth > innerWidth,
          }));
          assert.equal(environment.width, settings.width);
          assert.equal(environment.dark, settings.dark);
          assert.equal(environment.reduced, !settings.js);
          assert.equal(environment.overflow, false, 'No horizontal overflow');
        };

        await visit('galleries/canon-only/');
        const triggers = page.locator('button[command="show-modal"]');
        const count = await triggers.count();
        assert.ok(count > 1, 'Lightbox fixture needs multiple photos');
        const ids = await page.locator('[id]').evaluateAll(elements => elements.map(element => element.id));
        assert.equal(new Set(ids).size, ids.length);
        for (let index = 0; index < count; index++) {
          const trigger = triggers.nth(index);
          const id = await trigger.getAttribute('commandfor');
          await trigger.scrollIntoViewIfNeeded();
          await page.waitForFunction(position => {
            const image = document.querySelectorAll('button[command="show-modal"]')[position].parentElement.querySelector('picture img');
            return image.complete && image.naturalWidth > 0;
          }, index);
          const thumbnailSource = await trigger.locator('..').locator('picture img').first().evaluate(image => image.currentSrc);
          await trigger.focus();
          const opener = await trigger.getAttribute('id');
          await page.keyboard.press('Enter');
          const dialog = page.locator(`dialog[id=${JSON.stringify(id)}][open]`);
          await dialog.waitFor({ state: 'visible' });
          assert.equal(await page.locator('dialog[open]').count(), 1);
          assert.equal(await dialog.evaluate(element => element.matches(':modal')), true);
          await page.waitForFunction(dialogId => {
            const image = document.getElementById(dialogId).querySelector('picture img');
            return image.complete && image.naturalWidth > 0;
          }, id);
          const displayedSource = await dialog.locator('picture img').evaluate(image => image.currentSrc);
          assert.equal(imageIdentity(displayedSource, page.url()), imageIdentity(thumbnailSource, page.url()), 'Displayed image matches thumbnail');
          const close = dialog.locator('button[command="close"]');
          const rect = await close.boundingBox();
          assert.ok(rect && rect.width >= 44 && rect.height >= 44 && rect.y >= 0 && rect.y + rect.height <= settings.height, 'Close target visible');
          if (!settings.js) {
            assert.equal(await dialog.locator('[data-lightbox-target]:visible').count(), 0, 'No dead JS-only navigation');
          }
          if (index === 0) await page.screenshot({ path: path.join(screenshots, `${settings.name}-lightbox.png`) });
          await page.keyboard.press('Escape');
          await dialog.waitFor({ state: 'hidden' });
          await page.waitForFunction(expected => document.activeElement.id === expected, opener);
          await trigger.focus();
          await page.keyboard.press('Enter');
          await close.click();
          await dialog.waitFor({ state: 'hidden' });
          await page.waitForFunction(expected => document.activeElement.id === expected, opener);
        }
        if (settings.js) {
          const initiatingId = await triggers.first().getAttribute('id');
          await triggers.first().focus();
          await page.keyboard.press('Enter');
          const next = page.locator('dialog[open] [data-photo-next][data-lightbox-target]');
          const nextId = await next.getAttribute('data-lightbox-target');
          await next.click();
          await page.locator(`dialog[id=${JSON.stringify(nextId)}][open]`).waitFor({ state: 'visible' });
          await page.locator('dialog[open] button[command="close"]').click();
          await page.waitForFunction(expected => document.activeElement.id === expected, initiatingId);
        }

        await visit('galleries/sony-only/');
        assert.ok(await page.locator('.gallery > article picture').count() > 0);
        assert.equal(await page.locator('.gallery > article > a, .gallery > article > button, dialog').count(), 0, 'None is not interactive');
        await visit('galleries/landscapes/');
        const photoLink = page.locator('.gallery > article > a').first();
        const selectedOccurrence = await photoLink.locator('..').getAttribute('id');
        await photoLink.locator('img').scrollIntoViewIfNeeded();
        await page.waitForFunction(() => {
          const image = document.querySelector('.gallery > article > a picture img');
          return image.complete && image.naturalWidth > 0;
        });
        const selectedPhoto = await photoLink.locator('img').evaluate(image => image.currentSrc);
        const href = await photoLink.getAttribute('href');
        const expectedPage = new URL(href, page.url());
        await photoLink.locator('img').click();
        await page.waitForURL(expectedPage.href, { waitUntil: 'networkidle' });
        assert.equal(await page.locator('body.photo-page').count(), 1);
        const photo = page.locator('main article > picture > img');
        assert.equal(await photo.evaluate(image => image.complete && image.naturalWidth > 0), true);
        const displayedPhoto = await photo.evaluate(image => image.currentSrc);
        assert.equal(imageIdentity(displayedPhoto, page.url()), imageIdentity(selectedPhoto, page.url()), 'Photo page matches selected gallery image');
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, 'Photo page fits viewport');
        const canonical = new URL(await page.locator('link[rel="canonical"]').getAttribute('href'), base);
        assert.equal(canonical.pathname, expectedPage.pathname);
        const og = new URL(await page.locator('meta[property="og:image"]').getAttribute('content'), base);
        const image = new URL(await photo.getAttribute('src'), page.url());
        assert.equal(og.pathname, image.pathname, 'OG points to variant, not photo page');
        await page.screenshot({ path: path.join(screenshots, `${settings.name}-photo.png`) });
        const back = page.locator('a[data-photo-return]:visible');
        assert.equal(await back.count(), 1);
        await back.click();
        await page.waitForURL('**/galleries/landscapes/**');
        assert.equal(decodeURIComponent(new URL(page.url()).hash.slice(1)), selectedOccurrence, 'Returns to selected occurrence');
        assert.equal(await page.evaluate(() => !!document.getElementById(decodeURIComponent(location.hash.slice(1)))), true, 'Return occurrence exists');
        assert.deepEqual(failedResponses, [], 'No missing local assets/pages');
        results.push({ settings: settings.name, lightboxPhotos: count, passed: true });
      } finally {
        await context.close();
      }
    }
    console.log(JSON.stringify({ engine, version: browser.version(), results, screenshots }, null, 2));
  } finally {
    await browser.close();
  }
}

verify().catch(error => {
  console.error(error);
  process.exitCode = 1;
});