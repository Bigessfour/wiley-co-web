using System.Globalization;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Deterministic council-demo payloads used only when live repositories cannot satisfy capital gap / debt coverage requests.
/// </summary>
public static class WorkspaceSyntheticPanelData
{
    public static CapitalGapResult BuildCapitalGap(string selectedEnterprise, int fiscalYear)
    {
        const decimal annualRateRevenue = 412_500m;
        const decimal annualCapitalNeed = 252_000m;
        var rateRevenueGap = decimal.Round(annualRateRevenue - annualCapitalNeed, 2, MidpointRounding.AwayFromZero);
        var ratio = annualCapitalNeed > 0m
            ? decimal.Round(annualRateRevenue / annualCapitalNeed, 2, MidpointRounding.AwayFromZero)
            : 0m;
        var status = rateRevenueGap >= 0m ? "Covered" : ratio >= 0.9m ? "Watchlist" : "Gap";
        var running = annualRateRevenue;
        var points = new List<CapitalGapItemPoint>(3);
        foreach (var (label, budgeted, actual) in new (string Label, decimal Budgeted, decimal Actual)[]
                 {
                     ("Water treatment capital improvement", 120_000m, 45_000m),
                     ("Distribution main replacement", 92_000m, 12_000m),
                     ("Meter system upgrade", 40_000m, 8_000m)
                 })
        {
            running -= Math.Max(budgeted, actual);
            points.Add(new CapitalGapItemPoint(
                label,
                "Capital",
                decimal.Round(budgeted, 2, MidpointRounding.AwayFromZero),
                decimal.Round(Math.Max(0m, actual), 2, MidpointRounding.AwayFromZero),
                decimal.Round(running, 2, MidpointRounding.AwayFromZero),
                "Water",
                "Synthetic seed"));
        }

        var summary = string.Format(
            CultureInfo.InvariantCulture,
            "{0} FY {1} (sample data) shows {2:C0} in annual rate revenue against {3:C0} in capital needs, leaving {4:C0} of headroom.",
            selectedEnterprise,
            fiscalYear,
            annualRateRevenue,
            annualCapitalNeed,
            rateRevenueGap);

        return new CapitalGapResult(
            selectedEnterprise,
            fiscalYear,
            annualRateRevenue,
            annualCapitalNeed,
            rateRevenueGap,
            ratio,
            points.Count,
            status,
            summary,
            DateTime.UtcNow,
            points);
    }

    public static DebtCoverageResult BuildDebtCoverage(string selectedEnterprise, int fiscalYear)
    {
        const decimal annualRevenue = 380_000m;
        const decimal annualDebtService = 240_000m;
        var reserveHeadroom = decimal.Round(annualRevenue - annualDebtService, 2, MidpointRounding.AwayFromZero);
        const decimal covenantThreshold = 1.25m;
        var dscr = annualDebtService > 0m
            ? decimal.Round(annualRevenue / annualDebtService, 2, MidpointRounding.AwayFromZero)
            : 0m;
        var covenantHeadroom = decimal.Round(dscr - covenantThreshold, 2, MidpointRounding.AwayFromZero);
        var covenantStatus = dscr >= covenantThreshold
            ? "Compliant"
            : dscr >= covenantThreshold * 0.9m
                ? "Watchlist"
                : "At Risk";

        var summary = string.Format(
            CultureInfo.InvariantCulture,
            "{0} FY {1} (sample data) posts a {2:0.00}x DSCR against a {3:0.00}x covenant floor.",
            selectedEnterprise,
            fiscalYear,
            dscr,
            covenantThreshold);

        return new DebtCoverageResult(
            selectedEnterprise,
            fiscalYear,
            annualRevenue,
            annualDebtService,
            reserveHeadroom,
            dscr,
            covenantThreshold,
            covenantHeadroom,
            covenantStatus,
            summary,
            DateTime.UtcNow,
            new[]
            {
                new DebtCoverageWaterfallPoint("Annual Revenue", (double)annualRevenue),
                new DebtCoverageWaterfallPoint("Debt Service", -(double)annualDebtService),
                new DebtCoverageWaterfallPoint("Reserve Headroom", (double)reserveHeadroom)
            });
    }
}
