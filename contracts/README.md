# contracts

`openapi.json` is the **single source of truth** for the HTTP API.

## Rules (ADR-0001 §3)

1. `openapi.json` is **generated from the backend build** — never hand-edited.
2. `frontend/src/types/api.d.ts` is regenerated from it with `openapi-typescript` — never hand-edited.
3. CI (`contract-check`) fails the build when the committed types drift from the contract.

## Regenerating

```bash
# 1. Backend emits the document (P1+)
cd backend
dotnet run --project src/TelegramCrm.Api -- --generate-openapi ../contracts/openapi.json

# 2. Frontend types
cd ../frontend
pnpm gen:api
```

## Status

P0 — placeholder. The document is produced in P1 (Backend Core).
