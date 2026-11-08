# Shared contracts — agent suggestions (do not lose)

Living handoff for Cursor / Grok prompt series. Update status as prompts land.

Last updated: 2026-11-08 (Prompts 1–4 + Town Hub + Terraform footing; ready for Prompt 5).

Related docs:

- [`shared-contracts/README.md`](../shared-contracts/README.md)
- [`docs/shared-contracts-reference-data-api.md`](shared-contracts-reference-data-api.md)
- [`docs/town-hub.md`](town-hub.md) — suite cards + local vs App Runner URLs
- [`docs/aws-low-cost-architecture.md`](aws-low-cost-architecture.md) — suite hosting under ~$50–80/month
- [`infra/terraform/README.md`](../infra/terraform/README.md) — budgets + S3 contracts + ECR + destroy

---

## Completed

### Prompt 1 — shared-contracts pack + review harden

Status: **done** (commit `abd6861`, then harden included in same pack / follow-up).

- Versioned JSON Schema draft 2020-12 under `shared-contracts/schemas/v1/`
- October 2026 TOW examples under `shared-contracts/examples/`
- Harden from Bugbot review: `amountsBasis`, sign/summary rules, `enterprise-id-map.json`, `payload-batch.schema.json`
- No app/DB changes in that prompt

### Prompt 2 — minimal reference-data API

Status: **done** (commit `3988d6d`).

- `POST`/`GET` `/api/reference-data/actuals` (+ by-id)
- `POST`/`GET` `/api/reference-data/events` (+ by-id; body is `OperationalEvent[]`)
- Table `shared_contract_documents` (Aurora / existing EF)
- `202 Accepted`; QuickBooks import / snapshots / break-even untouched
- Docs + integration tests using `shared-contracts/examples/`
- Review follow-ups already applied: table-only migration, GET-by-id, tighter validation, multi-enterprise `enterprise_id` = null, JWT 401 coverage

### Prompt 3 — Blazor read-only display of ingested reference data

Status: **done**.

- Panel `SharedContractReferenceDataPanel` at `/wiley-workspace/reference-data`
- Nav + overview card; GET actuals/events via `SharedContractReferenceDataApiService`
- Does **not** feed break-even / snapshot composers

### Town Hub (Grok UI merge — not a duplicate panel)

Status: **done**.

- `/` and `/town-hub`: suite cards (Widget, Apartments, Rural Water, Trash Truck, TIKR)
- Compact ActualsSummary + OperationalEvents tiles via existing API client
- Links to full Reference Data panel; no second grid UI
- `TownHub:Apps` in `appsettings.json` / `appsettings.Production.json` — see `docs/town-hub.md`

### Prompt 4 — File / HTTPS ingest helper (first consumer adapter)

Status: **done**.

- `Scripts/ingest-shared-contracts.sh` — file → POST actuals + events `items[]`
- Optional `--validate` via ajv; QB import untouched
- UI: **Ingest Oct 2026 examples** on Reference Data panel + POST helpers on API client
- **First live publisher:** Wiley Apartments `GET /api/exports/shared-contracts/operational-events` + `scripts/post-operating-costs-to-widget.sh` (Trash Truck skipped — no cost ledger yet)

### Terraform footing (Widget + Apartments)

Status: **done** (apply+destroy smoke on `tow` 818… succeeded; stack left destroyed).

- `infra/terraform`: budgets $80/$100, S3 contracts drop, ECR, SSM, `scripts/destroy.sh`
- No Aurora/NAT/App Runner in this state
- **`/code-review`** still useful before Prompt 6 calculator wiring

**Ready for Prompt 5** (schema/CI hardening) — non-blocking.

---

## Suggested next prompts (Cursor)

Ordered for safe sequencing. Prefer one prompt at a time.

### Prompt 5 — Optional schema/CI hardening (non-blocking)

- Wire draft 2020-12 validation into CI for `shared-contracts/examples`
- Optional: reject duplicate `(enterpriseId, period)` on actuals or document append-only policy in UI
- Optional: query filters on GET (`enterpriseId`, `period`) using existing indexes

### Prompt 6 — Wire into decision-support (only when intentional)

- Map `tow-*` ids via `enterprise-id-map.json` into workspace enterprise display names
- Use ActualsSummary (`amountsBasis: fy-ytd`) as **inputs** for break-even / reserves — do not overwrite QuickBooks ledger tables
- Never sum `type=summary` OperationalEvents with check line items
- **`/code-review` required** before merge; treat as behavior change for council math

---

## Open suggestions still worth tracking

From contracts + API reviews (not blocking Prompt 2):

1. Money as `number` can float; long-term prefer decimal-string or document IEEE risk for consumers.
2. Integration tests use InMemory `EnsureCreated` — Postgres/Testcontainers path does not exercise `jsonb` migration in CI.
3. Model snapshot may still reflect `AppSettings.XaiModel = grok-4.7` seed drift; `AddSharedContractDocuments` migration itself is table-only (do not reintroduce `UpdateData` into feature migrations).
4. Uncommitted local noise (leave alone unless asked): workspace/CSS/playwright edits, `Import Data/tow-october-2026/`.

---

## Conventions for Grok / Cursor

- Prompt 1 = contracts; Prompt 2 = API ingest; Prompt 3+ = UI / adapters / coupling
- Prefer reversible local edits + tests; confirm before push/PR if not already asked
- Do not mix unrelated workspace polish into shared-contracts commits
- When in doubt, re-read this file and `shared-contracts-reference-data-api.md` before inventing new endpoints
