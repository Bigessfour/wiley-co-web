using System.Globalization;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

public sealed class WorkspaceKnowledgeService : IWorkspaceKnowledgeService
{
    private readonly IEnterpriseRepository enterpriseRepository;
    private readonly IAnalyticsService analyticsService;
    private readonly IAnalyticsRepository analyticsRepository;
    private readonly IBudgetAnalyticsRepository budgetAnalyticsRepository;
    private readonly ILogger<WorkspaceKnowledgeService> logger;

    public WorkspaceKnowledgeService(
        IEnterpriseRepository enterpriseRepository,
        IAnalyticsService analyticsService,
        IAnalyticsRepository analyticsRepository,
        IBudgetAnalyticsRepository budgetAnalyticsRepository,
        ILogger<WorkspaceKnowledgeService> logger)
    {
        this.enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        this.analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        this.analyticsRepository = analyticsRepository ?? throw new ArgumentNullException(nameof(analyticsRepository));
        this.budgetAnalyticsRepository = budgetAnalyticsRepository ?? throw new ArgumentNullException(nameof(budgetAnalyticsRepository));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WorkspaceKnowledgeResult> BuildAsync(string enterpriseName, int fiscalYear, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(enterpriseName))
        {
            throw new ArgumentException("An enterprise name is required.", nameof(enterpriseName));
        }

        if (fiscalYear <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fiscalYear), "A valid fiscal year is required.");
        }

        List<WileyWidget.Models.Enterprise> enterprises;
        try
        {
            enterprises = (await enterpriseRepository.GetAllAsync(cancellationToken).ConfigureAwait(false)).ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to load enterprise data while building workspace knowledge for {Enterprise} FY {FiscalYear}", enterpriseName, fiscalYear);
            throw new WorkspaceKnowledgeUnavailableException("Live workspace knowledge is unavailable because enterprise data could not be loaded.", ex);
        }

        var enterprise = enterprises.FirstOrDefault(item =>
            string.Equals(item.Name, enterpriseName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (enterprise is null)
        {
            throw new WorkspaceKnowledgeNotFoundException($"Enterprise '{enterpriseName}' was not found in the live data store.");
        }

        return await BuildAsync(new WorkspaceKnowledgeInput(
            enterprise.Name,
            fiscalYear,
            enterprise.CurrentRate,
            enterprise.MonthlyExpenses,
            enterprise.CitizenCount,
            0m), cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkspaceKnowledgeResult> BuildAsync(WorkspaceKnowledgeInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.SelectedEnterprise))
        {
            throw new ArgumentException("An enterprise name is required.", nameof(input));
        }

        if (input.SelectedFiscalYear <= 0)
        {
            throw new ArgumentException("A valid fiscal year is required.", nameof(input));
        }

        if (input.ProjectedVolume <= 0)
        {
            throw new ArgumentException("Projected volume must be greater than zero.", nameof(input));
        }

        var normalizedEnterprise = input.SelectedEnterprise.Trim();
        var scenarioCostTotal = Math.Max(0m, input.ScenarioCostTotal);
        var breakEvenRate = decimal.Round(input.TotalCosts / input.ProjectedVolume, 2, MidpointRounding.AwayFromZero);
        var adjustedBreakEvenRate = decimal.Round((input.TotalCosts + scenarioCostTotal) / input.ProjectedVolume, 2, MidpointRounding.AwayFromZero);
        var monthlyRevenue = decimal.Round(input.CurrentRate * input.ProjectedVolume, 2, MidpointRounding.AwayFromZero);
        var netPosition = decimal.Round(monthlyRevenue - (input.TotalCosts + scenarioCostTotal), 2, MidpointRounding.AwayFromZero);
        var rateGap = decimal.Round(breakEvenRate - input.CurrentRate, 2, MidpointRounding.AwayFromZero);
        var adjustedRateGap = decimal.Round(adjustedBreakEvenRate - input.CurrentRate, 2, MidpointRounding.AwayFromZero);
        var coverageRatio = input.TotalCosts + scenarioCostTotal > 0
            ? decimal.Round(monthlyRevenue / (input.TotalCosts + scenarioCostTotal), 2, MidpointRounding.AwayFromZero)
            : 0m;

        logger.LogInformation(
            "Building workspace knowledge for {Enterprise} FY {FiscalYear} (scenarioCostTotal={ScenarioCostTotal})",
            normalizedEnterprise,
            input.SelectedFiscalYear,
            scenarioCostTotal);

        decimal reserveBalance;
        ReserveForecastResult reserveForecast;
        BudgetOverviewData budgetOverview;
        WorkspaceKnowledgeVariance[] topVariances;

        try
        {
            var reserveBalanceTask = LoadAnalyticsDependencyAsync(
                "current reserve balance",
                () => analyticsRepository.GetCurrentReserveBalanceAsync(normalizedEnterprise, cancellationToken),
                normalizedEnterprise,
                input.SelectedFiscalYear);
            var reserveForecastTask = LoadAnalyticsDependencyAsync(
                "reserve forecast",
                () => analyticsService.GenerateReserveForecastAsync(Math.Max(1, input.ForecastYears), normalizedEnterprise, cancellationToken),
                normalizedEnterprise,
                input.SelectedFiscalYear);
            var topVariancesTask = LoadAnalyticsDependencyAsync(
                "top budget variances",
                () => budgetAnalyticsRepository.GetTopVariancesAsync(Math.Max(1, input.TopVarianceCount), input.SelectedFiscalYear, cancellationToken),
                normalizedEnterprise,
                input.SelectedFiscalYear);
            var budgetOverviewTask = LoadAnalyticsDependencyAsync(
                "budget overview",
                () => analyticsService.GetBudgetOverviewAsync(input.SelectedFiscalYear, cancellationToken),
                normalizedEnterprise,
                input.SelectedFiscalYear);

            await Task.WhenAll(reserveBalanceTask, reserveForecastTask, topVariancesTask, budgetOverviewTask).ConfigureAwait(false);

            reserveBalance = await reserveBalanceTask.ConfigureAwait(false);
            reserveForecast = await reserveForecastTask.ConfigureAwait(false);
            budgetOverview = await budgetOverviewTask.ConfigureAwait(false);
            topVariances = (await topVariancesTask.ConfigureAwait(false))
                .Select(item => new WorkspaceKnowledgeVariance(
                    item.AccountName,
                    item.BudgetedAmount,
                    item.ActualAmount,
                    item.VarianceAmount,
                    item.VariancePercentage))
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        var operationalStatus = BuildOperationalStatus(adjustedRateGap, coverageRatio, reserveForecast.RiskAssessment);
        var executiveSummary = BuildExecutiveSummary(normalizedEnterprise, input.SelectedFiscalYear, adjustedRateGap, netPosition, coverageRatio, scenarioCostTotal, reserveForecast.RiskAssessment);
        var rateRationale = BuildRateRationale(normalizedEnterprise, input.SelectedFiscalYear, input.CurrentRate, adjustedBreakEvenRate, scenarioCostTotal, topVariances);

        var insights = BuildInsights(
            adjustedBreakEvenRate,
            adjustedRateGap,
            netPosition,
            coverageRatio,
            reserveBalance,
            reserveForecast.RecommendedReserveLevel,
            reserveForecast.RiskAssessment,
            budgetOverview,
            topVariances);

        var actions = BuildActions(
            normalizedEnterprise,
            adjustedRateGap,
            coverageRatio,
            reserveForecast.RiskAssessment,
            scenarioCostTotal,
            topVariances);

        return new WorkspaceKnowledgeResult(
            normalizedEnterprise,
            input.SelectedFiscalYear,
            operationalStatus,
            executiveSummary,
            rateRationale,
            input.CurrentRate,
            input.TotalCosts,
            input.ProjectedVolume,
            scenarioCostTotal,
            breakEvenRate,
            adjustedBreakEvenRate,
            rateGap,
            adjustedRateGap,
            monthlyRevenue,
            netPosition,
            coverageRatio,
            reserveBalance,
            reserveForecast.RecommendedReserveLevel,
            reserveForecast.RiskAssessment,
            DateTime.UtcNow,
            insights,
            actions,
            topVariances);
    }

    private async Task<T> LoadAnalyticsDependencyAsync<T>(
        string dependencyName,
        Func<Task<T>> operation,
        string enterprise,
        int fiscalYear)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (WorkspaceKnowledgeUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unable to load workspace knowledge dependency {DependencyName} for {Enterprise} FY {FiscalYear}",
                dependencyName,
                enterprise,
                fiscalYear);
            var detail = ex is InvalidOperationException && !string.IsNullOrWhiteSpace(ex.Message)
                ? $"Live workspace knowledge is unavailable because {dependencyName} could not be loaded. {ex.Message}"
                : $"Live workspace knowledge is unavailable because {dependencyName} could not be loaded.";
            throw new WorkspaceKnowledgeUnavailableException(
                detail,
                ex);
        }
    }

    private static string BuildOperationalStatus(decimal adjustedRateGap, decimal coverageRatio, string reserveRiskAssessment)
    {
        if (adjustedRateGap > 0 || coverageRatio < 1m)
        {
            return "Action needed";
        }

        if (reserveRiskAssessment.Contains("High", StringComparison.OrdinalIgnoreCase))
        {
            return "Reserve risk";
        }

        if (adjustedRateGap < 0)
        {
            return "Coverage above target";
        }

        return "On target";
    }

    private static string BuildExecutiveSummary(string enterprise, int fiscalYear, decimal adjustedRateGap, decimal netPosition, decimal coverageRatio, decimal scenarioCostTotal, string reserveRiskAssessment)
    {
        if (adjustedRateGap > 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{enterprise} FY {fiscalYear} is below the server-calculated adjusted break-even target by {adjustedRateGap:C2}. Net position is {netPosition:C0} and coverage is {coverageRatio:F2}x. Reserve outlook is {reserveRiskAssessment}. Scenario pressure currently adds {scenarioCostTotal:C0} to the required revenue base.");
        }

        if (adjustedRateGap < 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{enterprise} FY {fiscalYear} is above the server-calculated adjusted break-even target by {Math.Abs(adjustedRateGap):C2}. Net position is {netPosition:C0}, coverage is {coverageRatio:F2}x, and reserve outlook is {reserveRiskAssessment}. Use the surplus deliberately for reserves or capital timing rather than letting it stay implicit.");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{enterprise} FY {fiscalYear} is sitting on the adjusted break-even line. Coverage is {coverageRatio:F2}x, net position is {netPosition:C0}, and reserve outlook is {reserveRiskAssessment}. Any new scenario cost or actuals variance will move the recommendation immediately.");
    }

    private static string BuildRateRationale(string enterprise, int fiscalYear, decimal currentRate, decimal adjustedBreakEvenRate, decimal scenarioCostTotal, IReadOnlyList<WorkspaceKnowledgeVariance> topVariances)
    {
        var largestVariance = topVariances.FirstOrDefault();
        var varianceClause = largestVariance is null
            ? "No material fiscal-year variance records were returned for the current scope."
            : string.Create(CultureInfo.InvariantCulture, $"The largest live variance in FY {fiscalYear} is {largestVariance.AccountName} at {largestVariance.VarianceAmount:C0} ({largestVariance.VariancePercentage:F1}%).");

        return string.Create(CultureInfo.InvariantCulture, $"Rate rationale for {enterprise} FY {fiscalYear}: current rate {currentRate:C2}, adjusted break-even {adjustedBreakEvenRate:C2}, scenario pressure {scenarioCostTotal:C0}. {varianceClause} The recommendation is built from persisted enterprise costs and volume, current workspace adjustments, reserve analytics, and fiscal-year budget variance data rather than client placeholders.");
    }

    private static IReadOnlyList<WorkspaceKnowledgeInsight> BuildInsights(
        decimal adjustedBreakEvenRate,
        decimal adjustedRateGap,
        decimal netPosition,
        decimal coverageRatio,
        decimal reserveBalance,
        decimal recommendedReserveLevel,
        string reserveRiskAssessment,
        BudgetOverviewData budgetOverview,
        IReadOnlyList<WorkspaceKnowledgeVariance> topVariances)
    {
        var insights = new List<WorkspaceKnowledgeInsight>
        {
            new(
                "Adjusted break-even",
                adjustedBreakEvenRate.ToString("C2", CultureInfo.InvariantCulture),
                "Server-calculated rate required to cover current costs plus active scenario pressure."),
            new(
                "Adjusted rate gap",
                adjustedRateGap.ToString("C2", CultureInfo.InvariantCulture),
                adjustedRateGap > 0m
                    ? "Positive values mean the current rate is still below the required level."
                    : "Negative values mean the current rate is covering the modeled cost base."),
            new(
                "Net position",
                netPosition.ToString("C0", CultureInfo.InvariantCulture),
                "Monthly revenue minus modeled costs after scenario adjustments."),
            new(
                "Coverage ratio",
                string.Create(CultureInfo.InvariantCulture, $"{coverageRatio:F2}x"),
                "Revenue divided by modeled costs. Ratios below 1.00x need immediate attention."),
            new(
                "Reserve posture",
                string.Create(CultureInfo.InvariantCulture, $"{reserveBalance:C0} vs {recommendedReserveLevel:C0}"),
                $"Current reserve estimate compared to recommended reserve target. Risk: {reserveRiskAssessment}."),
            new(
                "Budget variance",
                budgetOverview.TotalVariance.ToString("C0", CultureInfo.InvariantCulture),
                string.Create(CultureInfo.InvariantCulture, $"FY totals: budget {budgetOverview.TotalBudget:C0}, actual {budgetOverview.TotalActual:C0}."))
        };

        if (topVariances.Count > 0)
        {
            var topVariance = topVariances[0];
            insights.Add(new WorkspaceKnowledgeInsight(
                "Largest variance",
                topVariance.VarianceAmount.ToString("C0", CultureInfo.InvariantCulture),
                string.Create(CultureInfo.InvariantCulture, $"{topVariance.AccountName} is the largest live fiscal-year variance at {topVariance.VariancePercentage:F1}% of budget.")));
        }

        return insights;
    }

    private static IReadOnlyList<WorkspaceKnowledgeAction> BuildActions(
        string enterprise,
        decimal adjustedRateGap,
        decimal coverageRatio,
        string reserveRiskAssessment,
        decimal scenarioCostTotal,
        IReadOnlyList<WorkspaceKnowledgeVariance> topVariances)
    {
        var actions = new List<WorkspaceKnowledgeAction>();

        if (adjustedRateGap > 0m)
        {
            actions.Add(new WorkspaceKnowledgeAction(
                "Close the modeled rate gap",
                string.Create(CultureInfo.InvariantCulture, $"{enterprise} is {adjustedRateGap:C2} below the adjusted break-even target. Raise the working rate, reduce modeled costs, or both before issuing a production recommendation."),
                "high"));
        }
        else
        {
            actions.Add(new WorkspaceKnowledgeAction(
                "Document surplus usage",
                "The current rate is covering the modeled cost base. Explicitly assign the excess to reserves, capital timing, or subsidy reduction so the recommendation is auditable.",
                "medium"));
        }

        if (coverageRatio < 1m)
        {
            actions.Add(new WorkspaceKnowledgeAction(
                "Restore coverage above 1.00x",
                "Modeled revenue is not fully covering costs. Freeze discretionary scenario additions and correct the rate before publishing.",
                "high"));
        }

        if (reserveRiskAssessment.Contains("High", StringComparison.OrdinalIgnoreCase) || reserveRiskAssessment.Contains("Medium", StringComparison.OrdinalIgnoreCase))
        {
            actions.Add(new WorkspaceKnowledgeAction(
                "Build reserves intentionally",
                "Reserve analytics are signaling pressure. Reserve contributions should be explicit in the rate path instead of assumed inside the ending balance.",
                "medium"));
        }

        if (scenarioCostTotal > 0m)
        {
            actions.Add(new WorkspaceKnowledgeAction(
                "Persist the active scenario",
                string.Create(CultureInfo.InvariantCulture, $"Active scenario pressure adds {scenarioCostTotal:C0}. Save it through the workspace API so the rate recommendation and Jarvis explanation remain reproducible."),
                "medium"));
        }

        if (topVariances.Count > 0)
        {
            var topVariance = topVariances[0];
            actions.Add(new WorkspaceKnowledgeAction(
                "Review the largest fiscal-year variance",
                string.Create(CultureInfo.InvariantCulture, $"Validate {topVariance.AccountName} before publishing. It is currently the largest live variance at {topVariance.VarianceAmount:C0}."),
                "low"));
        }

        return actions;
    }
}