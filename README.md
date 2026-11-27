# Telegram CRM

> **Status:** MVP Complete (P0–P5) — Full-stack multi-tenant Telegram Mini App CRM & Storefront verified by real PostgreSQL 16 integration tests, Vitest, and full-stack Docker Compose + Playwright E2E in CI.

Monorepo for a multi-tenant Telegram Mini App CRM and storefront designed for Telegram channel sellers, featuring bilingual Persian/English (`fa`/`en`) UI with native RTL/LTR support.

| Layer | Stack |
|---|---|
| Backend | ASP.NET Core 10 LTS (modular monolith), EF Core 10, PostgreSQL 16, FluentValidation, Telegram.Bot |
| Frontend | React 19, Vite 6, TypeScript (strict), Tailwind CSS v4, shadcn/ui, TanStack Query, i18next (FA/EN, RTL/LTR), `@tma.js/sdk-react` |
| Infra & CI | Docker Compose (PostgreSQL 16 + API + Web + Nginx reverse proxy), GitHub Actions (7 jobs incl. live Playwright E2E) |

Architecture decisions live in [`docs/decisions/`](docs/decisions/); the API contract is [`contracts/openapi.json`](contracts/openapi.json) (generated from the backend build — never hand-edited).

---

## Core Capabilities

- **Multi-Tenancy & Security:** Single-database `TenantId` isolation enforced by EF Core `GlobalQueryFilters` + `TenantGuardInterceptor`, cross-tenant requests return `404` (never `403`), HMAC-SHA256 Telegram `initData` validation (including replay & future `auth_date` checks), HS256 JWT stored only in memory/`sessionStorage` (zero `localStorage`).
- **Merchant Panel (`/merchant`):** Live KPI dashboard, Categories & Products CRUD, atomic `stock-adjust` (`409 insufficient_stock`), magic-byte-validated product image uploads with atomic primary image swap, Order lifecycle state machine (`Pending → Confirmed → Preparing → Shipped → Delivered / Cancelled`) with `xmin` concurrency and idempotent stock restore, CRM Customers & Tags, and JSON-validated Shop Settings.
- **Customer Storefront (`/s/:shopSlug`):** Mobile-first bilingual catalog, category filtering & search, product detail gallery, `sessionStorage` cart per shop, server-side cart validation, `Idempotency-Key` checkout, customer order tracking & `Pending` cancellation, and Telegram BackButton integration.
- **Telegram Bot & Outbox:** Single-platform bot webhook (`X-Telegram-Bot-Api-Secret-Token` constant-time verification) handling `/start` and `/start shop_{slug}` deep links, plus transactional `OutboxProcessor` notifications.

---

## Quick Start (Full Stack with Docker)

1. Copy the environment template and fill in your secrets:

```bash
cp .env.example .env
```

2. Build and start the complete stack (PostgreSQL 16, auto-migrating API, React SPA, and Nginx proxy):

```bash
docker compose -f infrastructure/docker-compose.yml --env-file .env up -d --build --wait
```

3. Verify the running application:
   - Health check: <http://localhost:8080/api/v1/health>
   - Web app: <http://localhost:8080>

| Service | Internal port | Exposed | Purpose |
|---|---|---|---|
| `db` | 5432 | — | PostgreSQL 16 (`pgdata` volume) |
| `api` | 8080 | — | ASP.NET Core 10 API + auto-migrations + `/files` (`uploads` volume) |
| `web` | 80 | — | Static React 19 SPA (nginx) |
| `proxy` | 80 | `PROXY_HTTP_PORT` (8080) | Single origin: `/api/*` & `/files/*` → `api:8080`, `/*` → `web:80` |

---

## Local Development & Verification

```bash
# Backend (.NET 10.0.404)
dotnet restore backend/TelegramCrm.sln
dotnet build backend/TelegramCrm.sln --no-restore -c Release
dotnet test backend/TelegramCrm.sln -c Release

# Frontend (Node 20 + pnpm 9)
cd frontend
pnpm install --frozen-lockfile
pnpm gen:api
pnpm typecheck
pnpm lint
pnpm test
pnpm build
```

---

## BotFather & Webhook Setup (Production)

Telegram Mini Apps require HTTPS. Place a TLS terminator (Caddy, Traefik, or Cloudflare Tunnel) in front of `PROXY_HTTP_PORT` pointing to `https://your-domain.example`, then:

1. In `@BotFather`, configure your bot's **Menu Button / Mini App URL** to `https://your-domain.example`.
2. Register the webhook with the exact `TELEGRAM_WEBHOOK_SECRET` from `.env`:

```bash
curl -X POST "https://api.telegram.org/bot${TELEGRAM_BOT_TOKEN}/setWebhook"   -H "Content-Type: application/json"   -d "{"url":"https://your-domain.example/api/v1/telegram/webhook","secret_token":"${TELEGRAM_WEBHOOK_SECRET}"}"
```

3. Share any shop directly via deep link: `https://t.me/<your_bot_username>?start=shop_<shopSlug>`.

---

## Operations & CI

See **[`docs/operations.md`](docs/operations.md)** for the complete CI matrix, required secrets, and deployment/rollback checklist.
