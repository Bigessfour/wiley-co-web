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
    private const string RateSnapshotRecordPrefix = "RecordType:RateSnapshot";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<AppDbContext> contextFactory;
    private readonly ILogger<WorkspaceSnapshotComposer> logger;

    public WorkspaceSnapshotComposer(IDbContextFactory<AppDbContext> contextFactory, ILogger<WorkspaceSnapshotComposer> logger)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<WorkspaceBootstrapData> BuildAsync(string? enterpriseName, int? fiscalYear, CancellationToken cancellationToken)
    {
        return BuildAsyncCore(enterpriseName, fiscalYear, cancellationToken);
    }

    private async Task<WorkspaceBootstrapData> BuildAsyncCore(string? enterpriseName, int? fiscalYear, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Building workspace snapshot for {Enterprise} FY {FiscalYear}",
            DescribeEnterpriseName(enterpriseName),
            DescribeFiscalYear(fiscalYear));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildWorkspaceBootstrapDataAsync(context, enterpriseName, fiscalYear, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkspaceBootstrapData> BuildWorkspaceBootstrapDataAsync(
        AppDbContext context,
        string? enterpriseName,
        int? fiscalYear,
        CancellationToken cancellationToken)
    {
        var buildContext = await LoadWorkspaceSnapshotBuildContextAsync(context, enterpriseName, fiscalYear, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Workspace snapshot composed for {Enterprise} FY {FiscalYear}: enterprises={EnterpriseCount}, customers={CustomerCount}, scenarios={ScenarioCount}, items={ScenarioItemCount}",
            buildContext.SelectedEnterprise.Name,
            buildContext.SelectedFiscalYear,
            buildContext.Enterprises.Count,
            buildContext.CustomerRows.Count,
            buildContext.BudgetYears.Count,
            buildContext.ScenarioItems.Count);

        return CreateWorkspaceBootstrapData(buildContext);
    }

    private async Task<WorkspaceSnapshotBuildContext> LoadWorkspaceSnapshotBuildContextAsync(
        AppDbContext context,
        string? enterpriseName,
        int? fiscalYear,
        CancellationToken cancellationToken)
    {
        var selectionData = await LoadWorkspaceSnapshotSelectionAsync(context, enterpriseName, fiscalYear, cancellationToken).ConfigureAwait(false);
        var derivedData = await LoadWorkspaceSnapshotDerivedDataAsync(context, selectionData, cancellationToken).ConfigureAwait(false);
        var reserveTrajectory = BuildWorkspaceReserveTrajectory(selectionData.SelectedEnterprise, selectionData.SelectedFiscalYear, derivedData.CurrentRate, derivedData.TotalCosts, derivedData.ProjectedVolume, derivedData.RecommendedRate, derivedData.ProjectionRows, selectionData.PersistedSnapshot?.ReserveTrajectory);

        return new WorkspaceSnapshotBuildContext(
            selectionData.Enterprises,
            selectionData.SelectedEnterprise,
            selectionData.BudgetYears,
            selectionData.SelectedFiscalYear,
            derivedData.CustomerRows,
            derivedData.ServiceOptions,
            derivedData.CurrentRate,
            derivedData.TotalCosts,
            derivedData.ProjectedVolume,
            derivedData.RecommendedRate,
            derivedData.RateHistory,
            derivedData.ProjectionRows,
            derivedData.ScenarioItems,
            reserveTrajectory);
    }

    private async Task<WorkspaceSnapshotSelectionData> LoadWorkspaceSnapshotSelectionAsync(
        AppDbContext context,
        string? enterpriseName,
        int? fiscalYear,
        CancellationToken cancellationToken)
    {
        var enterprises = await LoadEnterprisesAsync(context, cancellationToken);
        var selectedEnterprise = SelectEnterprise(enterprises, enterpriseName);
        var budgetYears = await LoadBudgetYearsAsync(context, cancellationToken);
        var selectedFiscalYear = ResolveFiscalYear(fiscalYear, budgetYears);
        var persistedSnapshot = await LoadLatestRateSnapshotAsync(context, selectedEnterprise.Name, selectedFiscalYear, cancellationToken);

        return new WorkspaceSnapshotSelectionData(enterprises, selectedEnterprise, budgetYears, selectedFiscalYear, persistedSnapshot);
    }

    private async Task<WorkspaceSnapshotDerivedData> LoadWorkspaceSnapshotDerivedDataAsync(
        AppDbContext context,
        WorkspaceSnapshotSelectionData selectionData,
        CancellationToken cancellationToken)
    {
        var customerData = await LoadWorkspaceSnapshotCustomerDataAsync(context, selectionData, cancellationToken).ConfigureAwait(false);
        var rateData = await LoadWorkspaceSnapshotRateDataAsync(context, selectionData, cancellationToken).ConfigureAwait(false);

        return new WorkspaceSnapshotDerivedData(
            customerData.CustomerRows,
            customerData.ServiceOptions,
            rateData.CurrentRate,
            rateData.TotalCosts,
            rateData.ProjectedVolume,
            rateData.RecommendedRate,
            rateData.RateHistory,
            rateData.ProjectionRows,
            rateData.ScenarioItems);
    }

    private async Task<WorkspaceSnapshotCustomerData> LoadWorkspaceSnapshotCustomerDataAsync(
        AppDbContext context,
        WorkspaceSnapshotSelectionData selectionData,
        CancellationToken cancellationToken)
    {
        var customers = await LoadCustomersAsync(context, selectionData.SelectedEnterprise.Name, cancellationToken);
        var customerRows = BuildCustomerRows(selectionData.PersistedSnapshot, customers);
        var serviceOptions = BuildServiceOptions(customerRows);

        return new WorkspaceSnapshotCustomerData(customerRows, serviceOptions);
    }

    private async Task<WorkspaceSnapshotRateData> LoadWorkspaceSnapshotRateDataAsync(
        AppDbContext context,
        WorkspaceSnapshotSelectionData selectionData,
        CancellationToken cancellationToken)
    {
        var rateMetrics = await LoadWorkspaceSnapshotRateMetricsAsync(context, selectionData, cancellationToken).ConfigureAwait(false);
        var rateOutputs = await LoadWorkspaceSnapshotRateOutputsAsync(context, selectionData, rateMetrics, cancellationToken).ConfigureAwait(false);

        return new WorkspaceSnapshotRateData(
            rateMetrics.CurrentRate,
            rateMetrics.TotalCosts,
            rateMetrics.ProjectedVolume,
            rateMetrics.RecommendedRate,
            rateMetrics.RateHistory,
            rateOutputs.ProjectionRows,
            rateOutputs.ScenarioItems);
    }

    private async Task<WorkspaceSnapshotRateMetrics> LoadWorkspaceSnapshotRateMetricsAsync(
        AppDbContext context,
        WorkspaceSnapshotSelectionData selectionData,
        CancellationToken cancellationToken)
    {
        var (currentRate, totalCosts, projectedVolume, recommendedRate) = CalculateRates(selectionData.SelectedEnterprise, selectionData.PersistedSnapshot);
        var rateHistory = await LoadRateHistoryAsync(context, selectionData.SelectedEnterprise.Name, selectionData.SelectedFiscalYear, cancellationToken);

        return new WorkspaceSnapshotRateMetrics(currentRate, totalCosts, projectedVolume, recommendedRate, rateHistory);
    }

    private async Task<WorkspaceSnapshotRateOutputs> LoadWorkspaceSnapshotRateOutputsAsync(
        AppDbContext context,
        WorkspaceSnapshotSelectionData selectionData,
        WorkspaceSnapshotRateMetrics rateMetrics,
        CancellationToken cancellationToken)
    {
        var projectionRows = await LoadWorkspaceSnapshotProjectionRowsAsync(selectionData, rateMetrics).ConfigureAwait(false);
        var scenarioItems = await LoadWorkspaceSnapshotScenarioItemsAsync(context, selectionData, cancellationToken).ConfigureAwait(false);

        return new WorkspaceSnapshotRateOutputs(projectionRows, scenarioItems);
    }

    private static Task<List<ProjectionRow>> LoadWorkspaceSnapshotProjectionRowsAsync(
        WorkspaceSnapshotSelectionData selectionData,
        WorkspaceSnapshotRateMetrics rateMetrics)
    {
        var projectionRows = selectionData.PersistedSnapshot?.ProjectionRows is { Count: > 0 }
            ? BuildProjectionRowsFromPersisted(selectionData.PersistedSnapshot)
            : BuildProjectionRows(selectionData.SelectedFiscalYear, rateMetrics.CurrentRate, rateMetrics.RecommendedRate, rateMetrics.RateHistory);

        return Task.FromResult(projectionRows);
    }

    private async Task<List<WorkspaceScenarioItemData>> LoadWorkspaceSnapshotScenarioItemsAsync(
        AppDbContext context,
        WorkspaceSnapshotSelectionData selectionData,
        CancellationToken cancellationToken)
    {
        return selectionData.PersistedSnapshot?.ScenarioItems is { Count: > 0 }
            ? BuildScenarioItemsFromPersisted(selectionData.PersistedSnapshot)
            : await BuildScenarioItemsAsync(selectionData.SelectedEnterprise).ConfigureAwait(false);
    }

    private static WorkspaceBootstrapData CreateWorkspaceBootstrapData(WorkspaceSnapshotBuildContext buildContext)
    {
        var breakEvenQuadrants = BuildBreakEvenQuadrants(buildContext.Enterprises, buildContext.ProjectionRows);
        var apartmentUnitTypes = BuildApartmentUnitTypes(buildContext.Enterprises);

        return new WorkspaceBootstrapData(
            buildContext.SelectedEnterprise.Name,
            buildContext.SelectedFiscalYear,
            $"{buildContext.SelectedEnterprise.Name} planning snapshot",
            buildContext.CurrentRate,
            buildContext.TotalCosts,
            buildContext.ProjectedVolume,
            DateTime.UtcNow.ToString("O"))
        {
            EnterpriseOptions = buildContext.Enterprises.Select(enterprise => enterprise.Name).ToList(),
            FiscalYearOptions = buildContext.BudgetYears.Count > 0 ? buildContext.BudgetYears : [buildContext.SelectedFiscalYear],
            CustomerServiceOptions = buildContext.ServiceOptions,
            CustomerCityLimitOptions = ["All", "Yes", "No"],
            ScenarioItems = buildContext.ScenarioItems,
            CustomerRows = buildContext.CustomerRows,
            ProjectionRows = buildContext.ProjectionRows,
            BreakEvenQuadrants = breakEvenQuadrants,
            ApartmentUnitTypes = apartmentUnitTypes,
            ReserveTrajectory = buildContext.ReserveTrajectory
        };
    }

    private WorkspaceReserveTrajectoryData BuildWorkspaceReserveTrajectory(
        Enterprise selectedEnterprise,
        int selectedFiscalYear,
        decimal currentRate,
        decimal totalCosts,
        decimal projectedVolume,
        decimal recommendedRate,
        IReadOnlyList<ProjectionRow> projectionRows,
        WorkspaceReserveTrajectoryData? persistedTrajectory)
    {
        if (persistedTrajectory is { ForecastPoints.Count: > 0 })
        {
            return NormalizeReserveTrajectory(persistedTrajectory);
        }

        var monthlyReserveBaseline = Math.Round(Math.Max(0m, totalCosts) * 6m, 2, MidpointRounding.AwayFromZero);
        var recommendedReserveLevel = Math.Round(Math.Max(monthlyReserveBaseline, totalCosts * 9m), 2, MidpointRounding.AwayFromZero);
        var reserveTrend = Math.Round(Math.Max(0m, recommendedRate - currentRate) * Math.Max(1m, projectedVolume) * 0.04m, 2, MidpointRounding.AwayFromZero);
        var forecastYears = Math.Max(5, projectionRows.Count);
        var confidenceInterval = Math.Round(monthlyReserveBaseline * 0.08m, 2, MidpointRounding.AwayFromZero);

        var forecastPoints = Enumerable.Range(1, forecastYears)
            .Select(index => new WorkspaceReserveTrajectoryPointData(
                new DateTime(selectedFiscalYear + index, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                Math.Round(monthlyReserveBaseline + (reserveTrend * index), 2, MidpointRounding.AwayFromZero),
                confidenceInterval))
            .ToList();

        var riskAssessment = monthlyReserveBaseline < recommendedReserveLevel * 0.5m
            ? "High"
            : monthlyReserveBaseline < recommendedReserveLevel
                ? "Moderate"
                : "Low";

        logger.LogInformation(
            "Built reserve trajectory for {Enterprise} FY {FiscalYear} using derived snapshot data.",
            selectedEnterprise.Name,
            selectedFiscalYear);

        return new WorkspaceReserveTrajectoryData(
            monthlyReserveBaseline,
            recommendedReserveLevel,
            riskAssessment,
            forecastPoints);
    }

    private static WorkspaceReserveTrajectoryData NormalizeReserveTrajectory(WorkspaceReserveTrajectoryData trajectory)
    {
        return new WorkspaceReserveTrajectoryData(
            trajectory.CurrentReserves,
            trajectory.RecommendedReserveLevel,
            string.IsNullOrWhiteSpace(trajectory.RiskAssessment) ? "Unavailable" : trajectory.RiskAssessment.Trim(),
            trajectory.ForecastPoints
                .Select(point => new WorkspaceReserveTrajectoryPointData(point.DateUtc, point.PredictedReserves, point.ConfidenceInterval))
                .ToList());
    }

    private async Task<List<Enterprise>> LoadEnterprisesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var enterprises = await context.Enterprises
            .Include(enterprise => enterprise.ApartmentUnitTypes)
            .AsNoTracking()
            .Where(enterprise => !enterprise.IsDeleted)
            .ToListAsync(cancellationToken);

        return enterprises
            .OrderBy(enterprise => GetEnterpriseSortOrder(enterprise.Name))
            .ThenBy(enterprise => enterprise.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Enterprise SelectEnterprise(List<Enterprise> enterprises, string? enterpriseName)
    {
        return enterprises.FirstOrDefault(enterprise =>
            string.Equals(enterprise.Name, enterpriseName, StringComparison.OrdinalIgnoreCase))
            ?? enterprises.FirstOrDefault()
            ?? new Enterprise();
    }

    private async Task<List<int>> LoadBudgetYearsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        return await context.BudgetEntries
            .AsNoTracking()
            .Select(entry => entry.FiscalYear)
            .Distinct()
            .OrderBy(year => year)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<UtilityCustomer>> LoadCustomersAsync(AppDbContext context, string selectedEnterpriseName, CancellationToken cancellationToken)
    {
        return (await context.UtilityCustomers
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Where(customer => CustomerBelongsToEnterprise(customer, selectedEnterpriseName))
            .OrderBy(customer => string.IsNullOrWhiteSpace(customer.DisplayName) ? customer.AccountNumber : customer.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(customer => customer.AccountNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<CustomerRow> BuildCustomerRows(WorkspaceBootstrapData? persistedSnapshot, List<UtilityCustomer> customers)
    {
        return customers.Count > 0
            ? BuildCustomerRowsFromUtilities(customers)
            : persistedSnapshot?.CustomerRows is { Count: > 0 }
                ? BuildPersistedCustomerRows(persistedSnapshot)
                : [];
    }

    private static List<CustomerRow> BuildPersistedCustomerRows(WorkspaceBootstrapData persistedSnapshot)
    {
        return persistedSnapshot.CustomerRows!
            .Select(row => new CustomerRow(row.Name, row.Service, row.CityLimits))
            .ToList();
    }

    private static List<CustomerRow> BuildCustomerRowsFromUtilities(List<UtilityCustomer> customers)
    {
        return customers
            .Select(customer => new CustomerRow(
                string.IsNullOrWhiteSpace(customer.DisplayName) ? customer.AccountNumber : customer.DisplayName,
                customer.CustomerTypeDescription,
                customer.ServiceLocation == WileyWidget.Models.ServiceLocation.InsideCityLimits ? "Yes" : "No"))
            .ToList();
    }

    private static List<string> BuildServiceOptions(List<CustomerRow> customerRows)
    {
        var serviceOptions = customerRows
            .Select(customer => customer.Service)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(service => service)
            .ToList();
        serviceOptions.Insert(0, "All Services");
        return serviceOptions;
    }

    private static (decimal CurrentRate, decimal TotalCosts, decimal ProjectedVolume, decimal RecommendedRate) CalculateRates(Enterprise selectedEnterprise, WorkspaceBootstrapData? persistedSnapshot)
    {
        return (
            ResolveCurrentRate(selectedEnterprise, persistedSnapshot),
            ResolveTotalCosts(selectedEnterprise, persistedSnapshot),
            ResolveProjectedVolume(selectedEnterprise, persistedSnapshot),
            CalculateRecommendedRate(
                ResolveTotalCosts(selectedEnterprise, persistedSnapshot),
                ResolveProjectedVolume(selectedEnterprise, persistedSnapshot)));
    }

    private static decimal ResolveCurrentRate(Enterprise selectedEnterprise, WorkspaceBootstrapData? persistedSnapshot)
    {
        return ResolveRateValue(persistedSnapshot?.CurrentRate, selectedEnterprise.CurrentRate);
    }

    private static decimal ResolveTotalCosts(Enterprise selectedEnterprise, WorkspaceBootstrapData? persistedSnapshot)
    {
        return ResolveRateValue(persistedSnapshot?.TotalCosts, selectedEnterprise.MonthlyExpenses);
    }

    private static decimal ResolveProjectedVolume(Enterprise selectedEnterprise, WorkspaceBootstrapData? persistedSnapshot)
    {
        return ResolveRateValue(persistedSnapshot?.ProjectedVolume, selectedEnterprise.CitizenCount);
    }

    private static decimal ResolveRateValue(decimal? persistedValue, decimal fallbackValue)
    {
        return persistedValue is > 0m
            ? persistedValue.Value
            : fallbackValue > 0
                ? fallbackValue
                : 0m;
    }

    private static decimal CalculateRecommendedRate(decimal totalCosts, decimal projectedVolume)
    {
        return projectedVolume == 0 ? 0m : Math.Round(totalCosts / projectedVolume, 2, MidpointRounding.AwayFromZero);
    }

    private static List<ProjectionRow> BuildProjectionRowsFromPersisted(WorkspaceBootstrapData persistedSnapshot)
    {
        ArgumentNullException.ThrowIfNull(persistedSnapshot);
        return persistedSnapshot.ProjectionRows!
            .Select(row => new ProjectionRow(row.Year, row.Rate))
            .ToList();
    }

    private static List<WorkspaceScenarioItemData> BuildScenarioItemsFromPersisted(WorkspaceBootstrapData persistedSnapshot)
    {
        ArgumentNullException.ThrowIfNull(persistedSnapshot);
        return persistedSnapshot.ScenarioItems!
            .Select(item => new WorkspaceScenarioItemData(item.Id, item.Name, item.Cost))
            .ToList();
    }

    private static List<BreakEvenQuadrantData> BuildBreakEvenQuadrants(List<Enterprise> enterprises, IReadOnlyList<ProjectionRow> projectionRows)
    {
        return enterprises
            .Where(enterprise => !enterprise.IsDeleted)
            .OrderBy(enterprise => GetEnterpriseSortOrder(enterprise.Name))
            .ThenBy(enterprise => enterprise.Name, StringComparer.OrdinalIgnoreCase)
            .Select(enterprise => BuildBreakEvenQuadrant(enterprise, projectionRows))
            .ToList();
    }

    private static BreakEvenQuadrantData BuildBreakEvenQuadrant(Enterprise enterprise, IReadOnlyList<ProjectionRow> projectionRows)
    {
        var effectiveCustomers = Math.Max(1m, enterprise.EffectiveCustomerCount);
        var breakEvenRate = Math.Round(enterprise.MonthlyExpenses / effectiveCustomers, 2, MidpointRounding.AwayFromZero);
        var monthlyRevenue = Math.Round(enterprise.MonthlyRevenue, 2, MidpointRounding.AwayFromZero);
        var monthlyBalance = Math.Round(enterprise.MonthlyBalance, 2, MidpointRounding.AwayFromZero);
        var expensesPerCustomer = Math.Round(enterprise.MonthlyExpenses / effectiveCustomers, 2, MidpointRounding.AwayFromZero);

        var seriesPoints = BuildBreakEvenSeriesPoints(projectionRows, enterprise.CurrentRate, breakEvenRate, expensesPerCustomer);

        return new BreakEvenQuadrantData(
            enterprise.Name,
            enterprise.Type ?? string.Empty,
            enterprise.CurrentRate,
            enterprise.MonthlyExpenses,
            monthlyRevenue,
            monthlyBalance,
            breakEvenRate,
            effectiveCustomers,
            seriesPoints);
    }

    private static List<BreakEvenSeriesPoint> BuildBreakEvenSeriesPoints(IReadOnlyList<ProjectionRow> projectionRows, decimal currentRate, decimal breakEvenRate, decimal expensesPerCustomer)
    {
        if (projectionRows.Count == 0)
        {
            return
            [
                new BreakEvenSeriesPoint("Current", currentRate, expensesPerCustomer, breakEvenRate),
                new BreakEvenSeriesPoint("Break-Even", breakEvenRate, expensesPerCustomer, breakEvenRate)
            ];
        }

        var count = Math.Max(1, projectionRows.Count - 1);

        return projectionRows.Select((row, index) =>
        {
            var progress = (decimal)index / count;
            var revenue = Math.Round(currentRate + ((breakEvenRate - currentRate) * progress), 2, MidpointRounding.AwayFromZero);
            return new BreakEvenSeriesPoint(row.Year, revenue, expensesPerCustomer, breakEvenRate);
        }).ToList();
    }

    private static List<ApartmentUnitTypeData> BuildApartmentUnitTypes(List<Enterprise> enterprises)
    {
        var apartmentEnterprise = enterprises.FirstOrDefault(enterprise => string.Equals(enterprise.Name, "Apartments", StringComparison.OrdinalIgnoreCase));
        if (apartmentEnterprise is null)
        {
            return
            [
                new ApartmentUnitTypeData(Guid.Parse("b94d0f45-1f42-4b4d-93d7-6e9dbe3a1c01"), "2 Bedroom", 2, 8, 444.44m),
                new ApartmentUnitTypeData(Guid.Parse("b94d0f45-1f42-4b4d-93d7-6e9dbe3a1c02"), "3 Bedroom", 3, 8, 555.55m)
            ];
        }

        var unitTypes = apartmentEnterprise.ApartmentUnitTypes
            .Where(unitType => !unitType.IsDeleted)
            .Select(unitType => new ApartmentUnitTypeData(
                Guid.NewGuid(),
                unitType.Name,
                unitType.BedroomCount,
                unitType.UnitCount,
                unitType.MonthlyRent))
            .ToList();

        return unitTypes.Count > 0
            ? unitTypes
            :
            [
                new ApartmentUnitTypeData(Guid.Parse("b94d0f45-1f42-4b4d-93d7-6e9dbe3a1c01"), "2 Bedroom", 2, 8, 444.44m),
                new ApartmentUnitTypeData(Guid.Parse("b94d0f45-1f42-4b4d-93d7-6e9dbe3a1c02"), "3 Bedroom", 3, 8, 555.55m)
            ];
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
        return BuildProjectionRowsCore(fiscalYear, currentRate, recommendedRate, rateHistory);
    }

    private static List<ProjectionRow> BuildProjectionRowsCore(
        int fiscalYear,
        decimal currentRate,
        decimal recommendedRate,
        IReadOnlyList<RateHistoryPoint> rateHistory)
    {
        var previousYear = Math.Max(1, fiscalYear - 1);
        var nextYear = fiscalYear + 1;
        var followingYear = fiscalYear + 2;

        var historicalPreviousRate = ResolveHistoricalProjectionRate(fiscalYear, currentRate, rateHistory);

        var projectedStep = Math.Max(0m, CalculateProjectionStep(rateHistory, currentRate, recommendedRate));
        var nextYearRate = Math.Round(Math.Max(currentRate, currentRate + projectedStep), 2, MidpointRounding.AwayFromZero);
        var followingYearRate = Math.Round(Math.Max(nextYearRate, nextYearRate + projectedStep), 2, MidpointRounding.AwayFromZero);

        return
        [
            new ProjectionRow($"FY{previousYear % 100:00}", historicalPreviousRate),
            new ProjectionRow($"FY{fiscalYear % 100:00}", currentRate),
            new ProjectionRow($"FY{nextYear % 100:00}", nextYearRate),
            new ProjectionRow($"FY{followingYear % 100:00}", followingYearRate)
        ];
    }

    private static decimal ResolveHistoricalProjectionRate(int fiscalYear, decimal currentRate, IReadOnlyList<RateHistoryPoint> rateHistory)
    {
        var historicalPreviousRate = rateHistory
            .Where(point => point.FiscalYear < fiscalYear)
            .OrderByDescending(point => point.FiscalYear)
            .Select(point => point.Rate)
            .FirstOrDefault();

        if (historicalPreviousRate <= 0)
        {
            return currentRate;
        }

        return historicalPreviousRate > currentRate ? currentRate : historicalPreviousRate;
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
        return await LoadRateHistoryCoreAsync(context, enterpriseName, selectedFiscalYear, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<RateHistoryPoint>> LoadRateHistoryCoreAsync(AppDbContext context, string enterpriseName, int selectedFiscalYear, CancellationToken cancellationToken)
    {
        var snapshots = await LoadRateSnapshotEnvelopesAsync(context, cancellationToken);
        return NormalizeRateHistory(BuildRateHistoryPoints(snapshots, enterpriseName, selectedFiscalYear));
    }

    private static List<RateHistoryPoint> BuildRateHistoryPoints(
        IReadOnlyList<RateSnapshotEnvelope> snapshots,
        string enterpriseName,
        int selectedFiscalYear)
    {
        return snapshots
            .Select(snapshot => TryCreateRateHistoryPoint(snapshot, enterpriseName, selectedFiscalYear))
            .Where(point => point is not null)
            .Select(point => point!)
            .ToList();
    }

    private static RateHistoryPoint? TryCreateRateHistoryPoint(RateSnapshotEnvelope snapshot, string enterpriseName, int selectedFiscalYear)
    {
        if (!TryGetMatchingBootstrapData(snapshot, enterpriseName, out var bootstrapData))
        {
            return null;
        }

        var fiscalYear = ResolveRateHistoryFiscalYear(snapshot, bootstrapData, selectedFiscalYear);

        return new RateHistoryPoint(fiscalYear, decimal.Round(bootstrapData.CurrentRate.GetValueOrDefault(), 2, MidpointRounding.AwayFromZero));
    }

    private static bool TryGetMatchingBootstrapData(RateSnapshotEnvelope snapshot, string enterpriseName, out WorkspaceBootstrapData bootstrapData)
    {
        if (!TryReadBootstrap(snapshot.Payload, out bootstrapData))
        {
            return false;
        }

        return string.Equals(bootstrapData.SelectedEnterprise, enterpriseName, StringComparison.OrdinalIgnoreCase)
            && bootstrapData.CurrentRate is > 0;
    }

    private static int ResolveRateHistoryFiscalYear(RateSnapshotEnvelope snapshot, WorkspaceBootstrapData bootstrapData, int selectedFiscalYear)
    {
        if (bootstrapData.SelectedFiscalYear > 0)
        {
            return bootstrapData.SelectedFiscalYear;
        }

        if (snapshot.SnapshotDate.HasValue)
        {
            return snapshot.SnapshotDate.Value.Year;
        }

        return selectedFiscalYear;
    }

    private static string DescribeEnterpriseName(string? enterpriseName)
        => string.IsNullOrWhiteSpace(enterpriseName) ? "default" : enterpriseName;

    private static string DescribeFiscalYear(int? fiscalYear)
        => fiscalYear?.ToString() ?? "default";

    private static List<RateHistoryPoint> NormalizeRateHistory(List<RateHistoryPoint> rateHistory)
    {
        return rateHistory
            .GroupBy(point => point.FiscalYear)
            .Select(group => group.OrderByDescending(point => point.Rate).First())
            .OrderBy(point => point.FiscalYear)
            .ToList();
    }

    private static async Task<WorkspaceBootstrapData?> LoadLatestRateSnapshotAsync(AppDbContext context, string enterpriseName, int fiscalYear, CancellationToken cancellationToken)
    {
        return await LoadLatestRateSnapshotCoreAsync(context, enterpriseName, fiscalYear, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<WorkspaceBootstrapData?> LoadLatestRateSnapshotCoreAsync(AppDbContext context, string enterpriseName, int fiscalYear, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(enterpriseName) || fiscalYear <= 0)
        {
            return null;
        }

        var snapshots = await LoadRateSnapshotEnvelopesAsync(context, cancellationToken);
        return FindLatestRateSnapshot(snapshots, enterpriseName, fiscalYear);
    }

    private static WorkspaceBootstrapData? FindLatestRateSnapshot(
        IReadOnlyList<RateSnapshotEnvelope> snapshots,
        string enterpriseName,
        int fiscalYear)
    {
        foreach (var snapshot in snapshots)
        {
            if (!TryGetMatchingBootstrapData(snapshot, enterpriseName, out var bootstrapData))
            {
                continue;
            }

            if (bootstrapData.SelectedFiscalYear != fiscalYear)
            {
                continue;
            }

            return bootstrapData;
        }

        return null;
    }

    private static async Task<List<RateSnapshotEnvelope>> LoadRateSnapshotEnvelopesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        return await context.BudgetSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.Payload != null
                && snapshot.Notes != null
                && snapshot.Notes.Contains(RateSnapshotRecordPrefix))
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .Select(snapshot => new RateSnapshotEnvelope(snapshot.SnapshotDate, snapshot.Payload))
            .ToListAsync(cancellationToken);
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

    private static int GetEnterpriseSortOrder(string? enterpriseName)
    {
        for (var index = 0; index < WorkspaceEnterpriseSeedCatalog.All.Count; index++)
        {
            if (string.Equals(WorkspaceEnterpriseSeedCatalog.All[index].Name, enterpriseName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static bool CustomerBelongsToEnterprise(UtilityCustomer customer, string selectedEnterpriseName)
    {
        var inferredEnterpriseName = InferCustomerEnterpriseName(customer);
        if (string.IsNullOrWhiteSpace(inferredEnterpriseName))
        {
            return string.Equals(selectedEnterpriseName, "Water Utility", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(inferredEnterpriseName, selectedEnterpriseName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? InferCustomerEnterpriseName(UtilityCustomer customer)
    {
        return MatchEnterpriseName(customer.Notes)
            ?? MatchEnterpriseName(customer.DisplayName)
            ?? MatchEnterpriseName(customer.CompanyName)
            ?? MatchEnterpriseName(customer.AccountNumber)
            ?? MatchEnterpriseName(customer.ServiceAddress)
            ?? MatchEnterpriseName(customer.ServiceCity);
    }

    private static string? MatchEnterpriseName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var seed in WorkspaceEnterpriseSeedCatalog.All)
        {
            foreach (var token in GetEnterpriseMatchTokens(seed))
            {
                if (value.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return seed.Name;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetEnterpriseMatchTokens(WorkspaceEnterpriseSeed seed)
    {
        yield return seed.Name;
        yield return seed.DepartmentName;

        if (string.Equals(seed.Name, "Water Utility", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Water";
            yield break;
        }

        if (string.Equals(seed.Name, "Wiley Sanitation District", StringComparison.OrdinalIgnoreCase))
        {
            yield return "WSD";
            yield return "Sanitation Utility";
            yield return "Sanitation";
            yield return "Sewer";
            yield break;
        }

        if (string.Equals(seed.Name, "Trash", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Trash Utility";
            yield return "Refuse";
            yield return "Garbage";
            yield break;
        }

        if (string.Equals(seed.Name, "Apartments", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Apartment";
            yield return "Apts";
        }
    }

    private sealed record RateHistoryPoint(int FiscalYear, decimal Rate);

    private sealed record RateSnapshotEnvelope(DateOnly? SnapshotDate, string? Payload);

    private sealed record WorkspaceSnapshotBuildContext(
        List<Enterprise> Enterprises,
        Enterprise SelectedEnterprise,
        List<int> BudgetYears,
        int SelectedFiscalYear,
        List<CustomerRow> CustomerRows,
        List<string> ServiceOptions,
        decimal CurrentRate,
        decimal TotalCosts,
        decimal ProjectedVolume,
        decimal RecommendedRate,
        IReadOnlyList<RateHistoryPoint> RateHistory,
        List<ProjectionRow> ProjectionRows,
        List<WorkspaceScenarioItemData> ScenarioItems,
        WorkspaceReserveTrajectoryData ReserveTrajectory);

    private sealed record WorkspaceSnapshotSelectionData(
        List<Enterprise> Enterprises,
        Enterprise SelectedEnterprise,
        List<int> BudgetYears,
        int SelectedFiscalYear,
        WorkspaceBootstrapData? PersistedSnapshot);

    private sealed record WorkspaceSnapshotDerivedData(
        List<CustomerRow> CustomerRows,
        List<string> ServiceOptions,
        decimal CurrentRate,
        decimal TotalCosts,
        decimal ProjectedVolume,
        decimal RecommendedRate,
        IReadOnlyList<RateHistoryPoint> RateHistory,
        List<ProjectionRow> ProjectionRows,
        List<WorkspaceScenarioItemData> ScenarioItems);

    private sealed record WorkspaceSnapshotCustomerData(
        List<CustomerRow> CustomerRows,
        List<string> ServiceOptions);

    private sealed record WorkspaceSnapshotRateData(
        decimal CurrentRate,
        decimal TotalCosts,
        decimal ProjectedVolume,
        decimal RecommendedRate,
        IReadOnlyList<RateHistoryPoint> RateHistory,
        List<ProjectionRow> ProjectionRows,
        List<WorkspaceScenarioItemData> ScenarioItems);

    private sealed record WorkspaceSnapshotRateMetrics(
        decimal CurrentRate,
        decimal TotalCosts,
        decimal ProjectedVolume,
        decimal RecommendedRate,
        IReadOnlyList<RateHistoryPoint> RateHistory);

    private sealed record WorkspaceSnapshotRateOutputs(
        List<ProjectionRow> ProjectionRows,
        List<WorkspaceScenarioItemData> ScenarioItems);

    private static Task<List<WorkspaceScenarioItemData>> BuildScenarioItemsAsync(Enterprise selectedEnterprise)
    {
        var reserveTarget = Math.Round(Math.Max(0m, selectedEnterprise.MonthlyExpenses * 0.05m), 2, MidpointRounding.AwayFromZero);
        var scenarioItems = reserveTarget > 0m
            ? new List<WorkspaceScenarioItemData>
            {
                new(Guid.NewGuid(), $"{selectedEnterprise.Name} reserve target", reserveTarget)
            }
            : [];

        return Task.FromResult(scenarioItems);
    }
}
