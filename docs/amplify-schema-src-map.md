# Amplify Schema to `src` Map

## Purpose

This document maps the current Amplify database schema in [`docs/amplify-db-schema.sql`](amplify-db-schema.sql) to the code under `src/` and the Blazor UI entry points under `Components/`.

The goal is to separate:

1. Code that is still valuable for the current Wiley Widget version.
2. Code that needs to be repaired so the schema can flow cleanly into the app.
3. Code that is legacy or orthogonal enough to archive once the pipeline is stable.

## Schema Shape

The schema is centered on a data-import and reporting pipeline:

- `import_batches`
- `source_file_variants`
- `source_files`
- `chart_of_accounts`
- `customers`
- `vendors`
- `ledger_entries`
- `ledger_entry_lines`
- `trial_balance_lines`
- `profit_loss_monthly_lines`
- `budget_snapshots`

That shape is different from the current `src` naming in several places. The current app still uses municipal budgeting language, customer/vendor repositories, and budget analytics, but the schema is much more explicit about ingestion, normalization, and reporting stages.

## Schema To `src` Map

<!-- markdownlint-disable MD060 -->

| Schema area       | Tables                                                   | Current `src` anchors                                                                                                                                                                                       | Status            | Notes                                                                                                                                                                        |
| ----------------- | -------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Ingestion control | `import_batches`, `source_file_variants`, `source_files` | `src/WileyWidget.Data/AppDbContext.cs`, `src/WileyWidget.Data/DatabaseSeeder.cs`, `src/WileyWidget.Services/AnalyticsPipeline.cs`                                                                           | Missing / partial | The database has a real source-file pipeline, but `src` does not yet expose a dedicated import batch and source-file model surface. This is the first repair gap.            |
| Chart of accounts | `chart_of_accounts`                                      | `src/WileyWidget.Data/AccountsRepository.cs`, `src/WileyWidget.Models/Models/MunicipalAccount.cs`, `src/WileyWidget.Business/Interfaces/IAccountsRepository.cs`                                             | Partial           | The current domain uses municipal account terminology instead of a schema-native chart of accounts model. The repo is still useful, but the naming and shape need alignment. |
| Customers         | `customers`                                              | `src/WileyWidget.Models/Amplify/AmplifySchemaEntities.cs`, `src/WileyWidget.Data/AppDbContext.cs`, `src/WileyWidget.Business/Interfaces/IAppDbContext.cs`                                                   | Partial           | `AmplifyCustomer` is now the schema-native path. The legacy utility customer repository was archived because it was not aligned to the new pipeline.                         |
| Vendors           | `vendors`                                                | `src/WileyWidget.Models/Models/Vendor.cs`, `src/WileyWidget.Data/VendorRepository.cs`, `src/WileyWidget.Business/Interfaces/IVendorRepository.cs`                                                           | Keep              | Another strong live mapping. This should survive schema repair.                                                                                                              |
| Ledger entries    | `ledger_entries`, `ledger_entry_lines`                   | `src/WileyWidget.Models/Models/Transaction.cs`, `src/WileyWidget.Models/Models/BudgetEntry.cs`, `src/WileyWidget.Data/BudgetRepository.cs`, `src/WileyWidget.Data/BudgetAnalyticsRepository.cs`             | Partial           | The app has budget-entry and transaction concepts, but it does not yet expose a ledger-entry / line-item model that matches the schema directly.                             |
| Trial balance     | `trial_balance_lines`                                    | `src/WileyWidget.Data/BudgetAnalyticsRepository.cs`, `src/WileyWidget.Models/DashboardMetric.cs`, `src/WileyWidget.Models/MonthlyRevenue.cs`                                                                | Missing / partial | The analytics layer can produce budget summaries, but there is no explicit trial-balance projection model yet.                                                               |
| Profit and loss   | `profit_loss_monthly_lines`                              | `src/WileyWidget.Data/BudgetAnalyticsRepository.cs`, `src/WileyWidget.Models/Models/TaxRevenueSummary.cs`, `src/WileyWidget.Business/Models/MonthlyRevenueAggregate.cs`                                     | Partial           | There is reporting logic, but it is still budget-centric rather than a direct monthly P&L projection pipeline.                                                               |
| Budget snapshots  | `budget_snapshots`                                       | `src/WileyWidget.Models/Models/SavedScenarioSnapshot.cs`, `src/WileyWidget.Data/ScenarioSnapshotRepository.cs`, `src/WileyWidget.Models/TownOfWileyBudget2026.cs`, `Components/Pages/BudgetDashboard.razor` | Keep              | This is the closest current match to the reporting and scenario portion of the schema.                                                                                       |

<!-- markdownlint-enable MD060 -->

## What Is Valuable Right Now

These areas are the most likely to remain in the current Wiley Widget version:

- `src/WileyWidget.Data/AppDbContext.cs`
- `src/WileyWidget.Data/BudgetRepository.cs`
- `src/WileyWidget.Data/BudgetAnalyticsRepository.cs`
- `src/WileyWidget.Models/Amplify/AmplifySchemaEntities.cs`
- `src/WileyWidget.Business/Interfaces/IAppDbContext.cs`
- `src/WileyWidget.Data/VendorRepository.cs`
- `src/WileyWidget.Data/ScenarioSnapshotRepository.cs`
- `src/WileyWidget.Data/AccountsRepository.cs`
- `src/WileyWidget.Data/DepartmentRepository.cs`
- `src/WileyWidget.Data/EnterpriseRepository.cs`
- `src/WileyWidget.Services/AILoggingService.cs`
- `src/WileyWidget.Models/Models/Vendor.cs`
- `src/WileyWidget.Models/Models/BudgetEntry.cs`
- `src/WileyWidget.Models/Models/Transaction.cs`
- `src/WileyWidget.Models/Models/SavedScenarioSnapshot.cs`
- `src/WileyWidget.Models/TownOfWileyBudget2026.cs`
- `src/WileyWidget.Models/DepartmentCurrentCharge.cs`
- `src/WileyWidget.Models/DepartmentGoal.cs`
- `src/WileyWidget.Models/TaxRevenueSummary.cs`
- `src/WileyWidget.Services/AnalyticsService.cs`
- `src/WileyWidget.Services/AnalyticsPipeline.cs`
- `src/WileyWidget.Services/FallbackDataService.cs`
- `src/WileyWidget.Services/SettingsService.cs`
- `src/WileyWidget.Services/SigNozTelemetryService.cs`
- `src/WileyWidget.Services/TelemetryLogService.cs`
- `src/WileyWidget.Services/TelemetryStartupService.cs`
- `src/WileyWidget.Business/Interfaces/IGrokRecommendationService.cs`
- `src/WileyWidget.Business/Configuration/GrokRecommendationOptions.cs`
- `src/WileyWidget.Business/Services/GrokRecommendationService.cs`
- `src/WileyWidget.Services.Abstractions/IAIService.cs`
- `src/WileyWidget.Services.Abstractions/IAILoggingService.cs`
- `src/WileyWidget.Services.Abstractions/AIServiceInterfaces.cs`
- `src/WileyWidget.Services.Abstractions/IGrokSupercomputer.cs`
- `src/WileyWidget.Services.Abstractions/IXaiModelDiscoveryService.cs`
- `src/WileyWidget.Services.Abstractions/XaiModelDescriptor.cs`
- `src/WileyWidget.Services/AILoggingService.cs`
- `src/WileyWidget.Services/AICacheWarmingService.cs`
- `src/WileyWidget.Services/GrokSupercomputer.cs`
- `src/WileyWidget.Services/NullAIService.cs`
- `src/WileyWidget.Services/NullGrokSupercomputer.cs`
- `src/WileyWidget.Services/XAIService.cs`
- `src/WileyWidget.Services/XaiModelDiscoveryService.cs`
- `src/WileyWidget.Models/Models/AIInsight.cs`
- `src/WileyWidget.Services.Abstractions/TokenResult.cs`
- `Components/Pages/BudgetDashboard.razor`
- `Components/Pages/WileyWorkspace.razor.cs`

Reasoning:

- These files either participate in the current budget/customer/vendor workflow or are directly visible to users.
- The AI and Grok files are part of the planned assistant and recommendation layer, so they should stay in the working set.
- They are the best candidates to keep while the pipeline is being reshaped around the Amplify schema.

## What Needs Repair

The schema expects a stronger ETL and reporting pipeline than the app currently exposes. The missing or weak areas are:

- Import batch tracking for file intake and lineage.
- Source file normalization and variant matching.
- A schema-native chart-of-accounts model instead of only municipal account terminology.
- Explicit ledger entry and ledger line item entities.
- Trial-balance projections.
- Monthly profit and loss projections.
- A clear handoff from the data import layer into analytics and dashboard views.

This means the next implementation pass should likely add or refactor:

- source-file entities and repositories
- mapping services from CSV/Excel imports into canonical tables
- report projection services for trial balance and P&L outputs
- dashboard view models that bind directly to those projections

## Archive Or Cut Candidates

These areas look like legacy branches or side paths that do not participate in the schema-first pipeline.

### QuickBooks / Desktop / Reporting Legacy

- `src/WileyWidget.Business/Services/QuickBooksBudgetSyncService.cs`
- `src/WileyWidget.Business/Interfaces/IQuickBooksBudgetSyncService.cs`
- `src/WileyWidget.Services.Abstractions/IQuickBooksApiClient.cs`
- `src/WileyWidget.Services.Abstractions/IQuickBooksDataService.cs`
- `src/WileyWidget.Services.Abstractions/IQuickBooksDesktopImportService.cs`
- `src/WileyWidget.Services.Abstractions/IQuickBooksService.cs`
- `src/WileyWidget.Services.Abstractions/IReportService.cs`
- `src/WileyWidget.Services/QuickBooksApiClient.cs`
- `src/WileyWidget.Services/QuickBooksService.cs`
- `src/WileyWidget.Services/QuickBooksDesktopImportService.cs`
- `src/WileyWidget.Services/IntuitDataServiceAdapter.cs`
- `src/WileyWidget.Services/Plugins/Finance/QuickBooksPlugin.cs`
- `src/WileyWidget.Services/FastReportService.cs`
- `src/WileyWidget.Services/FastReportBudgetExtensions.cs`

### Utility / Payment Legacy

- `src/WileyWidget.Business/Interfaces/IPaymentRepository.cs`
- `src/WileyWidget.Business/Interfaces/IUtilityBillRepository.cs`
- `src/WileyWidget.Business/Interfaces/IUtilityCustomerRepository.cs`
- `src/WileyWidget.Services/IWhatIfScenarioEngine.cs`
- `src/WileyWidget.Services.Abstractions/IChargeCalculatorService.cs`
- `src/WileyWidget.Data/PaymentRepository.cs`
- `src/WileyWidget.Data/UtilityBillRepository.cs`
- `src/WileyWidget.Data/UtilityCustomerRepository.cs`
- `src/WileyWidget.Services/ServiceChargeCalculatorService.cs`
- `src/WileyWidget.Services/WhatIfScenarioEngine.cs`

### AI And Grok Legacy

These are now treated as part of the active plan, not archive material.

### UI Or Support Code To Re-evaluate

- `src/WileyWidget.Business/Interfaces/IDepartmentExpenseService.cs`
- `src/WileyWidget.Business/Interfaces/IActivityLogRepository.cs`
- `src/WileyWidget.Business/Interfaces/IAuditRepository.cs`
- `src/WileyWidget.Business/Interfaces/IAppDbContext.cs`
- `src/WileyWidget.Services/ActivityFallbackDataService.cs`
- `src/WileyWidget.Services/AnalyticsRepository.cs`

These are not automatically dead, but they should be checked against the schema-first pipeline before the next archive pass.

## Recommended Repair Order

1. Add or align schema-native import entities for `import_batches`, `source_file_variants`, and `source_files`.
2. Map the canonical finance tables into the EF Core model and repository layer.
3. Add explicit ledger and reporting projections for `ledger_entries`, `ledger_entry_lines`, `trial_balance_lines`, and `profit_loss_monthly_lines`.
4. Rebind `BudgetDashboard` and related pages to the new canonical projections.
5. Archive code that is not used by the repaired pipeline.

## Notes

- `docs/src-map-and-template.md` is still useful as a broad inventory.
- This file is the schema-driven version of that inventory.
- AI and Grok files are intentionally retained because they are part of the product plan.
- The archive list here is intentionally conservative. It is better to keep a marginal file for one more pass than to delete a still-needed adapter too early.
