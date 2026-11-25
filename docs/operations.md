# Operations — CI, secrets, deployment

Owner: Agent 5. Product/architecture docs are in `docs/architecture/` and `docs/decisions/` (Agent 1).

## CI job matrix

`.github/workflows/ci.yml` runs on every push and on every PR targeting `main`.

| Job | Steps |
|---|---|
| `backend-build` | `dotnet restore` / `build -c Release` / `test` on `backend/TelegramCrm.sln` |
| `frontend-build` | `pnpm install --frozen-lockfile` / `typecheck` / `build` |
| `lint` | `dotnet format --verify-no-changes` + `pnpm lint` |
| `contract-check` | regenerate `frontend/src/types/api.d.ts`, fail on drift |
| `compose-config` | `docker compose config --quiet` + `nginx -t` on both configs |
| `ci-status` | gate job; fails if any job above did not succeed |

While a project does not exist yet (P0) its job exits with a `::notice` instead of failing,
so `main` stays green from the first commit. The guards disappear once P1/P3 land.

**Merge policy:** merge only when `ci-status` is green.
