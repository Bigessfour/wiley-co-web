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

## Local curl (exact)

Local ports (from `Properties/launchSettings.json` + `WileyCoWeb.Api/Properties/launchSettings.json`):

| Process | URL |
| ------- | --- |
| Blazor WASM client | `http://127.0.0.1:5230` |
| API (`WileyCoWeb.Api`) | `http://127.0.0.1:5231` |

Run from the `wiley-co-web/` repo root with the API listening on **5231**. Auth is off by default in Development (`Auth:Enabled` false).

### POST ActualsSummary

```bash
curl -sS -i -X POST "http://127.0.0.1:5231/api/reference-data/actuals" \
  -H "Content-Type: application/json" \
  --data-binary @"shared-contracts/examples/actuals-summary-utility-oct-2026.json"
```

Expect `202 Accepted` and a `Location` like `/api/reference-data/actuals/{guid}`. Body is a single `ActualsSummary` object (October 2026 utility YTD: income `259177.18`, expenses `188843.30`, cash `152007.37`).

### POST OperationalEvent[]

The example file is a PayloadBatch envelope. The API wants the bare **`items` array**:

```bash
jq -c '.items' shared-contracts/examples/operational-events-oct-2026-checks.json \
  | curl -sS -i -X POST "http://127.0.0.1:5231/api/reference-data/events" \
    -H "Content-Type: application/json" \
    --data-binary @-
```

Expect `202 Accepted` and `Location` `/api/reference-data/events/{guid}`.

### Verify

```bash
curl -sS "http://127.0.0.1:5231/api/reference-data/actuals" | jq .
curl -sS "http://127.0.0.1:5231/api/reference-data/events" | jq .
```

### Round-trip script (preferred smoke test)

Hard-coded October 2026 ActualsSummary + 4 OperationalEvents (3 checks + summary). POSTs, GETs by id, asserts money fields, exits non-zero on failure:

```bash
# From wiley-co-web/ with WileyCoWeb.Api on http://127.0.0.1:5231
./Scripts/verify-shared-contracts.sh
```

Optional: `./Scripts/verify-shared-contracts.sh --api http://127.0.0.1:5231`  
Requires `curl` + `jq`. Does **not** touch QuickBooks import, snapshots, or break-even.

## Prompt 4 — file / HTTPS ingest helper

Does **not** touch QuickBooks CSV import (`/api/imports/quickbooks/*`).

### CLI (preferred for drop-folder / automation)

```bash
# From wiley-co-web/
./Scripts/ingest-shared-contracts.sh
./Scripts/ingest-shared-contracts.sh --validate   # optional ajv via npx
WILEY_WORKSPACE_API_BASE_ADDRESS=https://your-api ./Scripts/ingest-shared-contracts.sh
```

Posts the same two fixtures as the curl section above (actuals file + events `items[]`). Requires `curl` + `jq`. Point `--dir` at another folder of the same filenames if you stage files elsewhere first.

### UI

On `/wiley-workspace/reference-data`, **Ingest Oct 2026 examples** loads the same fixtures from `wwwroot/shared-contracts/examples/` (copied from `shared-contracts/examples` at build) and POSTs them. Town Hub summary tiles read the same GETs afterward.

### First live publisher (Wiley Apartments)

**Why Apartments (not Trash Truck):** ClerkSuite already stores landlord operating costs (`UnitOperatingCost`). Trash Truck is routes/scale tickets — no fuel/repairs ledger without inventing data. Apartments export is **read-only** (rent ledger / P/L / cost CRUD unchanged).

| Apartments field | OperationalEvent |
| ---------------- | ---------------- |
| (publisher) | `source=other`, `metadata.publisherSystem=wiley-apartments` |
| amount / incurred date | `type=check`, `amount`, `date` (positive outflow) |
| category / vendor / unit | `metadata.costCategory`, `vendor`/`payee`, `unitNumber` |
| enterprise | `metadata.enterpriseId=tow-apartments` |

v1 has **no** `source=trash-truck` or `type=cost` — put system identity in metadata.

```bash
# Live: ClerkSuite on 5077 + Widget API on 5231 (from wiley-co-web/)
PERIOD=2026-10 ./tools/post-apartments-operating-costs.sh

# Offline CSV adapter (sample rows included)
./tools/csv-to-reference-data-events.sh tools/sample-apartments-operating-costs.csv
```

Also: `Wiley_Apartments/docs/shared-contracts-export.md` and `scripts/post-operating-costs-to-widget.sh`.