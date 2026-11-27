# Operations — CI, secrets, deployment

Owner: **Agent 5** (DevOps & GitHub Repository Engineer).
Product / architecture docs live in `docs/architecture/` and `docs/decisions/`.

---

## 1. CI job matrix

`.github/workflows/ci.yml` runs on **every push to `main`** and on **every pull request
targeting `main`**. A `workflow_dispatch` trigger is available for manual runs.
Superseded runs on the same ref are cancelled (`concurrency.cancel-in-progress`).

| Job | Steps | Verification scope |
|---|---|---|
| `backend-build` | `dotnet restore` → `build -c Release` → `test` against real **PostgreSQL 16** service → export `contracts/openapi.json` artifact | Migrations, constraints, `xmin`, unit & integration tests |
| `frontend-build` | `pnpm install --frozen-lockfile` → `pnpm typecheck` → `pnpm test` (Vitest) → `pnpm build` | TypeScript strict, i18n parity, RTL audit, session policy, Vite bundle |
| `lint` | `dotnet format --verify-no-changes` + `pnpm lint` (ESLint) | Code style & analyzers across backend and frontend |
| `contract-check` | Regenerate `frontend/src/types/api.d.ts` from `contracts/openapi.json` and fail on diff | Zero API contract drift |
| `compose-config` | `docker compose config --quiet` + `nginx -t` on both nginx configs | Manifest and nginx syntax |
| `e2e` | `docker compose up -d --build --wait` + Playwright Chromium (`390x844`) running `frontend/e2e/full-stack.mjs` | Full live stack: PostgreSQL 16 + auto-migration + API + `/files` + Nginx + React 19 RTL/LTR + real HMAC auth + checkout + merchant confirm |
| `ci-status` | Final gate job; fails if any of the 6 jobs above did not strictly succeed | Branch protection required status check |

**Merge policy.** Merge only when `ci-status` is green.

---

## 2. Environment variables & Secrets

Configure production secrets via your host secret store or GitHub Environment Secrets.
CI itself uses throwaway dummy secrets for testing only.

| Variable | Required | Description |
|---|---|---|
| `POSTGRES_PASSWORD` | Yes | PostgreSQL 16 password |
| `TELEGRAM_BOT_TOKEN` | Yes | Bot token from `@BotFather` |
| `TELEGRAM_WEBHOOK_SECRET` | Yes | Random hex secret (`openssl rand -hex 32`) matched against `X-Telegram-Bot-Api-Secret-Token` |
| `JWT_SECRET` | Yes | High-entropy signing key, ≥32 bytes (`openssl rand -base64 48`) |
| `TELEGRAM_MINI_APP_BASE_URL` | Prod | Public HTTPS URL of the Mini App (used in `/start` WebApp buttons) |
| `DATABASE_AUTO_MIGRATE` | Default `true` | Applies pending EF Core migrations on API container startup |

Rules:
- Never echo a secret in a workflow log.
- Never commit `.env`; `.env.example` holds placeholders only.

---

## 3. Deployment & Rollback

1. Provision a host with Docker Engine + Compose v2.
2. Clone the repository and check out the release tag.
3. Create `.env` from `.env.example` and populate real secrets and `TELEGRAM_MINI_APP_BASE_URL`.
4. Build and launch:
   ```bash
   docker compose -f infrastructure/docker-compose.yml --env-file .env up -d --build --wait
   ```
5. Verify health:
   ```bash
   curl -fsS http://localhost:8080/api/v1/health
   ```
6. Put a TLS-terminating reverse proxy (Caddy, Traefik, or Cloudflare Tunnel) in front of
   `PROXY_HTTP_PORT`, narrow `set_real_ip_from` in `infrastructure/docker/nginx.conf` to
   the terminator's CIDR, and register the Telegram webhook via `setWebhook` with
   `secret_token`.

**Rollback:** `docker compose -f infrastructure/docker-compose.yml down`, check out the
previous release tag, and repeat step 4 (restore `pgdata` backup first if rolling back a
breaking migration).

### Service topology

| Service | Internal port | Published | Volumes | Purpose |
|---|---|---|---|---|
| `db` | 5432 | — | `pgdata` | PostgreSQL 16 |
| `api` | 8080 | — | `uploads` (`/app/storage`) | ASP.NET Core 10 API, Outbox worker, `/files` |
| `web` | 80 | — | — | Static frontend (nginx) |
| `proxy` | 80 | `${PROXY_HTTP_PORT:-8080}` | — | Single origin: `/api/*` & `/files/*` → `api:8080`, `/*` → `web:80` |

---

## 4. Local validation commands

```bash
# Validate compose manifest
docker compose -f infrastructure/docker-compose.yml --env-file .env config

# Validate nginx syntax
docker run --rm --add-host api:127.0.0.1 --add-host web:127.0.0.1   -v "$PWD/infrastructure/docker/nginx.conf:/etc/nginx/conf.d/default.conf:ro"   -v "$PWD/infrastructure/docker/security-headers.conf:/etc/nginx/conf.d/security-headers.conf:ro"   nginx:1.27-alpine nginx -t

# Run full-stack Playwright E2E locally (requires Docker + Node 20 + Playwright)
docker compose -f infrastructure/docker-compose.yml --env-file .env up -d --build --wait
E2E_BASE_URL=http://localhost:8080 node frontend/e2e/full-stack.mjs
```

---

## 5. Permanent operational rule — Composio MCP file writes

**Status: PERMANENT. Applies to every agent and every future phase (P0…P6). Do not remove.**

### 5.1 Never use `encoding: "base64"` with `GITHUB_CREATE_A_BLOB`

- **NEVER** call `GITHUB_CREATE_A_BLOB` (or any Composio MCP GitHub write tool) with
  `encoding: "base64"`. **UTF-8 only** — pass the file body as plain UTF-8 text and let
  the tool/API do the encoding.
- For whole-file writes prefer `GITHUB_CREATE_OR_UPDATE_FILE_CONTENTS` with plain-text
  `content`; use `GITHUB_CREATE_A_BLOB` only when a raw blob is genuinely required, and
  then still with UTF-8 content.

### 5.2 Two 403s = stop, hand off an artifact bundle

- If Composio returns **`403` twice** for the same operation, **STOP retrying
  immediately**. Hand off an artifact bundle via `upload_file` with target path, branch,
  and commit message.

### 5.3 Verification after any MCP write

- After a write, re-read the file (`GITHUB_GET_REPOSITORY_CONTENT`) and confirm the
  content and byte size match what was intended.
