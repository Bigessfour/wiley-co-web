# Legacy SQL and COA seed data

These scripts and CSVs were preserved from the retired **WinForms** repository [Bigessfour/Wiley-Widget](https://github.com/Bigessfour/Wiley-Widget) at commit [`6a9cbcdd`](https://github.com/Bigessfour/Wiley-Widget/commit/6a9cbcdd7bf4e34e1e8a4d79b12c337974983178) (`main` at archive time).

They are **historical seed and chart-of-accounts import material** for Town of Wiley municipal/sewer accounts, departments, budget periods, and related upserts (for example `TownOfWileyBudget2026_Import.sql`, `coa_1691_251009113607_001.csv`, and the companion needs-review CSV).

## Use in wiley-co-web

- **Not wired** into the current API `DatabaseSeeder` or EF migration flow in this repo.
- Run manually only in controlled environments when you intentionally need to reproduce legacy import behavior.
- **Entity Framework migrations from the old repo were intentionally not copied**; schema evolution here follows `WileyCoWeb.Api` migrations only.

## Contents (from old `sql/`)

| File | Purpose (summary) |
|------|-------------------|
| `TownOfWileyBudget2026_Import.sql` | Budget 2026 import |
| `migrate_TownOfWileyBudget2026_AddMappedDepartment.sql` | Department mapping migration script |
| `insert_*` / `upsert_*` / `link_budget_entries.sql` | Municipal/sewer account and department seed/upsert |
| `coa_1691_251009113607_001*.csv` | Chart of accounts extract and needs-review list |
| `verify-chat-history.sql` | Ad-hoc verification script |
