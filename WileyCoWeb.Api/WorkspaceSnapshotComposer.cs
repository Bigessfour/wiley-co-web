using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Amplify;

namespace WileyCoWeb.Api;

internal sealed class WorkspaceSnapshotComposer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<AppDbContext> contextFactory;
    private readonly ILogger<WorkspaceSnapshotComposer> logger;

    public WorkspaceSnapshotComposer(IDbContextFactory<AppDbContext> contextFactory, ILogger<WorkspaceSnapshotComposer> logger)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WorkspaceBootstrapData> BuildAsync(string? enterpriseName, int? fiscalYear, CancellationToken cancellationToken)
    {
        logger.LogInformation("Building workspace snapshot for {Enterprise} FY {FiscalYear}", enterpriseName ?? "default", fiscalYear?.ToString() ?? "default");
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
        var rateHistory = await LoadRateHistoryAsync(context, selectedEnterprise.Name, selectedFiscalYear, cancellationToken);

        var projectionRows = BuildProjectionRows(selectedFiscalYear, currentRate, recommendedRate, rateHistory);
        var scenarioItems = await BuildScenarioItemsAsync(context, selectedFiscalYear, cancellationToken);

        logger.LogInformation(
            "Workspace snapshot composed for {Enterprise} FY {FiscalYear}: enterprises={EnterpriseCount}, customers={CustomerCount}, scenarios={ScenarioCount}, items={ScenarioItemCount}",
            selectedEnterprise.Name,
            selectedFiscalYear,
            enterprises.Count,
            customerRows.Count,
            budgetYears.Count,
            scenarioItems.Count);

        return new WorkspaceBootstrapData(
            selectedEnterprise.Name,
            selectedFiscalYear,
            $"{selectedEnterprise.Name} planning snapshot",
            currentRate,
            totalCosts,
            projectedVolume,
            DateTime.UtcNow.ToString("O"))
        {
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

    private static List<ProjectionRow> BuildProjectionRows(
        int fiscalYear,
        decimal currentRate,
        decimal recommendedRate,
        IReadOnlyList<RateHistoryPoint> rateHistory)
    {
        var previousYear = Math.Max(1, fiscalYear - 1);
        var nextYear = fiscalYear + 1;
        var followingYear = fiscalYear + 2;

        var historicalPreviousRate = rateHistory
            .Where(point => point.FiscalYear < fiscalYear)
            .OrderByDescending(point => point.FiscalYear)
            .Select(point => point.Rate)
            .FirstOrDefault();

        if (historicalPreviousRate <= 0)
        {
            historicalPreviousRate = currentRate;
        }

        var projectedStep = CalculateProjectionStep(rateHistory, currentRate, recommendedRate);
        var nextYearRate = Math.Round(currentRate + projectedStep, 2, MidpointRounding.AwayFromZero);
        var followingYearRate = Math.Round(nextYearRate + projectedStep, 2, MidpointRounding.AwayFromZero);

        return
        [
            new ProjectionRow($"FY{previousYear % 100:00}", historicalPreviousRate),
            new ProjectionRow($"FY{fiscalYear % 100:00}", currentRate),
            new ProjectionRow($"FY{nextYear % 100:00}", nextYearRate),
            new ProjectionRow($"FY{followingYear % 100:00}", followingYearRate)
        ];
    }

    private static decimal CalculateProjectionStep(IReadOnlyList<RateHistoryPoint> rateHistory, decimal currentRate, decimal recommendedRate)
    {
        var orderedRates = rateHistory
            .OrderBy(point => point.FiscalYear)
            .Select(point => point.Rate)
            .ToList();

        if (orderedRates.Count >= 2)
        {
            var deltas = new List<decimal>(orderedRates.Count - 1);

            for (var index = 1; index < orderedRates.Count; index++)
            {
                deltas.Add(orderedRates[index] - orderedRates[index - 1]);
            }

            return Math.Round(deltas.Average(), 2, MidpointRounding.AwayFromZero);
        }

        return Math.Round(recommendedRate - currentRate, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<List<RateHistoryPoint>> LoadRateHistoryAsync(AppDbContext context, string enterpriseName, int selectedFiscalYear, CancellationToken cancellationToken)
    {
        var snapshots = await context.BudgetSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.Payload != null)
            .OrderBy(snapshot => snapshot.CreatedAt)
            .Select(snapshot => new { snapshot.SnapshotDate, snapshot.Payload })
            .ToListAsync(cancellationToken);

        var rateHistory = new List<RateHistoryPoint>();

        foreach (var snapshot in snapshots)
        {
            if (!TryReadBootstrap(snapshot.Payload, out var bootstrapData))
            {
                continue;
            }

            if (!string.Equals(bootstrapData.SelectedEnterprise, enterpriseName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (bootstrapData.CurrentRate is not > 0)
            {
                continue;
            }

            var fiscalYear = bootstrapData.SelectedFiscalYear > 0
                ? bootstrapData.SelectedFiscalYear
                : (snapshot.SnapshotDate?.Year ?? selectedFiscalYear);

            rateHistory.Add(new RateHistoryPoint(fiscalYear, decimal.Round(bootstrapData.CurrentRate.Value, 2, MidpointRounding.AwayFromZero)));
        }

        return rateHistory
            .GroupBy(point => point.FiscalYear)
            .Select(group => group.OrderByDescending(point => point.Rate).First())
            .OrderBy(point => point.FiscalYear)
            .ToList();
    }

    private static bool TryReadBootstrap(string? payload, out WorkspaceBootstrapData bootstrapData)
    {
        bootstrapData = default!;

        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            bootstrapData = JsonSerializer.Deserialize<WorkspaceBootstrapData>(payload, JsonOptions)!;
            return bootstrapData is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record RateHistoryPoint(int FiscalYear, decimal Rate);

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
