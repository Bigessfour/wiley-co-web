using WileyWidget.Abstractions;

namespace WileyCoWeb.State;

public sealed class RateCalculator
{
    public static decimal CalculateRecommendedRate(decimal totalCosts, decimal projectedVolume)
        => EnterpriseRateService.CalculateBreakEvenRate(totalCosts, projectedVolume);

    public static decimal CalculateRateDelta(decimal currentRate, decimal recommendedRate)
        => EnterpriseRateService.CalculateRateDelta(currentRate, recommendedRate);

    public static decimal CalculateAdjustedTotalCosts(decimal totalCosts, decimal scenarioCostTotal)
        => EnterpriseRateService.CalculateAdjustedTotalCosts(totalCosts, scenarioCostTotal);

    public static decimal CalculateAdjustedRecommendedRate(decimal adjustedTotalCosts, decimal projectedVolume)
        => EnterpriseRateService.CalculateBreakEvenRate(adjustedTotalCosts, projectedVolume);

    public static decimal CalculateAdjustedRateDelta(decimal currentRate, decimal adjustedRecommendedRate)
        => EnterpriseRateService.CalculateAdjustedRateDelta(currentRate, adjustedRecommendedRate);

    public static IReadOnlyList<RateComparisonPoint> CreateRateComparison(decimal currentRate, decimal adjustedRecommendedRate)
    {
        return
        [
            new RateComparisonPoint("Current", (double)currentRate),
            new RateComparisonPoint("Break-Even", (double)adjustedRecommendedRate)
        ];
    }
}
