namespace WileyWidget.Abstractions;

public static class EnterpriseRateService
{
    public static decimal CalculateBreakEvenRate(
        decimal totalCosts,
        decimal projectedVolume,
        bool roundToCurrency = false)
    {
        var rate = projectedVolume == 0 ? 0m : totalCosts / projectedVolume;
        return roundToCurrency
            ? Math.Round(rate, 2, MidpointRounding.AwayFromZero)
            : rate;
    }

    public static decimal CalculateRateDelta(decimal currentRate, decimal breakEvenRate)
    {
        return currentRate - breakEvenRate;
    }

    public static decimal CalculateAdjustedTotalCosts(decimal totalCosts, decimal scenarioCostTotal)
    {
        return totalCosts + scenarioCostTotal;
    }

    public static decimal CalculateAdjustedBreakEvenRate(
        decimal totalCosts,
        decimal scenarioCostTotal,
        decimal projectedVolume,
        bool roundToCurrency = false)
    {
        var adjustedTotalCosts = CalculateAdjustedTotalCosts(totalCosts, scenarioCostTotal);
        return CalculateBreakEvenRate(adjustedTotalCosts, projectedVolume, roundToCurrency);
    }

    public static decimal CalculateAdjustedRateDelta(decimal currentRate, decimal adjustedBreakEvenRate)
    {
        return CalculateRateDelta(currentRate, adjustedBreakEvenRate);
    }

    public static decimal CalculateRateAdequacyPercent(
        decimal currentRate,
        decimal breakEvenRate,
        decimal capPercent = 150m)
    {
        if (breakEvenRate <= 0m)
        {
            return 0m;
        }

        var adequacy = currentRate / breakEvenRate * 100m;
        return Math.Min(adequacy, capPercent);
    }

    public static decimal CalculateMonthlyRevenue(decimal currentRate, decimal projectedVolume)
    {
        var effectiveVolume = Math.Max(1m, projectedVolume);
        return Math.Round(currentRate * effectiveVolume, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateMonthlyBalance(decimal monthlyRevenue, decimal totalCosts)
    {
        return Math.Round(monthlyRevenue - totalCosts, 2, MidpointRounding.AwayFromZero);
    }

    // Overhead application (post-categorization, per core mission).
    // Global % (Town + management + labor) stored in AppSettings (or per via input).
    // Enterprise Share base = direct costs (standard absorption). Net Contribution = Revenue - Direct - (oh% * Direct).
    // Deterministic, round to 2 decimals for currency impact. Testable (see EnterpriseRateServiceTests).
    public static decimal CalculateTotalOverheadPercent(decimal townPercent, decimal managementPercent, decimal laborPercent)
        => townPercent + managementPercent + laborPercent;

    public static decimal CalculateOverheadBurden(decimal directCosts, decimal totalOverheadPercent)
    {
        if (directCosts <= 0) return 0m;
        return Math.Round(directCosts * (totalOverheadPercent / 100m), 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateNetContribution(decimal revenue, decimal directCosts, decimal overheadBurden)
    {
        return Math.Round(revenue - directCosts - overheadBurden, 2, MidpointRounding.AwayFromZero);
    }

    public static bool HoldsItsOwn(decimal netContribution) => netContribution >= 0m;

    public static decimal CalculateVampireImpact(decimal netContribution)
        => netContribution >= 0m ? 0m : Math.Abs(netContribution);
}
