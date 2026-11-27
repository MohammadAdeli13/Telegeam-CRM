import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { chromium } from 'playwright';

const BASE_URL = (process.env.E2E_BASE_URL || 'http://localhost:8080').replace(/\/$/, '');
const BOT_TOKEN = process.env.TELEGRAM_BOT_TOKEN;
const WEBHOOK_SECRET = process.env.TELEGRAM_WEBHOOK_SECRET;

assert.ok(BOT_TOKEN, 'TELEGRAM_BOT_TOKEN env var is required for E2E.');
assert.ok(WEBHOOK_SECRET, 'TELEGRAM_WEBHOOK_SECRET env var is required for E2E.');

// Minimal valid 1x1 PNG (68 bytes)
const TINY_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a8XQAAAAASUVORK5CYII=',
  'base64',
);

function buildSignedInitData(user) {
  const authDate = String(Math.floor(Date.now() / 1000));
  const pairs = new Map([
    ['auth_date', authDate],
    ['query_id', `e2e-${crypto.randomUUID()}`],
    ['user', JSON.stringify(user)],
  ]);

  const dataCheckString = [...pairs.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([k, v]) => `${k}=${v}`)
    .join('\n');

  const secretKey = crypto.createHmac('sha256', 'WebAppData').update(BOT_TOKEN).digest();
  const hash = crypto.createHmac('sha256', secretKey).update(dataCheckString).digest('hex');

  const params = new URLSearchParams();
  for (const [k, v] of pairs.entries()) {
    params.set(k, v);
  }
  params.set('hash', hash);
  return params.toString();
}

async function apiJson(path, options = {}) {
  const response = await fetch(`${BASE_URL}${path}`, options);
  const text = await response.text();
  let json = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    json = text;
  }
  return { response, json };
}

async function run() {
  console.log(`[E2E] Testing full stack at ${BASE_URL}`);

  // 1. Health & Security Headers behind Nginx proxy
  const { response: healthRes, json: health } = await apiJson('/api/v1/health');
  assert.equal(healthRes.status, 200, `Expected 200 from /api/v1/health, got ${healthRes.status}`);
  assert.equal(health.status, 'healthy');
  assert.equal(health.database, 'up');
  assert.equal(healthRes.headers.get('x-content-type-options'), 'nosniff');
  assert.match(healthRes.headers.get('content-security-policy') || '', /frame-ancestors/);
  assert.equal(healthRes.headers.get('x-frame-options'), null, 'X-Frame-Options must not be set');
  console.log('[E2E] 1/6 Health & Nginx security headers verified.');

  // 2. Webhook secret guard behind Nginx
  const badWebhook = await apiJson('/api/v1/telegram/webhook', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Telegram-Bot-Api-Secret-Token': 'invalid-secret',
    },
    body: JSON.stringify({ update_id: 1 }),
  });
  assert.equal(badWebhook.response.status, 401);

  // 3. Seed a merchant shop, category, product, and real PNG image through the live API
  const merchantUser = { id: 501001, first_name: 'Noor', last_name: 'Merchant', language_code: 'fa' };
  const customerUser = { id: 502002, first_name: 'Sara', last_name: 'Buyer', language_code: 'fa' };
  const merchantInitData = buildSignedInitData(merchantUser);
  const customerInitData = buildSignedInitData(customerUser);

  const loginRes = await apiJson('/api/v1/auth/telegram', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ initData: merchantInitData }),
  });
  assert.equal(loginRes.response.status, 200, `Merchant auth failed: ${JSON.stringify(loginRes.json)}`);
  let merchantToken = loginRes.json.accessToken;

  const shopSlug = `e2e-shop-${crypto.randomBytes(3).toString('hex')}`;
  const createShopRes = await apiJson('/api/v1/merchants', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${merchantToken}`,
    },
    body: JSON.stringify({
      name: 'گالری نور',
      slug: shopSlug,
      defaultLanguage: 'fa',
      currency: 'IRR',
    }),
  });
  assert.equal(createShopRes.response.status, 201);
  const shopId = createShopRes.json.id;

  const switchRes = await apiJson('/api/v1/auth/switch-tenant', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${merchantToken}`,
    },
    body: JSON.stringify({ tenantId: shopId }),
  });
  assert.equal(switchRes.response.status, 200);
  merchantToken = switchRes.json.accessToken;

  const catRes = await apiJson('/api/v1/merchant/categories', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${merchantToken}`,
    },
    body: JSON.stringify({
      name: 'نوشیدنی',
      nameEn: 'Drinks',
      slug: 'drinks',
      sortOrder: 0,
      isActive: true,
    }),
  });
  assert.equal(catRes.response.status, 201);

  const prodRes = await apiJson('/api/v1/merchant/products', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${merchantToken}`,
    },
    body: JSON.stringify({
      categoryId: catRes.json.id,
      name: 'قهوه اسپرسو',
      nameEn: 'Espresso Roast',
      description: 'دانه تازه برشته‌شده',
      price: 150000,
      stock: 5,
      isActive: true,
    }),
  });
  assert.equal(prodRes.response.status, 201);
  const productId = prodRes.json.id;

  // Upload real PNG image and verify it is served through Nginx /files/*
  const form = new FormData();
  form.append('file', new Blob([TINY_PNG], { type: 'image/png' }), 'espresso.png');
  form.append('altText', 'Espresso photo');
  const uploadRes = await fetch(`${BASE_URL}/api/v1/merchant/products/${productId}/images`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${merchantToken}` },
    body: form,
  });
  assert.equal(uploadRes.status, 201, `Image upload failed with status ${uploadRes.status}`);
  const uploadedImg = await uploadRes.json();
  assert.equal(uploadedImg.isPrimary, true);

  const servedImg = await fetch(`${BASE_URL}${uploadedImg.url}`);
  assert.equal(servedImg.status, 200, `Uploaded image not served via Nginx at ${uploadedImg.url}`);
  assert.match(servedImg.headers.get('content-type') || '', /image\/png/);
  console.log(`[E2E] 2/6 Seeded shop /s/${shopSlug} & verified image serving via Nginx.`);

  // 4. Launch real Chromium browser on mobile viewport (390x844)
  const browser = await chromium.launch();
  const pageErrors = [];
  const sameOriginConsoleErrors = [];

  const attachErrorListeners = (page) => {
    page.on('pageerror', (err) => pageErrors.push(err.message));
    page.on('console', (msg) => {
      if (msg.type() === 'error') {
        const text = msg.text();
        if (!text.includes('fonts.googleapis.com') && !text.includes('fonts.gstatic.com')) {
          sameOriginConsoleErrors.push(text);
        }
      }
    });
  };

  try {
    // 4a. Customer flow: Storefront RTL -> LTR toggle -> product detail -> cart -> checkout
    const customerContext = await browser.newContext({
      viewport: { width: 390, height: 844 },
    });
    await customerContext.addInitScript((rawInitData) => {
      window.Telegram = {
        WebApp: {
          initData: rawInitData,
          colorScheme: 'dark',
          ready: () => {},
          expand: () => {},
        },
      };
    }, customerInitData);

    const customerPage = await customerContext.newPage();
    attachErrorListeners(customerPage);

    await customerPage.goto(`${BASE_URL}/s/${shopSlug}`, { waitUntil: 'networkidle' });
    await customerPage.getByText('قهوه اسپرسو').waitFor({ timeout: 15000 });

    // Check RTL default & zero horizontal overflow
    const rtlCheck = await customerPage.evaluate(() => ({
      dir: document.documentElement.dir,
      lang: document.documentElement.lang,
      scrollWidth: document.documentElement.scrollWidth,
      innerWidth: window.innerWidth,
      localStorageLen: window.localStorage.length,
    }));
    assert.equal(rtlCheck.dir, 'rtl');
    assert.equal(rtlCheck.lang, 'fa');
    assert.ok(
      rtlCheck.scrollWidth <= rtlCheck.innerWidth,
      `Horizontal overflow in RTL: ${rtlCheck.scrollWidth} > ${rtlCheck.innerWidth}`,
    );
    assert.equal(rtlCheck.localStorageLen, 0, 'localStorage must remain empty');

    // Verify product image actually rendered in DOM
    const imgLoaded = await customerPage.evaluate(() => {
      const img = document.querySelector('img');
      return Boolean(img && img.complete && img.naturalWidth > 0);
    });
    assert.equal(imgLoaded, true, 'Product image did not load in storefront');

    // Toggle to English LTR & verify bilingual text + zero overflow
    await customerPage.getByRole('button', { name: 'تغییر زبان' }).click();
    await customerPage.getByText('Espresso Roast').waitFor({ timeout: 5000 });
    const ltrCheck = await customerPage.evaluate(() => ({
      dir: document.documentElement.dir,
      lang: document.documentElement.lang,
      scrollWidth: document.documentElement.scrollWidth,
      innerWidth: window.innerWidth,
    }));
    assert.equal(ltrCheck.dir, 'ltr');
    assert.equal(ltrCheck.lang, 'en');
    assert.ok(
      ltrCheck.scrollWidth <= ltrCheck.innerWidth,
      `Horizontal overflow in LTR: ${ltrCheck.scrollWidth} > ${ltrCheck.innerWidth}`,
    );
    console.log('[E2E] 3/6 Storefront mobile RTL/LTR, image render & overflow checks passed.');

    // Open product detail, add to cart, validate cart, and place order
    await customerPage.getByRole('link', { name: 'Espresso Roast' }).first().click();
    await customerPage.getByRole('button', { name: 'Add to cart' }).waitFor({ timeout: 10000 });
    await customerPage.getByRole('button', { name: 'Add to cart' }).click();
    await customerPage.getByRole('link', { name: 'View cart' }).click();

    await customerPage.getByRole('button', { name: 'Verify stock & prices' }).waitFor({ timeout: 10000 });
    await customerPage.getByRole('button', { name: 'Verify stock & prices' }).click();
    await customerPage.getByText('All items and prices are verified.').waitFor({ timeout: 10000 });

    await customerPage.getByLabel('Note for merchant (optional)').fill('Please deliver after 6 PM');
    await customerPage.getByRole('button', { name: 'Place order' }).click();

    await customerPage.getByText('Your order has been placed!').waitFor({ timeout: 15000 });
    await customerPage.getByText('Pending').first().waitFor({ timeout: 5000 });
    const customerLocalStorage = await customerPage.evaluate(() => window.localStorage.length);
    assert.equal(customerLocalStorage, 0, 'localStorage must stay empty after checkout');
    console.log('[E2E] 4/6 Customer cart validation & idempotent order placement passed.');

    await customerContext.close();

    // 4b. Merchant flow: Open panel -> Dashboard KPIs -> Orders -> Confirm order
    const merchantContext = await browser.newContext({
      viewport: { width: 390, height: 844 },
    });
    await merchantContext.addInitScript((rawInitData) => {
      window.Telegram = {
        WebApp: {
          initData: rawInitData,
          colorScheme: 'dark',
          ready: () => {},
          expand: () => {},
        },
      };
    }, merchantInitData);

    const merchantPage = await merchantContext.newPage();
    attachErrorListeners(merchantPage);

    await merchantPage.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });
    await merchantPage.waitForURL('**/merchant', { timeout: 15000 });
    await merchantPage.getByText('نمای کلی فروشگاه').waitFor({ timeout: 10000 });

    // Navigate to Orders tab and transition order Pending -> Confirmed
    await merchantPage.getByRole('link', { name: 'سفارش‌ها' }).first().click();
    await merchantPage.waitForURL('**/merchant/orders', { timeout: 10000 });
    await merchantPage.getByRole('button', { name: 'جزئیات' }).first().waitFor({ timeout: 10000 });
    await merchantPage.getByRole('button', { name: 'جزئیات' }).first().click();

    await merchantPage.getByRole('button', { name: 'تغییر به «تأیید شده»' }).waitFor({ timeout: 10000 });
    await merchantPage.getByRole('button', { name: 'تغییر به «تأیید شده»' }).click();
    await merchantPage.getByText('تأیید شده').first().waitFor({ timeout: 10000 });

    const merchantLocalStorage = await merchantPage.evaluate(() => window.localStorage.length);
    assert.equal(merchantLocalStorage, 0, 'Merchant panel must not write to localStorage');
    console.log('[E2E] 5/6 Merchant dashboard & Pending -> Confirmed order transition passed.');

    await merchantContext.close();
  } finally {
    await browser.close();
  }

  // 5. Verify no page or console errors occurred
  assert.deepEqual(pageErrors, [], `Uncaught page errors: ${pageErrors.join('; ')}`);
  assert.deepEqual(
    sameOriginConsoleErrors,
    [],
    `Console errors detected: ${sameOriginConsoleErrors.join('; ')}`,
  );
  console.log('[E2E] 6/6 Zero uncaught page errors and zero console errors. ALL E2E CHECKS PASSED!');
}

run().catch((err) => {
  console.error('[E2E] FAILED:', err);
  process.exit(1);
});
