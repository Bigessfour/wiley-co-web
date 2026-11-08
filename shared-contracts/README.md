# Shared contracts (v1)

Versioned JSON Schema (draft **2020-12**) shapes for cross-system data exchange around Town of Wiley municipal finance and utility operations.

This pack is the contract only. **No database changes** and no application wiring are required to use or validate these files.

## Wiley Widget is the financial hub

Wiley Widget (this repo’s Blazor workspace + API) is the **financial hub**: it aggregates council-packet actuals, QuickBooks exports, bank snapshots, and (later) metered usage into one council-facing workspace.

Other systems publish or consume the same JSON shapes. They do not need to share Wiley Widget’s Aurora schema.

## Publishers and consumers

| Shape | Typical publishers | Typical consumers |
| ----- | ------------------ | ----------------- |
| **Enterprise** | Wiley Widget (canonical ids/names), seed/catalog exports | Any system joining on `enterpriseId` |
| **ActualsSummary** | Council packet / budget comparison exports, QuickBooks class rollups, NAS finance packs | Wiley Widget (break-even, reserves, rate decision support) |
| **OperationalEvent** | Council check listings, QuickBooks Desktop CSV imports, NAS meeting exports | Wiley Widget import/preview pipelines; audit/reporting tools |
| **CustomerUsage** | Utility billing / meter systems (future) | Wiley Widget affordability and usage alerts; other ops apps |

Stable **string** ids (for example `tow-water-utility`) are intentional. Do not use database integer primary keys in these payloads.

Canonical id ↔ display-name mapping (including import aliases and WSD rollup rules) lives in [`enterprise-id-map.json`](enterprise-id-map.json). Use that file until the API/UI joins on `tow-*` ids directly.

| `id` | `displayName` |
| ---- | ------------- |
| `tow-water-utility` | Water Utility |
| `tow-wsd` | Wiley Sanitation District |
| `tow-trash` | Trash |
| `tow-apartments` | Apartments |
| `tow-general` | General |

`tow-trash` is a seeded workspace enterprise. The October 2026 example pack does not split trash out of the utility check listing; those rows stay under `tow-water-utility`.

## Conventions (read before consuming)

### ActualsSummary

- `period` is a **reporting month label** (`YYYY-MM`), not always the exclusive income/expense window.
- Required `amountsBasis`:
  - `fy-ytd` — income/expenses are fiscal-year-to-date through that period (council budget comparison style).
  - `monthly` — income/expenses are for that calendar month only.
- `cashBalance` is always an **as-of snapshot** for the period (typically month-end), independent of `amountsBasis`.
- When `cashBalanceAccounts` is present, `cashBalance` is the sum of only those bank labels from the bank-balances export.

### OperationalEvent

- `source` and `type` are closed enums in the schema.
- **Sign convention:** for `type=check`, `amount` is a **positive outflow** (council listing style), not a signed QuickBooks debit/credit. Convert explicitly if your ledger needs signs.
- **Do not double-count:** `type=summary` rows carry aggregate totals. Sum only `check` / `deposit` / `transfer` line items, **or** use the summary — never both.
- `type=check` requires `metadata.enterpriseId`. `type=summary` requires `metadata.grandTotal`.
- WSD enterprise and WSD general listings both publish as `enterpriseId: tow-wsd` (optional `metadata.wsdFund`: `enterprise` | `general`).

### Transport envelope

Prefer [`schemas/v1/payload-batch.schema.json`](schemas/v1/payload-batch.schema.json) for multi-item file drops or HTTPS bodies:

```json
{ "schemaVersion": "1.0.0", "itemSchema": "operational-event", "items": [ /* ... */ ] }
```

Validate the envelope, then validate each `items[]` element against the schema named by `itemSchema`. Single-object examples (`enterprise`, `actuals-summary`) stay unwrapped.

## Transport (v1)

Acceptable for v1:

- **File-based** drop (local folder, S3 object, or repo path such as `examples/`)
- **Simple HTTPS** POST/GET of JSON bodies matching these schemas

No message bus, queue, or shared database is required for v1.

Money fields are USD **numbers** with a two-decimal convention. Periods use `YYYY-MM` (for example `2026-10`). Dates on events use `YYYY-MM-DD` (pattern-enforced in schema).

## Layout

```text
shared-contracts/
  README.md
  enterprise-id-map.json
  schemas/v1/
    enterprise.schema.json
    actuals-summary.schema.json
    operational-event.schema.json
    customer-usage.schema.json
    payload-batch.schema.json
  examples/
    enterprise-water-utility.json
    actuals-summary-utility-oct-2026.json
    operational-events-oct-2026-checks.json
    customer-usage-oct-2026-sample.json
```

Each item schema sets `"$schema": "https://json-schema.org/draft/2020-12/schema"` and `$id` `https://wiley-widget.local/schemas/v1/<name>.schema.json`.

## October 2026 examples

Examples are grounded in [`Import Data/tow-october-2026/`](../Import%20Data/tow-october-2026/) (utility budget comparison, bank balances, check listings).

| File | What it shows |
| ---- | ------------- |
| `enterprise-water-utility.json` | Water Utility, FY 2026 |
| `actuals-summary-utility-oct-2026.json` | `amountsBasis: fy-ytd`; YTD income `259177.18`, YTD expenses `188843.30`; cash `152007.37` from `UTILITY` + `UTILITY SAVINGS` |
| `operational-events-oct-2026-checks.json` | PayloadBatch of representative checks plus a summary (`grandTotal` `62008.71`; by-enterprise Utility `39077.79`, General `17189.56`, Brookside `3757.99`, WSD `1983.37`) |
| `customer-usage-oct-2026-sample.json` | PayloadBatch of **synthetic** meter usage for `2026-10` (no gallons in the October import pack) |

Source CSVs (do not modify for this pack):

- `utility-budget-comparison-fy2026.csv`
- `bank-balances-oct-2026.csv`
- `utility-transactions-oct-2026.csv` (and sibling enterprise listing CSVs)

## Validation (optional)

Not wired into CI yet. Local checks can use any draft 2020-12 validator. Prefer a validator that understands `if`/`then` (OperationalEvent variants). Date fields use a regex `pattern` so they still fail closed without format plugins.

```bash
# Node (ajv-cli), if installed
npx --yes ajv-cli validate -s schemas/v1/enterprise.schema.json -d examples/enterprise-water-utility.json
npx --yes ajv-cli validate -s schemas/v1/actuals-summary.schema.json -d examples/actuals-summary-utility-oct-2026.json

# Python
python -c "import json,jsonschema; jsonschema.Draft202012Validator(json.load(open('schemas/v1/actuals-summary.schema.json'))).validate(json.load(open('examples/actuals-summary-utility-oct-2026.json')))"
```

For batch examples, validate against `payload-batch.schema.json`, then validate each `items[]` element against the named item schema.

## API ingest (minimal)

Wiley Widget API stores these payloads without touching QuickBooks import or workspace snapshots:

- `POST` / `GET` `/api/reference-data/actuals` (+ `GET .../actuals/{id}`)
- `POST` / `GET` `/api/reference-data/events` (+ `GET .../events/{id}`; body is `OperationalEvent[]`, not the PayloadBatch envelope)

See [`docs/shared-contracts-reference-data-api.md`](../docs/shared-contracts-reference-data-api.md).
