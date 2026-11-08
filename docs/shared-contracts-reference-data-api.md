# Shared contracts reference-data API

Minimal ingest for cross-system JSON from [`shared-contracts/`](../shared-contracts/) without changing QuickBooks import, workspace snapshots, or break-even calculators.

## Endpoints

| Method | Path | Body | Auth | Success |
| ------ | ---- | ---- | ---- | ------- |
| `POST` | `/api/reference-data/actuals` | `ActualsSummary` | Mutating (JWT when enabled) | `202 Accepted` + `Location` `/api/reference-data/actuals/{id}` |
| `GET` | `/api/reference-data/actuals` | — | Read | `200` list of stored documents |
| `GET` | `/api/reference-data/actuals/{id}` | — | Read | `200` one document or `404` |
| `POST` | `/api/reference-data/events` | `OperationalEvent[]` | Mutating | `202 Accepted` + `Location` `/api/reference-data/events/{id}` |
| `GET` | `/api/reference-data/events` | — | Read | `200` list of stored batches |
| `GET` | `/api/reference-data/events/{id}` | — | Read | `200` one batch or `404` |

Schemas and October 2026 examples live under `shared-contracts/schemas/v1/` and `shared-contracts/examples/`. The events example file is a PayloadBatch envelope; POST `/events` expects the **`items` array** (bare `OperationalEvent[]`), not the envelope wrapper.

## Validation (v1 minimal)

- **Actuals:** requires `enterpriseId`, `period` (`YYYY-MM`), `amountsBasis` (`fy-ytd` \| `monthly`), plus numeric money fields from the body binder.
- **Events:** requires non-empty array; each item needs `source` / `type` / `date` (`YYYY-MM-DD`) / `description` / object `metadata`. Closed enums match the shared schemas. `type=check` requires `metadata.enterpriseId`; `type=summary` requires `metadata.grandTotal`.
- Duplicate POSTs for the same enterprise/period create additional rows (append-only).

## Storage

Table `shared_contract_documents` (Aurora / existing Postgres via EF):

- `kind`: `actuals-summary` or `operational-events`
- `payload`: jsonb of the posted body
- optional `enterprise_id` / `period` for indexing
  - For event batches spanning multiple enterprises, `enterprise_id` is left **null** (not “first item only”).

Does **not** write to `budget_snapshots`, ledger import tables, or QuickBooks routes (`/api/imports/quickbooks/*`, `/api/workspace/reference-data/import`).

## Migration

```bash
dotnet ef database update --project src/WileyWidget.Data/WileyWidget.Data.csproj --startup-project WileyCoWeb.Api/WileyCoWeb.Api.csproj --context AppDbContext
```

Migration name: `AddSharedContractDocuments` (table + indexes only).
