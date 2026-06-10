using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Amplify;
using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class EnterpriseLedgerCostServiceTests
{
    [Fact]
    public void CountsTowardOperatingExpense_IncludesExpenseAccounts_AndExcludesRevenue()
    {
        Assert.True(EnterpriseLedgerCostService.CountsTowardOperatingExpense(new LedgerEntry
        {
            AccountName = "510.00 Utilities",
            Amount = 1200m,
            EntryScope = WorkspaceEnterpriseCatalog.WaterUtility
        }));

        Assert.False(EnterpriseLedgerCostService.CountsTowardOperatingExpense(new LedgerEntry
        {
            AccountName = "410.00 Water Revenue",
            Amount = 5000m,
            EntryScope = WorkspaceEnterpriseCatalog.WaterUtility
        }));

        Assert.True(EnterpriseLedgerCostService.CountsTowardOperatingExpense(new LedgerEntry
        {
            AccountName = "101 · CASH IN BANK - UTILITY",
            SplitAccount = "453 · TRASH SUPPLIES/REPAIRS",
            Amount = -125.8m,
            EntryScope = WorkspaceEnterpriseCatalog.WaterUtility
        }));

        Assert.False(EnterpriseLedgerCostService.CountsTowardOperatingExpense(new LedgerEntry
        {
            EntryType = "Deposit",
            AccountName = "410.00 Water Revenue",
            Amount = 5000m,
            EntryScope = WorkspaceEnterpriseCatalog.WaterUtility
        }));
    }

    [Fact]
    public async Task ComputeAsync_SumsExpenseRows_ForMatchingEntryScope()
    {
        var contextFactory = CreateContextFactory(nameof(ComputeAsync_SumsExpenseRows_ForMatchingEntryScope));
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var batch = new ImportBatch
            {
                BatchName = "test-batch",
                SourceSystem = "quickbooks-desktop",
                Status = "completed"
            };
            var sourceFile = new SourceFile
            {
                Batch = batch,
                CanonicalEntity = "quickbooks-ledger",
                OriginalFileName = "general-ledger-fy2026-util.xlsx",
                NormalizedFileName = "general-ledger-fy2026-util.xlsx",
                FileHash = Guid.NewGuid().ToString("N"),
                ImportedAt = DateTimeOffset.UtcNow
            };
            context.ImportBatches.Add(batch);
            context.SourceFiles.Add(sourceFile);
            await context.SaveChangesAsync();

            context.LedgerEntries.Add(new LedgerEntry
            {
                SourceFileId = sourceFile.Id,
                SourceRowNumber = 1,
                EntryDate = new DateOnly(2026, 3, 15),
                AccountName = "510.00 Utilities",
                Amount = 1200m,
                EntryScope = WorkspaceEnterpriseCatalog.WaterUtility
            });
            context.LedgerEntries.Add(new LedgerEntry
            {
                SourceFileId = sourceFile.Id,
                SourceRowNumber = 2,
                EntryDate = new DateOnly(2026, 4, 10),
                AccountName = "520.00 Maintenance",
                Amount = 600m,
                EntryScope = WorkspaceEnterpriseCatalog.WaterUtility
            });
            context.LedgerEntries.Add(new LedgerEntry
            {
                SourceFileId = sourceFile.Id,
                SourceRowNumber = 3,
                EntryDate = new DateOnly(2026, 4, 10),
                AccountName = "510.00 Utilities",
                Amount = 900m,
                EntryScope = WorkspaceEnterpriseCatalog.WileySanitationDistrict
            });
            await context.SaveChangesAsync();
        }

        var service = new EnterpriseLedgerCostService(contextFactory, new ConfigurationBuilder().Build(), NullLogger<EnterpriseLedgerCostService>.Instance);
        var result = await service.ComputeAsync(WorkspaceEnterpriseCatalog.WaterUtility, 2026);

        Assert.True(result.HasLedgerData);
        Assert.Equal(2, result.MatchedRowCount);
        Assert.Equal(1800m, result.AnnualOperatingExpenses);
        Assert.Equal(150m, result.MonthlyOperatingExpenses);
    }

  [Fact]
    public async Task RefreshEnterpriseMonthlyExpensesAsync_UpdatesEnterpriseMonthlyExpenses()
    {
        var contextFactory = CreateContextFactory(nameof(RefreshEnterpriseMonthlyExpensesAsync_UpdatesEnterpriseMonthlyExpenses));
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.Enterprises.Add(new Enterprise
            {
                Name = WorkspaceEnterpriseCatalog.WaterUtility,
                CurrentRate = 25m,
                MonthlyExpenses = 98000m,
                CitizenCount = 100
            });

            var batch = new ImportBatch
            {
                BatchName = "test-batch",
                SourceSystem = "quickbooks-desktop",
                Status = "completed"
            };
            var sourceFile = new SourceFile
            {
                Batch = batch,
                CanonicalEntity = "quickbooks-ledger",
                OriginalFileName = "general-ledger-fy2026-util.xlsx",
                NormalizedFileName = "general-ledger-fy2026-util.xlsx",
                FileHash = Guid.NewGuid().ToString("N"),
                ImportedAt = DateTimeOffset.UtcNow
            };
            context.ImportBatches.Add(batch);
            context.SourceFiles.Add(sourceFile);
            await context.SaveChangesAsync();

            context.LedgerEntries.Add(new LedgerEntry
            {
                SourceFileId = sourceFile.Id,
                SourceRowNumber = 1,
                EntryDate = new DateOnly(2026, 1, 1),
                AccountName = "510.00 Utilities",
                Amount = 2400m,
                EntryScope = WorkspaceEnterpriseCatalog.WaterUtility
            });
            await context.SaveChangesAsync();
        }

        var service = new EnterpriseLedgerCostService(contextFactory, new ConfigurationBuilder().Build(), NullLogger<EnterpriseLedgerCostService>.Instance);
        var updated = await service.RefreshEnterpriseMonthlyExpensesAsync(2026);

        Assert.Equal(1, updated);

        await using var verifyContext = await contextFactory.CreateDbContextAsync();
        var enterprise = await verifyContext.Enterprises.SingleAsync(item => item.Name == WorkspaceEnterpriseCatalog.WaterUtility);
        Assert.Equal(200m, enterprise.MonthlyExpenses);
    }

    [Fact]
    public void ResolveFiscalYearsForSourceFile_UsesEntryDatesAndFileName()
    {
        var sourceFile = new SourceFile
        {
            OriginalFileName = "general-ledger-fy2025-util.xlsx",
            LedgerEntries =
            [
                new LedgerEntry { EntryDate = new DateOnly(2026, 2, 1) },
                new LedgerEntry { EntryDate = new DateOnly(2026, 8, 1) }
            ]
        };

        var fiscalYears = EnterpriseLedgerCostService.ResolveFiscalYearsForSourceFile(sourceFile);

        Assert.Contains(2025, fiscalYears);
        Assert.Contains(2026, fiscalYears);
    }

    private static IDbContextFactory<AppDbContext> CreateContextFactory(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new TestDbContextFactory(options);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options) => this.options = options;

        public AppDbContext CreateDbContext() => new(options);

        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => new(CreateDbContext());
    }
}
