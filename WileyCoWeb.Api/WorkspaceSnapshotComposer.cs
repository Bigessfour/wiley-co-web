using Microsoft.EntityFrameworkCore;
using WileyCoWeb.Contracts;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyCoWeb.Api;

internal sealed class WorkspaceSnapshotComposer
{
    private readonly IDbContextFactory<AppDbContext> contextFactory;

    public WorkspaceSnapshotComposer(IDbContextFactory<AppDbContext> contextFactory)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<WorkspaceSnapshotResponse> BuildAsync(string? enterpriseName, int? fiscalYear, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var enterprises = await context.Enterprises
            .AsNoTracking()
            .Where(enterprise => !enterprise.IsDeleted)
            .OrderBy(enterprise => enterprise.Name)
            .ToListAsync(cancellationToken);

        var selectedEnterprise = enterprises.FirstOrDefault(enterprise =>
            string.Equals(enterprise.Name, enterpriseName, StringComparison.OrdinalIgnoreCase))
            ?? enterprises.FirstOrDefault()
            ?? new Enterprise();

        var budgetYears = await context.BudgetEntries
            .AsNoTracking()
            .Select(entry => entry.FiscalYear)
            .Distinct()
            .OrderBy(year => year)
            .ToListAsync(cancellationToken);

        var selectedFiscalYear = ResolveFiscalYear(fiscalYear, budgetYears);

        var customers = await context.UtilityCustomers
            .AsNoTracking()
            .OrderBy(customer => customer.AccountNumber)
            .ToListAsync(cancellationToken);

        var customerRows = customers
            .Select(customer => new CustomerRow(
                string.IsNullOrWhiteSpace(customer.DisplayName) ? customer.AccountNumber : customer.DisplayName,
                customer.CustomerTypeDescription,
                customer.ServiceLocation == WileyWidget.Models.ServiceLocation.InsideCityLimits ? "Yes" : "No"))
            .ToList();

        var serviceOptions = customerRows
            .Select(customer => customer.Service)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(service => service)
            .ToList();
        serviceOptions.Insert(0, "All Services");

        var currentRate = selectedEnterprise.CurrentRate > 0 ? selectedEnterprise.CurrentRate : 0m;
        var totalCosts = selectedEnterprise.MonthlyExpenses > 0 ? selectedEnterprise.MonthlyExpenses : 0m;
        var projectedVolume = selectedEnterprise.CitizenCount > 0 ? selectedEnterprise.CitizenCount : 0m;
        var recommendedRate = projectedVolume == 0 ? 0m : Math.Round(totalCosts / projectedVolume, 2, MidpointRounding.AwayFromZero);
        var adjustedRecommendedRate = projectedVolume == 0 ? 0m : Math.Round((totalCosts + Math.Max(0m, totalCosts * 0.08m)) / projectedVolume, 2, MidpointRounding.AwayFromZero);

        var projectionRows = BuildProjectionRows(selectedFiscalYear, currentRate, recommendedRate, adjustedRecommendedRate);
        var scenarioItems = await BuildScenarioItemsAsync(context, selectedFiscalYear, cancellationToken);

        return new WorkspaceSnapshotResponse
        {
            SelectedEnterprise = selectedEnterprise.Name,
            SelectedFiscalYear = selectedFiscalYear,
            ActiveScenarioName = $"{selectedEnterprise.Name} planning snapshot",
            CurrentRate = currentRate,
            TotalCosts = totalCosts,
            ProjectedVolume = projectedVolume,
            LastUpdatedUtc = DateTime.UtcNow.ToString("O"),
            EnterpriseOptions = enterprises.Select(enterprise => enterprise.Name).ToList(),
            FiscalYearOptions = budgetYears.Count > 0 ? budgetYears : [selectedFiscalYear],
            CustomerServiceOptions = serviceOptions,
            CustomerCityLimitOptions = ["All", "Yes", "No"],
            ScenarioItems = scenarioItems,
            CustomerRows = customerRows,
            ProjectionRows = projectionRows
        };
    }

    private static int ResolveFiscalYear(int? fiscalYear, List<int> budgetYears)
    {
        if (fiscalYear is > 0)
        {
            return fiscalYear.Value;
        }

        if (budgetYears.Count > 0)
        {
            return budgetYears[^1];
        }

        return DateTime.UtcNow.Year;
    }

    private static List<ProjectionRow> BuildProjectionRows(int fiscalYear, decimal currentRate, decimal recommendedRate, decimal adjustedRecommendedRate)
    {
        var previousYear = Math.Max(1, fiscalYear - 1);
        var nextYear = fiscalYear + 1;
        var followingYear = fiscalYear + 2;

        return
        [
            new ProjectionRow($"FY{previousYear % 100:00}", Math.Round(currentRate * 0.94m, 2, MidpointRounding.AwayFromZero)),
            new ProjectionRow($"FY{fiscalYear % 100:00}", currentRate),
            new ProjectionRow($"FY{nextYear % 100:00}", recommendedRate),
            new ProjectionRow($"FY{followingYear % 100:00}", adjustedRecommendedRate)
        ];
    }

    private static async Task<List<WorkspaceScenarioItemData>> BuildScenarioItemsAsync(AppDbContext context, int fiscalYear, CancellationToken cancellationToken)
    {
        var topDepartments = await context.BudgetEntries
            .AsNoTracking()
            .Where(entry => entry.FiscalYear == fiscalYear)
            .GroupBy(entry => entry.Department.Name)
            .Select(group => new
            {
                Name = group.Key,
                Amount = group.Sum(entry => entry.BudgetedAmount)
            })
            .OrderByDescending(group => group.Amount)
            .Take(3)
            .ToListAsync(cancellationToken);

        return topDepartments
            .Select((department, index) => new WorkspaceScenarioItemData(
                Guid.NewGuid(),
                string.IsNullOrWhiteSpace(department.Name) ? $"Priority item {index + 1}" : department.Name,
                Math.Round(Math.Max(0m, department.Amount * 0.05m), 2, MidpointRounding.AwayFromZero)))
            .ToList();
    }
}
