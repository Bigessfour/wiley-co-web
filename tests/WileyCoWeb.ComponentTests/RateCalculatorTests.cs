using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class RateCalculatorTests
{
    [Fact]
    public void CalculateRecommendedRate_MatchesHandCalculationForBundledDemo()
    {
        const decimal totalCosts = 412_500m;
        const decimal projectedVolume = 14_500m;

        var actual = RateCalculator.CalculateRecommendedRate(totalCosts, projectedVolume);

        Assert.Equal(totalCosts / projectedVolume, actual);
    }

    [Fact]
    public void CalculateRecommendedRate_ReturnsZeroWhenVolumeIsZero()
    {
        Assert.Equal(0m, RateCalculator.CalculateRecommendedRate(100m, 0m));
    }

    [Fact]
    public void CalculateAdjustedRecommendedRate_IncludesScenarioCosts()
    {
        const decimal totalCosts = 412_500m;
        const decimal scenarioTotal = 180_000m + 42_000m + 30_000m;
        const decimal projectedVolume = 14_500m;
        var adjustedCosts = RateCalculator.CalculateAdjustedTotalCosts(totalCosts, scenarioTotal);

        var rate = RateCalculator.CalculateAdjustedRecommendedRate(adjustedCosts, projectedVolume);

        Assert.Equal(664_500m / 14_500m, rate);
    }

    [Fact]
    public void CreateRateComparison_UsesCurrentAndBreakEvenLabels()
    {
        var points = RateCalculator.CreateRateComparison(28.5m, 30m);

        Assert.Equal(2, points.Count);
        Assert.Equal("Current", points[0].Label);
        Assert.Equal("Break-Even", points[1].Label);
        Assert.Equal(28.5, points[0].Value, 9);
        Assert.Equal(30.0, points[1].Value, 9);
    }
}
