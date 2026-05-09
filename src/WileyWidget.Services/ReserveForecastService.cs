using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

public sealed class ReserveForecastService(
    WileyWidget.Services.Abstractions.IScenarioSnapshotRepository scenarioSnapshotRepository,
    WileyWidget.Business.Interfaces.IBudgetRepository budgetRepository,
    ILogger<ReserveForecastService> logger)
    : IReserveForecastService
{
    public async Task<ReserveTrajectoryForecastResult> GetTrajectoryAsync(
        IReadOnlyCollection<string> enterpriseTypes,
        int fiscalYear,
        decimal currentRate,
        decimal totalCosts,
        decimal projectedVolume,
        decimal recommendedRate,
        int years = 5,
        CancellationToken cancellationToken = default)
    {
        var safeYears = Math.Max(1, years);
        var enterpriseCount = enterpriseTypes.Count == 0 ? 1 : enterpriseTypes.Count;

        var recentSnapshots = await scenarioSnapshotRepository.GetRecentAsync(Math.Max(3, safeYears * 2), cancellationToken).ConfigureAwait(false);
        var budgetHistory = await budgetRepository.GetHistoricalBudgetSummaryAsync(Math.Max(3, safeYears), fiscalYear, cancellationToken).ConfigureAwait(false);

        var annualGrowthRate = CalculateAnnualGrowthRate(budgetHistory, recentSnapshots);
        var scenarioVolatility = CalculateScenarioVolatility(recentSnapshots);
        var enterpriseFactor = 1m + (enterpriseCount - 1) * 0.02m;
        var baselineReserve = Math.Round(Math.Max(0m, totalCosts) * 6m, 2, MidpointRounding.AwayFromZero);
        var reserveFloor = Math.Round(Math.Max(baselineReserve, totalCosts * 9m), 2, MidpointRounding.AwayFromZero);
        var trend = Math.Round(Math.Max(0m, recommendedRate - currentRate) * Math.Max(1m, projectedVolume) * 0.04m * enterpriseFactor, 2, MidpointRounding.AwayFromZero);

        var points = new List<ReserveTrajectoryPoint>(safeYears * 12);
        for (var monthIndex = 1; monthIndex <= safeYears * 12; monthIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var growthMultiplier = 1m + (annualGrowthRate * monthIndex / 12m);
            var projectedReserve = Math.Round(baselineReserve * growthMultiplier + (trend * monthIndex), 2, MidpointRounding.AwayFromZero);
            var confidenceInterval = Math.Round(projectedReserve * scenarioVolatility, 2, MidpointRounding.AwayFromZero);
            var lowScenario = Math.Max(0m, projectedReserve - confidenceInterval);
            var highScenario = projectedReserve + confidenceInterval;

            points.Add(new ReserveTrajectoryPoint(
                DateTime.UtcNow.Date.AddMonths(monthIndex),
                projectedReserve,
                lowScenario,
                highScenario,
                reserveFloor));
        }

        var riskAssessment = DetermineRiskAssessment(points, reserveFloor);

        logger.LogInformation(
            "Built reserve trajectory forecast for {EnterpriseCount} enterprise types over {Years} years using {SnapshotCount} recent snapshots.",
            enterpriseCount,
            safeYears,
            recentSnapshots.Count);

        return new ReserveTrajectoryForecastResult(
            baselineReserve,
            reserveFloor,
            riskAssessment,
            points);
    }

    private static decimal CalculateAnnualGrowthRate(IReadOnlyList<WileyWidget.Models.HistoricalBudgetYear> budgetHistory, IReadOnlyList<WileyWidget.Models.SavedScenarioSnapshot> recentSnapshots)
    {
        var budgetGrowth = budgetHistory.Count == 0
            ? 0.03m
            : budgetHistory.Sum(item => item.YearOverYearPercent) / Math.Max(1, budgetHistory.Count) / 100m;

        var snapshotMomentum = recentSnapshots.Count == 0
            ? 0m
            : recentSnapshots.Sum(item => item.RateIncreasePercent - item.ExpenseIncreasePercent) / Math.Max(1, recentSnapshots.Count) / 100m;

        return Clamp((budgetGrowth * 0.7m) + (snapshotMomentum * 0.3m), 0.005m, 0.08m);
    }

    private static decimal CalculateScenarioVolatility(IReadOnlyList<WileyWidget.Models.SavedScenarioSnapshot> recentSnapshots)
    {
        if (recentSnapshots.Count == 0)
        {
            return 0.08m;
        }

        var averageVariance = recentSnapshots.Sum(item => Math.Abs(item.Variance)) / Math.Max(1, recentSnapshots.Count);
        var averageTarget = recentSnapshots.Sum(item => Math.Max(1m, item.RevenueTarget)) / Math.Max(1, recentSnapshots.Count);
        var volatility = averageTarget <= 0m ? 0.08m : averageVariance / averageTarget;

        return Clamp(Math.Max(0.05m, volatility), 0.05m, 0.18m);
    }

    private static string DetermineRiskAssessment(IReadOnlyList<ReserveTrajectoryPoint> points, decimal reserveFloor)
    {
        if (points.Count == 0)
        {
            return "Unavailable";
        }

        var finalProjection = points[^1].ProjectedReserve;
        if (finalProjection < reserveFloor * 0.5m)
        {
            return "High";
        }

        if (finalProjection < reserveFloor)
        {
            return "Moderate";
        }

        return "Low";
    }

    private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
    {
        if (value < minimum)
        {
            return minimum;
        }

        if (value > maximum)
        {
            return maximum;
        }

        return value;
    }
}