# Telegram CRM

> **Status:** P0 — Skeleton. Repository scaffolding only; no application code yet.

Monorepo for a multi-tenant Telegram Mini-App CRM / storefront.

| Layer | Stack |
|---|---|
| Backend | ASP.NET Core 10 (modular monolith), EF Core + PostgreSQL |
| Frontend | React 19 + Vite + TypeScript (strict), Tailwind + shadcn/ui, i18next (fa/en, RTL) |
| Infra | Docker Compose: PostgreSQL + API + Web + Nginx reverse proxy |

Architecture decisions live in [`docs/decisions/`](docs/decisions/); the API contract is
[`contracts/openapi.json`](contracts/openapi.json) (generated from the backend build — never hand-edited).

---

## Repository layout

```
.
├── .github/workflows/          # ci.yml
├── backend/
│   ├── TelegramCrm.sln
│   ├── src/
│   │   ├── TelegramCrm.Api/            # composition root, endpoints, DI
│   │   ├── TelegramCrm.Domain/         # entities, value objects, domain events
│   │   ├── TelegramCrm.Application/    # use-cases, DTOs, validators
│   │   └── TelegramCrm.Infrastructure/ # EF Core, Npgsql, Telegram.Bot, outbox, storage
│   └── tests/                          # unit + integration tests
├── frontend/
│   ├── src/{app,features,shared,locales}
│   └── e2e/                            # Playwright
├── contracts/                          # openapi.json (source of truth for types)
├── infrastructure/
│   ├── docker/{api.Dockerfile,web.Dockerfile,nginx.conf}
│   └── docker-compose.yml
└── docs/{architecture,decisions}
```

---

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | **10.0.404** | pinned by `global.json` |
| Node.js | 20.x | |
| pnpm | 9.x | `corepack enable && corepack prepare pnpm@9 --activate` |
| Docker + Compose v2 | latest | only needed for the containerised stack |

---

## Quick start

```bash
cp .env.example .env          # then fill POSTGRES_PASSWORD, TELEGRAM_BOT_TOKEN, JWT_SECRET
```

`.env` is git-ignored. Never commit it, and never paste real values into `.env.example`.

```bash
# backend
cd backend && dotnet restore TelegramCrm.sln && dotnet build TelegramCrm.sln --no-restore

# frontend
cd frontend && pnpm install --frozen-lockfile && pnpm typecheck && pnpm build

# full stack
docker compose -f infrastructure/docker-compose.yml --env-file .env up --build
```

Then open <http://localhost:8080>.

| Service | Internal port | Exposed | Purpose |
|---|---|---|---|
| `db` | 5432 | — | PostgreSQL 16, data in named volume `pgdata` |
| `api` | 8080 | — | ASP.NET Core API |
| `web` | 80 | — | Static frontend (nginx) |
| `proxy` | 80 | `PROXY_HTTP_PORT` (8080) | Single origin: `/api` → `api:8080`, `/` → `web:80` |

Only `proxy` is published to the host, so the browser talks to one origin and no CORS
configuration is required.

---

## CI, secrets and deployment

See **[`docs/operations.md`](docs/operations.md)** for the CI job matrix, the required
GitHub Secrets, and the step-by-step deployment / rollback procedure.

**Merge policy:** a pull request may only be merged when every CI job is green.

---

## Contributing

- Branch naming: `feat/p{N}-{scope}` (e.g. `feat/p1-backend-core`).
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/):
  `feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`, `ci:`.
- One logical change per pull request; CI must be green before review.

---

## License

Proprietary — all rights reserved.
