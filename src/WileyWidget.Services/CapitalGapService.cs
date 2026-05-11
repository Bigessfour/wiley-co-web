using System.Globalization;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

public sealed class CapitalGapService : ICapitalGapService
{
    private static readonly string[] CapitalKeywords = ["capital", "project", "improvement", "equipment", "development"];
    private static readonly string[] RevenueKeywords = ["revenue", "rate", "charge", "income", "interest", "grant", "transfer"];

    private readonly IBudgetRepository budgetRepository;
    private readonly ILogger<CapitalGapService> logger;

    public CapitalGapService(IBudgetRepository budgetRepository, ILogger<CapitalGapService> logger)
    {
        this.budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CapitalGapResult> BuildAsync(string enterpriseName, int fiscalYear, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(enterpriseName))
        {
            throw new ArgumentException("An enterprise name is required.", nameof(enterpriseName));
        }

        if (fiscalYear <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fiscalYear), "A valid fiscal year is required.");
        }

        var normalizedEnterprise = enterpriseName.Trim();
        var budgetEntries = (await budgetRepository.GetByFiscalYearAsync(fiscalYear, cancellationToken).ConfigureAwait(false)).ToList();
        if (budgetEntries.Count == 0)
        {
            throw new CapitalGapNotFoundException($"No budget entries were found for fiscal year {fiscalYear}.");
        }

        var enterpriseEntries = budgetEntries.Where(entry => MatchesEnterprise(entry, normalizedEnterprise)).ToList();
        if (enterpriseEntries.Count == 0)
        {
            enterpriseEntries = budgetEntries;
        }

        var capitalItems = enterpriseEntries
            .Where(IsCapitalItem)
            .OrderByDescending(GetCapitalNeedAmount)
            .ThenBy(entry => entry.AccountNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (capitalItems.Count == 0)
        {
            capitalItems = enterpriseEntries
                .Where(entry => entry.BudgetedAmount > 0m)
                .OrderByDescending(entry => entry.BudgetedAmount)
                .ThenBy(entry => entry.AccountNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Description, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();
        }

        if (capitalItems.Count == 0)
        {
            throw new CapitalGapNotFoundException($"No capital-tagged budget items were found for {normalizedEnterprise} FY {fiscalYear}.");
        }

        var rateRevenueEntries = enterpriseEntries.Where(IsRateRevenueItem).ToList();
        if (rateRevenueEntries.Count == 0)
        {
            rateRevenueEntries = budgetEntries.Where(IsRateRevenueItem).ToList();
        }

        var annualRateRevenue = decimal.Round(rateRevenueEntries.Sum(GetRevenueAmount), 2, MidpointRounding.AwayFromZero);
        var annualCapitalNeed = decimal.Round(capitalItems.Sum(GetCapitalNeedAmount), 2, MidpointRounding.AwayFromZero);
        var rateRevenueGap = decimal.Round(annualRateRevenue - annualCapitalNeed, 2, MidpointRounding.AwayFromZero);
        var capitalNeedCoverageRatio = annualCapitalNeed > 0m
            ? decimal.Round(annualRateRevenue / annualCapitalNeed, 2, MidpointRounding.AwayFromZero)
            : 0m;
        var capitalStatus = BuildCapitalStatus(rateRevenueGap, capitalNeedCoverageRatio);

        var runningGap = annualRateRevenue;
        var points = new List<CapitalGapItemPoint>(capitalItems.Count);
        foreach (var entry in capitalItems)
        {
            var capitalNeed = GetCapitalNeedAmount(entry);
            runningGap -= capitalNeed;

            points.Add(new CapitalGapItemPoint(
                GetItemLabel(entry),
                "Capital",
                decimal.Round(entry.BudgetedAmount, 2, MidpointRounding.AwayFromZero),
                decimal.Round(Math.Max(0m, entry.ActualAmount), 2, MidpointRounding.AwayFromZero),
                decimal.Round(runningGap, 2, MidpointRounding.AwayFromZero),
                entry.DepartmentName,
                entry.AccountName));
        }

        logger.LogInformation(
            "Built capital gap for {Enterprise} FY {FiscalYear}: revenue={Revenue} capitalNeed={CapitalNeed} gap={Gap}",
            normalizedEnterprise,
            fiscalYear,
            annualRateRevenue.ToString(CultureInfo.InvariantCulture),
            annualCapitalNeed.ToString(CultureInfo.InvariantCulture),
            rateRevenueGap.ToString(CultureInfo.InvariantCulture));

        var executiveSummary = $"{normalizedEnterprise} FY {fiscalYear} shows {annualRateRevenue:C0} in annual rate revenue against {annualCapitalNeed:C0} in capital needs, leaving {rateRevenueGap:C0} of headroom.";

        return new CapitalGapResult(
            normalizedEnterprise,
            fiscalYear,
            annualRateRevenue,
            annualCapitalNeed,
            rateRevenueGap,
            capitalNeedCoverageRatio,
            points.Count,
            capitalStatus,
            executiveSummary,
            DateTime.UtcNow,
            points);
    }

    private static decimal GetRevenueAmount(BudgetEntry entry)
        => decimal.Round(Math.Max(entry.BudgetedAmount, entry.ActualAmount), 2, MidpointRounding.AwayFromZero);

    private static decimal GetCapitalNeedAmount(BudgetEntry entry)
        => decimal.Round(Math.Max(entry.BudgetedAmount, entry.ActualAmount), 2, MidpointRounding.AwayFromZero);

    private static string GetItemLabel(BudgetEntry entry)
        => string.IsNullOrWhiteSpace(entry.AccountName)
            ? entry.Description
            : entry.AccountName;

    private static bool IsCapitalItem(BudgetEntry entry)
    {
        if (entry.MunicipalAccount?.Type == AccountType.CapitalOutlay)
        {
            return true;
        }

        if (entry.FundType == FundType.CapitalProjects)
        {
            return true;
        }

        return CombinedText(entry).ContainsAny(CapitalKeywords);
    }

    private static bool IsRateRevenueItem(BudgetEntry entry)
    {
        if (IsCapitalItem(entry))
        {
            return false;
        }

        return entry.MunicipalAccount?.Type is AccountType.Revenue or AccountType.Grants or AccountType.Interest or AccountType.Transfers
            || CombinedText(entry).ContainsAny(RevenueKeywords);
    }

    private static bool MatchesEnterprise(BudgetEntry entry, string enterpriseName)
    {
        var candidates = new[]
        {
            entry.EntityName,
            entry.Fund?.Name,
            entry.Fund?.FundCode,
            entry.MunicipalAccount?.Name,
            entry.MunicipalAccount?.FundDescription,
            entry.Description,
            entry.DepartmentName
        };

        return candidates.Any(candidate => !string.IsNullOrWhiteSpace(candidate) && candidate.Contains(enterpriseName, StringComparison.OrdinalIgnoreCase));
    }

    private static string CombinedText(BudgetEntry entry)
        => string.Join(' ',
            entry.Description,
            entry.AccountName,
            entry.AccountNumber,
            entry.EntityName,
            entry.DepartmentName,
            entry.MunicipalAccount?.Name ?? string.Empty,
            entry.MunicipalAccount?.FundDescription ?? string.Empty);

    private static string BuildCapitalStatus(decimal rateRevenueGap, decimal capitalNeedCoverageRatio)
        => rateRevenueGap >= 0m
            ? "Covered"
            : capitalNeedCoverageRatio >= 0.9m
                ? "Watchlist"
                : "Gap";
}

internal static class CapitalGapTextExtensions
{
    public static bool ContainsAny(this string text, IEnumerable<string> keywords)
        => keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}