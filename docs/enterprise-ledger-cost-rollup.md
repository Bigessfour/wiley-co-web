# Enterprise ledger cost rollup

QuickBooks `ledger_entries` are the canonical operating-expense source for workspace break-even, coverage ratio, Jarvis knowledge, and enterprise comparison views after ledger import.

## Rollup rules

| Rule                 | Treatment                                                                                                            |
| -------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Scope                | `EntryScope` must match a canonical enterprise alias from `WorkspaceEnterpriseCatalog`                               |
| Fiscal year          | `entry_date.Year` equals the selected FY, or source file name contains `FY{year}` when date is missing               |
| Expense accounts     | Prefix `5` or `6`, or Wiley GL offset codes `45`–`49` on the split side                                              |
| Revenue accounts     | Prefix `40`–`42` — excluded from cost numerator                                                                      |
| Cash-offset GL lines | When the primary account is balance sheet (`1`–`3`), classify operating expense from the split account               |
| Balance sheet        | Prefix `1`, `2`, `3` — excluded                                                                                      |
| Entry types          | `Deposit` and `Transfer` rows excluded                                                                               |
| Allocation splits    | Routed rows already carry enterprise `EntryScope` and allocated `amount` — summed as-is                              |
| Normalization        | `MonthlyExpenses` stores **average monthly** operating expense: FY expense total ÷ 12                                |
| Sign                 | Expense row amounts are summed with `Math.Abs` so credit-normal GL postings still contribute positive operating cost |

## Cost source metadata

Workspace snapshots expose `CostSource`:

- `ledger` — rolled-up QuickBooks actuals
- `snapshot` — persisted rate snapshot override
- `baseline` — seeded `Enterprise.MonthlyExpenses` when no ledger rows exist

## Refresh triggers

- QuickBooks commit (`QuickBooksImportService.CommitAsync`)
- Reference-data sample ledger import
- QuickBooks historical reroute (`/api/imports/quickbooks/reroute`)
