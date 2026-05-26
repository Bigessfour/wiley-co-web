using WileyWidget.Abstractions;

namespace WileyWidget.Tests;

public sealed class EnterpriseRateServiceTests
{
    [Theory]
    [Trait("Category", "HighRisk")]
    [InlineData(24000, 400, 60.00)]
    [InlineData(412500, 14500, 28.45)]
    public void CalculateBreakEvenRate_ReturnsExpected(
        decimal totalCost,
        decimal volume,
        decimal expectedRate)
    {
        var result = EnterpriseRateService.CalculateBreakEvenRate(totalCost, volume, roundToCurrency: true);
        Assert.Equal(expectedRate, result);
    }

    [Fact]
    [Trait("Category", "HighRisk")]
    public void CalculateBreakEvenRate_ReturnsZeroWhenVolumeIsZero()
    {
        Assert.Equal(0m, EnterpriseRateService.CalculateBreakEvenRate(100m, 0m, roundToCurrency: true));
    }

    [Theory]
    [Trait("Category", "HighRisk")]
    [InlineData(28.5, 60.0, -31.5)]
    [InlineData(60.0, 28.5, 31.5)]
    public void CalculateRateDelta_ReturnsCurrentMinusBreakEven(
        decimal currentRate,
        decimal breakEvenRate,
        decimal expectedDelta)
    {
        var delta = EnterpriseRateService.CalculateRateDelta(currentRate, breakEvenRate);
        Assert.Equal(expectedDelta, delta);
        Assert.Equal(breakEvenRate - currentRate, -delta);
    }

    [Fact]
    [Trait("Category", "HighRisk")]
    public void CalculateAdjustedBreakEvenRate_IncludesScenarioCosts()
    {
        const decimal totalCosts = 412_500m;
        const decimal scenarioTotal = 252_000m;
        const decimal projectedVolume = 14_500m;

        var rate = EnterpriseRateService.CalculateAdjustedBreakEvenRate(
            totalCosts,
            scenarioTotal,
            projectedVolume,
            roundToCurrency: false);

        Assert.Equal((totalCosts + scenarioTotal) / projectedVolume, rate);
    }

    [Theory]
    [Trait("Category", "HighRisk")]
    [InlineData(60, 60, 100)]
    [InlineData(51, 60, 85)]
    [InlineData(45, 60, 75)]
    public void CalculateRateAdequacyPercent_ReturnsExpected(
        decimal currentRate,
        decimal breakEvenRate,
        decimal expectedAdequacy)
    {
        var adequacy = EnterpriseRateService.CalculateRateAdequacyPercent(currentRate, breakEvenRate);
        Assert.Equal(expectedAdequacy, adequacy);
    }

    [Fact]
    [Trait("Category", "HighRisk")]
    public void CalculateMonthlyBalance_UsesRoundedRevenueMinusCosts()
    {
        var revenue = EnterpriseRateService.CalculateMonthlyRevenue(60m, 400m);
        var balance = EnterpriseRateService.CalculateMonthlyBalance(revenue, 24000m);

        Assert.Equal(24000m, revenue);
        Assert.Equal(0m, balance);
    }

    // p1-rate-consolidation cross-path tests
    [Theory]
    [Trait("Category", "HighRisk")]
    [InlineData(10000, 500, 500, 21.0)] // (10000+500)/500 = 21
    [InlineData(0, 100, 100, 1.0)]
    public void CalculateAdjustedTotalCosts_And_BreakEven_ProduceExpected(
        decimal baseCosts, decimal scenario, decimal volume, decimal expectedRate)
    {
        var adjusted = EnterpriseRateService.CalculateAdjustedTotalCosts(baseCosts, scenario);
        var rate = EnterpriseRateService.CalculateAdjustedBreakEvenRate(baseCosts, scenario, volume, roundToCurrency: false);
        Assert.Equal(baseCosts + scenario, adjusted);
        Assert.Equal(expectedRate, rate);
    }

    [Theory]
    [Trait("Category", "HighRisk")]
    [InlineData(27.5, 27.5, 0.0)] // parity: no delta
    [InlineData(30.0, 27.5, 2.5)]
    [InlineData(27.5, 30.0, -2.5)]
    public void CalculateAdjustedRateDelta_HandlesParityAndSigns(decimal current, decimal adjustedBe, decimal expected)
    {
        var delta = EnterpriseRateService.CalculateAdjustedRateDelta(current, adjustedBe);
        Assert.Equal(expected, delta);
    }

    [Fact]
    [Trait("Category", "HighRisk")]
    public void CalculateBreakEvenRate_ZeroVolume_And_AdjustedDelta_ZeroVolume()
    {
        Assert.Equal(0m, EnterpriseRateService.CalculateBreakEvenRate(100m, 0m));
        var adj = EnterpriseRateService.CalculateAdjustedBreakEvenRate(100m, 10m, 0m);
        Assert.Equal(0m, adj);
        Assert.Equal(5m, EnterpriseRateService.CalculateRateDelta(5m, 0m)); // current - be
    }
}
