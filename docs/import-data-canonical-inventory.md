# Import Data Canonical Inventory

This document describes the normalized `Import Data/` folder for the next production import pass, preserving distinct source-variant workbooks where the family genuinely has separate WSD and Util inputs.

## Canonical workbook set

- `chart-of-accounts` -> `Import Data/chart-of-accounts-wsd.xlsx`
- `customers` -> `Import Data/customers-wsd.xlsx`
- `general-ledger-fy2026` -> `Import Data/general-ledger-fy2026-util.xlsx` and `Import Data/general-ledger-fy2026-wsd.xlsx`
- `profit-loss-by-class-monthly-fy2026` -> `Import Data/profit-loss-by-class-monthly-fy2026-wsd.xlsx`
- `transaction-list-by-date-all` -> `Import Data/transaction-list-by-date-all.xlsx`, `Import Data/transaction-list-by-date-all-util.xlsx`, and `Import Data/transaction-list-by-date-all-wsd.xlsx`
- `trial-balance-fy2026` -> `Import Data/trial-balance-fy2026-wsd.xlsx`
- `vendors` -> `Import Data/vendors-wsd.xlsx`

## Variant handling

- CSV duplicates were removed where a workbook variant existed.
- Treat `Util` and `WSD` as source variants, not separate logical schema families.
- Treat archive/staging artifacts as non-canonical import inputs.
- Preserve source-variant meaning in the filename so the import pipeline can distinguish `Util` and `WSD` inputs at a glance.

## Current blocker status

- The local curation step is now explicit.
- The remaining blocker is the external production import run, which still requires the real deployment workflow and target environment.
