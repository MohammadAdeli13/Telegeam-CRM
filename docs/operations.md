# Operations — CI, secrets, deployment

Owner: **Agent 5** (DevOps & GitHub Repository Engineer).
Product / architecture docs live in `docs/architecture/` and `docs/decisions/` (Agent 1).

---

## 1. CI job matrix

`.github/workflows/ci.yml` runs on **every push** (all branches) and on **every pull
request targeting `main`**. A `workflow_dispatch` trigger is available for manual runs.
Superseded runs on the same ref are cancelled (`concurrency.cancel-in-progress`).

| Job | Steps | Skips when |
|---|---|---|
| `backend-build` | `dotnet restore` → `build -c Release` → `test` on `backend/TelegramCrm.sln`; uploads `test-results.trx` | `backend/TelegramCrm.sln` absent |
| `frontend-build` | `pnpm install --frozen-lockfile` → `pnpm typecheck` → `pnpm build` | `frontend/package.json` absent |
| `lint` | `dotnet format --verify-no-changes` + `pnpm lint` | neither project present |
| `contract-check` | regenerate `frontend/src/types/api.d.ts` from `contracts/openapi.json`, fail on drift | contract or frontend absent |
| `compose-config` | `docker compose config --quiet` + `nginx -t` on both nginx configs | never (always runs) |
| `ci-status` | gate job; fails if any job above did not succeed | never |

**P0 behaviour.** While a project does not exist yet, its job exits with a `::notice`
instead of failing, so `main` stays green from the first commit. The guards disappear
naturally once P1/P3 land the real projects.

**Merge policy.** Merge only when `ci-status` is green. `ci-status` is the single
required status check for branch protection.

### What `compose-config` does and does not prove

The `nginx -t` step runs each config inside a throwaway `nginx:1.27-alpine` container
with `--add-host api:127.0.0.1 --add-host web:127.0.0.1`. Those host entries exist
**only so that nginx can resolve the upstream names at parse time**.

> **This is a syntax check, not a connectivity check.** It does not verify that the
> `api` or `web` services are reachable, that ports match, that the reverse proxy
> actually forwards traffic, or that TLS terminates correctly. Real end-to-end
> verification requires `docker compose up` on a host with Docker and is out of scope
> for CI.

---

## 2. GitHub Secrets

Configure under **Settings → Secrets and variables → Actions**. CI itself needs none of
them; they are injected at deploy time only.

| Secret | Used by | Description |
|---|---|---|
| `POSTGRES_PASSWORD` | deploy | database password |
| `TELEGRAM_BOT_TOKEN` | deploy | bot token from @BotFather |
| `JWT_SECRET` | deploy | ≥32 random chars (`openssl rand -base64 48`) |

Rules:

- Never echo a secret in a workflow log.
- Never write a secret into a committed file.
- `.env.example` holds **placeholders only**; `.env` is git-ignored.

---

## 3. Deployment

1. Provision a host with Docker Engine + Compose v2.
2. Clone the repository and check out the release tag (`v0.1.0`).
3. Create `.env` on the host from `.env.example` and fill in the real values
   (or export them from your secret store).
4. `docker compose -f infrastructure/docker-compose.yml --env-file .env up -d --build`
5. Verify: `curl -f http://localhost:8080/api/v1/health` and open the storefront.
6. Put a TLS-terminating reverse proxy (Caddy, Traefik, or a cloud LB) in front of
   `PROXY_HTTP_PORT`; Telegram Mini Apps require HTTPS.

**Rollback.** `docker compose ... down`, check out the previous tag, repeat step 4.

### Service topology

| Service | Internal port | Published | Purpose |
|---|---|---|---|
| `db` | 5432 | — | PostgreSQL 16, data in named volume `pgdata` |
| `api` | 8080 | — | ASP.NET Core API |
| `web` | 80 | — | static frontend (nginx) |
| `proxy` | 80 | `${PROXY_HTTP_PORT:-8080}` | single origin: `/api` → `api:8080`, `/` → `web:80` |

Only `proxy` publishes a host port, so the browser talks to one origin and no CORS
configuration is required. `db` and `api` expose healthchecks; `api` waits for
`db` to be healthy before starting.

---

## 4. P0 validation limitations (explicit, unverified items)

- **`docker compose config` was NOT verified in the sandbox.** Docker is not installed
  in the build sandbox (`which docker` → not found), so the manifest can only be
  validated by the `compose-config` CI job or on a machine with Docker. This is an
  explicit, unverified item for P0.
- **`nginx -t` in CI is a syntax check only** — see §1. It does not prove service
  connectivity.
- YAML/JSON syntax of the committed files was validated locally (`yaml.safe_load`,
  `json.load`). That is a **syntax check only**, not a runtime check.

---

## 5. Local validation commands

```bash
# compose manifest (needs Docker)
docker compose -f infrastructure/docker-compose.yml --env-file .env config

# nginx syntax (needs Docker)
docker run --rm -v "$PWD/infrastructure/docker/nginx.conf:/etc/nginx/conf.d/default.conf:ro" \
  nginx:1.27-alpine nginx -t
docker run --rm -v "$PWD/infrastructure/docker/web-nginx.conf:/etc/nginx/conf.d/default.conf:ro" \
  nginx:1.27-alpine nginx -t
```

---

## 6. Permanent operational rule — Composio MCP file writes

**Status: PERMANENT. Applies to every agent and every future phase (P0…P6). Do not remove.**

### 6.1 Never use `encoding: "base64"` with `GITHUB_CREATE_A_BLOB`

- **NEVER** call `GITHUB_CREATE_A_BLOB` (or any Composio MCP GitHub write tool) with
  `encoding: "base64"`. **UTF-8 only** — pass the file body as plain UTF-8 text and let
  the tool/API do the encoding.
- Reason: the base64 path through Composio MCP has produced corrupted blobs and
  `403`/`422` responses. UTF-8 is the only supported write path.
- For whole-file writes prefer `GITHUB_CREATE_OR_UPDATE_FILE_CONTENTS` with plain-text
  `content`; use `GITHUB_CREATE_A_BLOB` only when a raw blob is genuinely required, and
  then still with UTF-8 content.

### 6.2 Two 403s = stop, hand off an artifact bundle

- If Composio returns **`403` twice** for the same operation, **STOP retrying
  immediately**. Do not loop, do not vary the payload hoping it passes.
- Instead, **hand off an artifact bundle**: publish the intended files with
  `upload_file` (permanent `/sites/` or `/files/` URL) and post the bundle + the exact
  target path/branch/commit message to the group so the integrator (Agent 5) can land
  it through the GitHub API.
- Rationale: repeated 403s indicate a permission/token problem, not a transient error;
  retrying burns the run and can never succeed.

### 6.3 Verification after any MCP write

- After a write, re-read the file (`GITHUB_GET_REPOSITORY_CONTENT`) and confirm the
  content and byte size match what was intended. A silent truncation is a failure.
