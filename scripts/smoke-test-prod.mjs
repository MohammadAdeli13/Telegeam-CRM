import assert from 'node:assert/strict';
import crypto from 'node:crypto';

const BASE_URL = (process.env.E2E_BASE_URL || 'https://crm.toolavailable.cyou').replace(/\/$/, '');
const BOT_TOKEN = process.env.TELEGRAM_BOT_TOKEN;
const WEBHOOK_SECRET = process.env.TELEGRAM_WEBHOOK_SECRET;

assert.ok(BOT_TOKEN, 'TELEGRAM_BOT_TOKEN env var is required.');
assert.ok(WEBHOOK_SECRET, 'TELEGRAM_WEBHOOK_SECRET env var is required.');

const TINY_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a8XQAAAAASUVORK5CYII=',
  'base64',
);

function buildSignedInitData(user) {
  const authDate = String(Math.floor(Date.now() / 1000));
  const pairs = new Map([
    ['auth_date', authDate],
    ['query_id', `smoke-${crypto.randomUUID()}`],
    ['user', JSON.stringify(user)],
  ]);

  const dataCheckString = [...pairs.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([k, v]) => `${k}=${v}`)
    .join('\n');

  const secretKey = crypto.createHmac('sha256', 'WebAppData').update(BOT_TOKEN).digest();
  const hash = crypto.createHmac('sha256', secretKey).update(dataCheckString).digest('hex');

  const params = new URLSearchParams();
  for (const [k, v] of pairs.entries()) params.set(k, v);
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
  console.log(`\n[SMOKE] Running production API test suite against ${BASE_URL}\n`);

  // 1. Health & Security Headers
  const { response: healthRes, json: health } = await apiJson('/api/v1/health');
  assert.equal(healthRes.status, 200, `Health failed: ${healthRes.status}`);
  assert.equal(health.status, 'healthy');
  assert.equal(health.database, 'up');
  assert.equal(healthRes.headers.get('x-content-type-options'), 'nosniff');
  assert.match(healthRes.headers.get('content-security-policy') || '', /frame-ancestors/);
  console.log('✔ [1/8] Health, DB & Nginx/Cloudflare security headers OK');

  // 2. Webhook Guard & Unauthenticated Guard
  const badWebhook = await apiJson('/api/v1/telegram/webhook', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Telegram-Bot-Api-Secret-Token': 'wrong-secret' },
    body: JSON.stringify({ update_id: 1 }),
  });
  assert.equal(badWebhook.response.status, 401, 'Webhook must reject invalid secret');

  const unauthMerchant = await apiJson('/api/v1/merchants/mine');
  assert.equal(unauthMerchant.response.status, 401, 'Protected endpoint must return 401 without JWT');
  console.log('✔ [2/8] Webhook secret guard & JWT auth guard OK');

  // 3. Telegram initData Auth (Merchant & Customer)
  const merchantInitData = buildSignedInitData({ id: 991001, first_name: 'Smoke', last_name: 'Merchant', language_code: 'fa' });
  const customerInitData = buildSignedInitData({ id: 992002, first_name: 'Smoke', last_name: 'Buyer', language_code: 'fa' });

  const mLogin = await apiJson('/api/v1/auth/telegram', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ initData: merchantInitData }),
  });
  assert.equal(mLogin.response.status, 200, `Merchant auth failed: ${JSON.stringify(mLogin.json)}`);
  let merchantToken = mLogin.json.accessToken;

  const cLogin = await apiJson('/api/v1/auth/telegram', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ initData: customerInitData }),
  });
  assert.equal(cLogin.response.status, 200, `Customer auth failed: ${JSON.stringify(cLogin.json)}`);
  const customerToken = cLogin.json.accessToken;
  console.log('✔ [3/8] Telegram HMAC-SHA256 initData login & JWT issuance OK');

  // 4. Create Shop & Switch Tenant
  const shopSlug = `smoke-${crypto.randomBytes(3).toString('hex')}`;
  const shopRes = await apiJson('/api/v1/merchants', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${merchantToken}` },
    body: JSON.stringify({ name: 'فروشگاه تست اسموک', slug: shopSlug, defaultLanguage: 'fa', currency: 'IRR' }),
  });
  assert.equal(shopRes.response.status, 201, `Create shop failed: ${JSON.stringify(shopRes.json)}`);

  const switchRes = await apiJson('/api/v1/auth/switch-tenant', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${merchantToken}` },
    body: JSON.stringify({ tenantId: shopRes.json.id }),
  });
  assert.equal(switchRes.response.status, 200, `Switch tenant failed: ${JSON.stringify(switchRes.json)}`);
  merchantToken = switchRes.json.accessToken;
  console.log(`✔ [4/8] Shop created (/s/${shopSlug}) & tenant switched OK`);

  // 5. Category, Product (stock=5) & Real Image Upload + Serving
  const catRes = await apiJson('/api/v1/merchant/categories', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${merchantToken}` },
    body: JSON.stringify({ name: 'نوشیدنی', nameEn: 'Drinks', slug: 'drinks', sortOrder: 0, isActive: true }),
  });
  assert.equal(catRes.response.status, 201, `Create category failed: ${JSON.stringify(catRes.json)}`);

  const prodRes = await apiJson('/api/v1/merchant/products', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${merchantToken}` },
    body: JSON.stringify({
      categoryId: catRes.json.id,
      name: 'قهوه تست',
      nameEn: 'Test Coffee',
      description: 'تست خودکار',
      price: 150000,
      stock: 5,
      isActive: true,
    }),
  });
  assert.equal(prodRes.response.status, 201, `Create product failed: ${JSON.stringify(prodRes.json)}`);
  const productId = prodRes.json.id;

  const form = new FormData();
  form.append('file', new Blob([TINY_PNG], { type: 'image/png' }), 'test.png');
  form.append('altText', 'Smoke test image');
  const uploadRes = await fetch(`${BASE_URL}/api/v1/merchant/products/${productId}/images`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${merchantToken}` },
    body: form,
  });
  assert.equal(uploadRes.status, 201, `Image upload failed: ${uploadRes.status}`);
  const img = await uploadRes.json();

  const servedImg = await fetch(`${BASE_URL}${img.url}`);
  assert.equal(servedImg.status, 200, `Image not served at ${img.url}: ${servedImg.status}`);
  assert.match(servedImg.headers.get('content-type') || '', /image\/png/);
  console.log(`✔ [5/8] Category, Product (stock=5), Image upload & Nginx serving (${img.url}) OK`);

  // 6. Storefront Cart Validation & Order Placement (qty=2 -> stock should become 3)
  const validateRes = await apiJson(`/api/v1/storefront/${shopSlug}/cart/validate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${customerToken}` },
    body: JSON.stringify({ items: [{ productId, quantity: 2 }] }),
  });
  assert.equal(validateRes.response.status, 200, `Cart validate failed: ${JSON.stringify(validateRes.json)}`);

  const orderRes = await apiJson(`/api/v1/storefront/${shopSlug}/orders`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${customerToken}`,
      'Idempotency-Key': crypto.randomUUID(),
    },
    body: JSON.stringify({ items: [{ productId, quantity: 2 }], note: 'Smoke test order' }),
  });
  assert.equal(orderRes.response.status, 201, `Place order failed: ${JSON.stringify(orderRes.json)}`);
  const orderId = orderRes.json.id;

  const prodAfterOrder = await apiJson(`/api/v1/merchant/products/${productId}`, {
    headers: { Authorization: `Bearer ${merchantToken}` },
  });
  assert.equal(prodAfterOrder.json.stock, 3, `Expected stock 3 after ordering 2, got ${prodAfterOrder.json.stock}`);
  console.log('✔ [6/8] Storefront cart validation, order placement & atomic stock decrement (5 -> 3) OK');

  // 7. Edge Case: Over-ordering (qty=4 when stock=3 -> must fail with 409)
  const overOrderRes = await apiJson(`/api/v1/storefront/${shopSlug}/orders`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${customerToken}`,
      'Idempotency-Key': crypto.randomUUID(),
    },
    body: JSON.stringify({ items: [{ productId, quantity: 4 }] }),
  });
  assert.equal(overOrderRes.response.status, 409, `Expected 409 insufficient_stock, got ${overOrderRes.response.status}`);
  console.log('✔ [7/8] Edge case: Over-ordering correctly rejected with 409 Conflict');

  // 8. Cancel Order & Verify Atomic Stock Restoration (3 -> 5)
  const cancelRes = await apiJson(`/api/v1/storefront/${shopSlug}/orders/${orderId}/cancel`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${customerToken}` },
    body: JSON.stringify({ reason: 'Smoke test cleanup' }),
  });
  assert.equal(cancelRes.response.status, 200, `Cancel order failed: ${JSON.stringify(cancelRes.json)}`);

  const prodAfterCancel = await apiJson(`/api/v1/merchant/products/${productId}`, {
    headers: { Authorization: `Bearer ${merchantToken}` },
  });
  assert.equal(prodAfterCancel.json.stock, 5, `Expected stock restored to 5, got ${prodAfterCancel.json.stock}`);
  console.log('✔ [8/8] Order cancellation & atomic stock restoration (3 -> 5) OK');

  console.log('\n🎉 ALL 8 PRODUCTION SMOKE TESTS PASSED!\n');
}

run().catch((err) => {
  console.error('\n❌ [SMOKE] FAILED:', err);
  process.exit(1);
});
