namespace WileyCoWeb.State;

public sealed class RateCalculator
{
    public static decimal CalculateRecommendedRate(decimal totalCosts, decimal projectedVolume)
    {
        return projectedVolume == 0 ? 0 : totalCosts / projectedVolume;
    }

    public static decimal CalculateRateDelta(decimal currentRate, decimal recommendedRate)
    {
        return currentRate - recommendedRate;
    }

    public static decimal CalculateAdjustedTotalCosts(decimal totalCosts, decimal scenarioCostTotal)
    {
        return totalCosts + scenarioCostTotal;
    }

    public static decimal CalculateAdjustedRecommendedRate(decimal adjustedTotalCosts, decimal projectedVolume)
    {
        return projectedVolume == 0 ? 0 : adjustedTotalCosts / projectedVolume;
    }

    public static decimal CalculateAdjustedRateDelta(decimal currentRate, decimal adjustedRecommendedRate)
    {
        return currentRate - adjustedRecommendedRate;
    }

    public static IReadOnlyList<RateComparisonPoint> CreateRateComparison(decimal currentRate, decimal adjustedRecommendedRate)
    {
        return
        [
            new RateComparisonPoint("Current", (double)currentRate),
            new RateComparisonPoint("Break-Even", (double)adjustedRecommendedRate)
        ];
    }
}