using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Entities;

namespace WileyCoWeb.Api;

/// <summary>
/// Idempotent minimal budget rows so capital gap and debt coverage panels have live FY data when the database was provisioned without budget imports.
/// </summary>
internal static class WorkspacePanelBudgetSeed
{
    internal static async Task EnsureBudgetEntriesWhenDatabaseHasNoBudgetAsync(
        AppDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (await context.BudgetEntries.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var hasWaterEnterprise = await context.Enterprises.AsNoTracking()
            .AnyAsync(e => !e.IsDeleted && e.Name == WorkspaceEnterpriseCatalog.WaterUtility, cancellationToken)
            .ConfigureAwait(false);

        if (!hasWaterEnterprise)
        {
            logger.LogWarning("Workspace panel budget seed skipped: {Enterprise} enterprise row is missing.", WorkspaceEnterpriseCatalog.WaterUtility);
            return;
        }

        var waterDepartment = await context.Departments
            .FirstOrDefaultAsync(d => d.Name == "Water", cancellationToken)
            .ConfigureAwait(false);

        if (waterDepartment is null)
        {
            waterDepartment = new Department { Name = "Water", DepartmentCode = "H2O" };
            context.Departments.Add(waterDepartment);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var fund = await context.Funds
            .FirstOrDefaultAsync(f => f.Name.Contains("Water Utility"), cancellationToken)
            .ConfigureAwait(false);

        if (fund is null)
        {
            fund = new Fund
            {
                FundCode = "401-WTR",
                Name = "Water Utility enterprise operating",
                Type = FundType.EnterpriseFund
            };
            context.Funds.Add(fund);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var fiscalYear = DateTime.UtcNow.Year;
        var start = new DateTime(fiscalYear - 1, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(fiscalYear, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        context.BudgetEntries.AddRange(
            new BudgetEntry
            {
                AccountNumber = "405.1",
                Description = "Water utility rate revenue",
                BudgetedAmount = 380_000m,
                ActualAmount = 360_000m,
                FiscalYear = fiscalYear,
                StartPeriod = start,
                EndPeriod = end,
                FundType = FundType.EnterpriseFund,
                DepartmentId = waterDepartment.Id,
                FundId = fund.Id,
                IsGASBCompliant = true
            },
            new BudgetEntry
            {
                AccountNumber = "510.1",
                Description = "Water capital project bond",
                BudgetedAmount = 120_000m,
                ActualAmount = 40_000m,
                FiscalYear = fiscalYear,
                StartPeriod = start,
                EndPeriod = end,
                FundType = FundType.CapitalProjects,
                DepartmentId = waterDepartment.Id,
                FundId = fund.Id,
                IsGASBCompliant = true
            },
            new BudgetEntry
            {
                AccountNumber = "510.2",
                Description = "Treatment plant equipment improvement",
                BudgetedAmount = 92_000m,
                ActualAmount = 12_000m,
                FiscalYear = fiscalYear,
                StartPeriod = start,
                EndPeriod = end,
                FundType = FundType.EnterpriseFund,
                DepartmentId = waterDepartment.Id,
                FundId = fund.Id,
                IsGASBCompliant = true
            },
            new BudgetEntry
            {
                AccountNumber = "520.1",
                Description = "Debt service principal",
                BudgetedAmount = 150_000m,
                ActualAmount = 148_000m,
                FiscalYear = fiscalYear,
                StartPeriod = start,
                EndPeriod = end,
                FundType = FundType.EnterpriseFund,
                DepartmentId = waterDepartment.Id,
                FundId = fund.Id,
                IsGASBCompliant = true
            },
            new BudgetEntry
            {
                AccountNumber = "520.2",
                Description = "Debt service interest",
                BudgetedAmount = 90_000m,
                ActualAmount = 85_000m,
                FiscalYear = fiscalYear,
                StartPeriod = start,
                EndPeriod = end,
                FundType = FundType.EnterpriseFund,
                DepartmentId = waterDepartment.Id,
                FundId = fund.Id,
                IsGASBCompliant = true
            });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Seeded minimal workspace panel budget entries for FY {FiscalYear}.", fiscalYear);
    }
}
