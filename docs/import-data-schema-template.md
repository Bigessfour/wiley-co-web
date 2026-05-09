# Import Data Schema Template

## Source Files

The `Import Data/` folder contains the current finance source exports used to define the future AWS database schema.
Several files are duplicate entity families exported for different source contexts such as `WSD` and `Util`. For schema design, treat each family as one canonical entity set and preserve the source variant as metadata.

### Canonical file inventory

Use one canonical copy per business entity family:

- `chart-of-accounts` - source variants from `chart-of-accounts-wsd.xlsx`
- `customers` - source variants from `customers-wsd.xlsx`
- `general-ledger-fy2026` - source variants from `general-ledger-fy2026-util.xlsx` and `general-ledger-fy2026-wsd.xlsx`
- `profit-loss-by-class-monthly-fy2026` - source variants from `profit-loss-by-class-monthly-fy2026-wsd.xlsx`
- `transaction-list-by-date-all` - source variants from `transaction-list-by-date-all.xlsx`, `transaction-list-by-date-all-util.xlsx`, and `transaction-list-by-date-all-wsd.xlsx`
- `trial-balance-fy2026` - source variants from `trial-balance-fy2026-wsd.xlsx`
- `vendors` - source variants from `vendors-wsd.xlsx`

### Source variants discovered

- `chart-of-accounts-wsd.xlsx`
- `customers-wsd.xlsx`
- `general-ledger-fy2026-util.xlsx`
- `general-ledger-fy2026-wsd.xlsx`
- `profit-loss-by-class-monthly-fy2026-wsd.xlsx`
- `transaction-list-by-date-all.xlsx`
- `transaction-list-by-date-all-util.xlsx`
- `transaction-list-by-date-all-wsd.xlsx`
- `trial-balance-fy2026-wsd.xlsx`
- `vendors-wsd.xlsx`

The duplicate CSV exports, `wsddata.zip`, and the staging `seed-pass-20260421/` bundle were removed so the import set stays unambiguous.

## Header observations

These exports look like QuickBooks-derived accounting extracts.

### Transaction list

The transaction list export includes fields like:

- `Type`
- `Date`
- `Num`
- `Name`
- `Memo`
- `Account`
- `Clr`
- `Split`
- `Amount`
- `Balance`

### General ledger

The ledger export includes similar accounting columns and grouped account totals, which makes it a good source for:

- Account balances
- Journal entries
- Transaction line items
- Beginning and ending balances by account

### Duplicate handling rule

- Prefer the first normalized copy of each entity family as the schema source of truth.
- Keep `WSD` and `Util` as source tags rather than separate schema entities.
- If two files have the same logical table and the same columns, store them as variants of one source file type.
- If two files with the same family differ in columns or row semantics, split them into one canonical table plus source-specific staging metadata rather than duplicating the entity.

## Recommended AWS database shape

Because the target is AWS database storage rather than EF Core persistence, the repo should treat the database as the system of record and the app as a consumer of that schema.

The workbook validation confirmed these source families and shapes:

- `chart-of-accounts` -> account master list with balances and tax metadata.
- `customers` -> customer master list with bill-to and contact details.
- `vendors` -> vendor master list with bill-from and contact details.
- `general-ledger-fy2026` -> transaction/ledger line exports with date, type, account, split, amount, and running balance.
- `transaction-list-by-date-all` -> flattened transaction line exports with the same accounting row structure.
- `trial-balance-fy2026` -> as-of trial balance by account.
- `profit-loss-by-class-monthly-fy2026` -> monthly income and expense statement by account.

### Core tables

- `import_batches`
- `source_files`
- `source_file_variants`
- `chart_of_accounts`
- `customers`
- `vendors`
- `ledger_entries`
- `ledger_entry_lines`
- `trial_balance_lines`
- `profit_loss_monthly_lines`
- `budget_snapshots`

### Likely relationships

- `source_files` belongs to `import_batches` and captures the original file name, canonical entity family, source variant, workbook sheet, row count, column count, and file hash.
- `source_file_variants` records each distinct export flavor such as `WSD`, `Util`, or `All`.
- `chart_of_accounts`, `customers`, and `vendors` all reference `source_files` and carry the original source row number for traceability.
- `ledger_entries` is the canonical normalized table for transaction and general ledger rows.
- `ledger_entry_lines` stores split rows when a transaction is represented by multiple accounting lines.
- `trial_balance_lines` stores one account row per reporting period and source variant.
- `profit_loss_monthly_lines` stores one account row with month columns and total for the monthly P&L export.

### Amplify DB schema draft

Use a source-aware, normalized schema with the following core columns.

The canonical SQL version of this draft lives in [docs/amplify-db-schema.sql](amplify-db-schema.sql).

#### `import_batches`

- `id`
- `batch_name`
- `source_system`
- `started_at`
- `completed_at`
- `status`
- `notes`

#### `source_files`

- `id`
- `batch_id`
- `source_file_variant_id`
- `canonical_entity`
- `original_file_name`
- `normalized_file_name`
- `sheet_name`
- `file_hash`
- `row_count`
- `column_count`
- `imported_at`

#### `source_file_variants`

- `id`
- `variant_code` such as `WSD`, `UTIL`, or `ALL`
- `description`

#### `chart_of_accounts`

- `id`
- `source_file_id`
- `source_row_number`
- `account_name`
- `account_type`
- `balance_total`
- `description`
- `account_number`
- `tax_line`

#### `customers`

- `id`
- `source_file_id`
- `source_row_number`
- `customer_name`
- `bill_to`
- `primary_contact`
- `main_phone`
- `fax`
- `balance_total`

#### `vendors`

- `id`
- `source_file_id`
- `source_row_number`
- `vendor_name`
- `account_number`
- `bill_from`
- `primary_contact`
- `main_phone`
- `fax`
- `balance_total`

#### `ledger_entries`

- `id`
- `source_file_id`
- `source_row_number`
- `entry_date`
- `entry_type`
- `transaction_number`
- `name`
- `memo`
- `account_name`
- `split_account`
- `amount`
- `running_balance`
- `cleared_flag`
- `entry_scope` indicating transaction list or general ledger

#### `ledger_entry_lines`

- `id`
- `ledger_entry_id`
- `line_number`
- `account_name`
- `memo`
- `split_account`
- `amount`
- `running_balance`
- `is_split_row`

#### `trial_balance_lines`

- `id`
- `source_file_id`
- `source_row_number`
- `as_of_date`
- `account_name`
- `debit`
- `credit`
- `balance`

#### `profit_loss_monthly_lines`

- `id`
- `source_file_id`
- `source_row_number`
- `line_label`
- `line_type`
- `jan_amount`
- `feb_amount`
- `mar_amount`
- `apr_amount`
- `may_amount`
- `jun_amount`
- `jul_amount`
- `aug_amount`
- `sep_amount`
- `oct_amount`
- `nov_amount`
- `dec_amount`
- `total_amount`

## Suggested normalization of file names

Renaming is optional, but these names are easier to carry through the new file structure:

- `Full_ChartOfAccounts.xlsx WSD.xlsx` -> `chart-of-accounts-wsd.xlsx`
- `Full_Customers.xlsx WSD.xlsx` -> `customers-wsd.xlsx`
- `Full_GeneralLedger_FY2026.xlsx Util.csv` -> `general-ledger-fy2026-util.csv`
- `Full_GeneralLedger_FY2026.xlsx Util.xlsx` -> `general-ledger-fy2026-util.xlsx`
- `Full_GeneralLedger_FY2026xlsx WSD.csv` -> `general-ledger-fy2026-wsd.csv`
- `Full_GeneralLedger_FY2026xlsx WSD.xlsx` -> `general-ledger-fy2026-wsd.xlsx`
- `Full_PnL_ByClass_Monthly_FY2026.xlsx WSD.xlsx` -> `profit-loss-by-class-monthly-fy2026-wsd.xlsx`
- `Full_TransactionList_ByDate_All.csv` -> `transaction-list-by-date-all.csv`
- `Full_TransactionList_ByDate_All.xlsx` -> `transaction-list-by-date-all.xlsx`
- `Full_TransactionList_ByDate_All.xlsx Util.xlsx` -> `transaction-list-by-date-all-util.xlsx`
- `Full_TransactionList_ByDate_All.xlsx WSD.xlsx` -> `transaction-list-by-date-all-wsd.xlsx`
- `Full_TrialBalance_FY2026.xlsx WSD.xlsx` -> `trial-balance-fy2026-wsd.xlsx`
- `Full_Vendors.xlsx WSD.xlsx` -> `vendors-wsd.xlsx`

## Canonical copy rule for the database schema

Use the following canonical entities as the basis for database design:

1. `chart_of_accounts`
2. `customers`
3. `vendors`
4. `transactions`
5. `transaction_lines`
6. `general_ledger_entries`
7. `trial_balances`
8. `profit_loss_monthly`

Support tables should carry source context and import lineage:

- `import_batches`
- `source_files`
- `source_file_variants`
- `import_row_errors`
- `import_column_mappings`
- `staging_records`

The workbook gap for workspace-only customer fields stays manual:

- `customer_type`
- `service_address`
- `service_city`
- `service_state`
- `service_zip_code`
- `service_location`
- `status`
- `account_open_date`
- `phone_number`
- `email_address`
- `meter_number`
- `notes`

These fields are not backed by the current QuickBooks source exports and should be entered directly in the workspace UI when needed.

## Continuity recommendation

- Keep the raw exports untouched until the ingestion path is defined.
- Add a normalized copy only if the filenames need to be consumed programmatically or surfaced in UI.
- Prefer storing the original filename in the database as metadata instead of relying on the file name as a business key.

## Next implementation step

Build a schema-first import flow that:

1. Detects the file type.
2. Stores the raw file metadata.
3. Maps rows into a normalized AWS database schema.
4. Surfaces a review grid in Blazor before final commit.
