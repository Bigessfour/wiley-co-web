# Aurora PostgreSQL Reset Runbook

This runbook is the approved path for wiping and reapplying the Wiley.co Aurora PostgreSQL schema from the EF Core migration set.

## Canonical Schema Source

The canonical database shape is the EF Core migration set in `src/WileyWidget.Data/Migrations`.

Current initial PostgreSQL migration:

- `20260415122817_InitialCreate.cs`
- `AppDbContextModelSnapshot.cs`

Do not treat `docs/amplify-db-schema.sql` as the full database definition anymore. That SQL file is now only an import-pipeline reference.

## Why This Matters

The real clerk-supplied QuickBooks files in `Import Data` are aligned with the current QuickBooks import code, but the API reads more than the import tables.

Current alignment:

- `Full_TransactionList_ByDate_All.csv`
- `Full_GeneralLedger_FY2026.xlsx Util.csv`
- `Full_GeneralLedger_FY2026xlsx WSD.csv`
- The QuickBooks import endpoints accept `Transaction List by Date` and `General Ledger` exports and persist them through `import_batches`, `source_files`, and `ledger_entries`.
- Reserve analytics in `AnalyticsRepository` read from `ledger_entries`.

Current limitation:

- Workspace bootstrap, baseline editing, top-variance analysis, and scenario composition still rely on `Enterprises`, `BudgetEntries`, `MunicipalAccounts`, `UtilityCustomers`, and `BudgetSnapshots`.
- Re-importing QuickBooks files alone does not repopulate every table used by the workspace analysis surface.

Operational conclusion:

- Aurora must be rebuilt from the full EF migration, not from the import-only SQL file.
- After a reset, the database still needs enterprise and budget data alongside QuickBooks actuals if the full workspace analysis experience is expected.

## Preconditions

- AWS CLI authenticated for `us-east-2`
- Access to Secrets Manager secret `wiley-widget/api/database-url`
- `.NET SDK 9.0.313` installed
- `dotnet ef` available
- A host with network access to the private Aurora writer endpoint on `5432`
- Runtime remains configured with `Database__AllowDegradedStartup=false`
- Runtime remains configured with `Database__ApplyMigrations=false` during normal app startup

This workstation cannot currently reach the private Aurora writer endpoint directly. If you run the reset script from here, it fails before EF can open the database connection. Execute the script from a VPC-attached host, an SSM tunnel session, or another private runner that can reach the cluster. The cluster's HTTP endpoint is enabled, so a Data API-based apply path is also viable when direct TCP access is unavailable.

For this workstation, the approved non-destructive alignment path is:

```powershell
./Scripts/apply-aurora-migration-data-api.ps1 -NoBuild
```

That script generates the EF migration SQL locally, sends it through the Aurora Data API, preserves the already-imported QuickBooks tables and rows, and adds the missing workspace-analysis tables plus EF migration history.

## Reset And Reapply Workflow

Preferred path from the repository root:

```powershell
./Scripts/reset-aurora-postgres.ps1 -ResetToZero
```

That script:

1. Pulls the Aurora connection string from Secrets Manager.
2. Sets `DATABASE_URL` for the current shell.
3. Rolls the schema back to migration `0` when `-ResetToZero` is supplied.
4. Reapplies the PostgreSQL migration with `dotnet ef database update`.

If you only need to bring Aurora forward without a wipe:

```powershell
./Scripts/reset-aurora-postgres.ps1
```

## Manual Equivalent Commands

```powershell
$env:DATABASE_URL = '<aurora-connection-string>'
dotnet ef database update 0 --project src/WileyWidget.Data/WileyWidget.Data.csproj --startup-project WileyCoWeb.Api/WileyCoWeb.Api.csproj --context AppDbContext
dotnet ef database update InitialCreate --project src/WileyWidget.Data/WileyWidget.Data.csproj --startup-project WileyCoWeb.Api/WileyCoWeb.Api.csproj --context AppDbContext
```

## Post-Reset Validation

Validate both schema shape and data-path expectations.

Schema validation:

- `import_batches`
- `source_files`
- `ledger_entries`
- `trial_balance_lines`
- `budget_snapshots`
- `budget_snapshot_artifacts`
- `Enterprises`
- `BudgetEntries`
- `MunicipalAccounts`

API validation:

- `/health`
- `/api/workspace/snapshot`
- `/api/workspace/knowledge`
- `/api/imports/quickbooks/preview`
- `/api/imports/quickbooks/commit`

Data validation:

- Re-import the real clerk QuickBooks files after reset.
- Confirm `source_files` row counts and file hashes match the import event.
- Confirm `ledger_entries` receives the imported rows.
- Confirm reserve analytics return non-empty results once ledger rows exist.
- Confirm workspace snapshot and variance views still have enterprise and budget records; QuickBooks import does not create them.

## Council-Facing Risk Note

If the goal is to provide the full analysis surface to the API after a database wipe, the reset is only half the job.

The database must contain:

- imported QuickBooks ledger actuals
- enterprise baseline records
- fiscal-year budget entries
- saved budget snapshots when scenario history is expected

Without those non-import tables, the API can still ingest QuickBooks files and compute ledger-based reserve analytics, but the broader workspace analysis and scenario features will remain incomplete.