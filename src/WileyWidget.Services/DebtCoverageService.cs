using System.Globalization;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

public sealed class DebtCoverageService : IDebtCoverageService
{
    private static readonly string[] DebtKeywords = ["debt", "bond", "interest", "principal", "loan", "covenant"];

    private readonly IEnterpriseRepository enterpriseRepository;
    private readonly IAccountsRepository accountsRepository;
    private readonly IBudgetRepository budgetRepository;
    private readonly ILogger<DebtCoverageService> logger;

    public DebtCoverageService(
        IEnterpriseRepository enterpriseRepository,
        IAccountsRepository accountsRepository,
        IBudgetRepository budgetRepository,
        ILogger<DebtCoverageService> logger)
    {
        this.enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        this.accountsRepository = accountsRepository ?? throw new ArgumentNullException(nameof(accountsRepository));
        this.budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DebtCoverageResult> BuildAsync(string enterpriseName, int fiscalYear, CancellationToken cancellationToken = default)
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

        List<Enterprise> enterprises;
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
            logger.LogError(ex, "Unable to load enterprise data while building debt coverage for {Enterprise} FY {FiscalYear}", normalizedEnterprise, fiscalYear);
            throw new DebtCoverageUnavailableException("Live debt coverage is unavailable because enterprise data could not be loaded.", ex);
        }

        var enterprise = enterprises.FirstOrDefault(item =>
            string.Equals(item.Name, normalizedEnterprise, StringComparison.OrdinalIgnoreCase));

        if (enterprise is null)
        {
            throw new DebtCoverageNotFoundException($"Enterprise '{normalizedEnterprise}' was not found in the live data store.");
        }

        var startDate = new DateTime(fiscalYear, 1, 1);
        var endDate = new DateTime(fiscalYear, 12, 31);

        var revenueTask = accountsRepository.GetMonthlyRevenueAsync(startDate, endDate, cancellationToken);
        var budgetTask = budgetRepository.GetByFiscalYearAsync(fiscalYear, cancellationToken);

        await Task.WhenAll(revenueTask, budgetTask).ConfigureAwait(false);

        var annualRevenue = decimal.Round(revenueTask.Result.Sum(item => item.Amount), 2, MidpointRounding.AwayFromZero);
        var budgetEntries = budgetTask.Result.ToList();
        var annualDebtService = decimal.Round(budgetEntries.Where(IsDebtServiceEntry).Sum(item => Math.Max(item.BudgetedAmount, item.ActualAmount)), 2, MidpointRounding.AwayFromZero);
        var reserveHeadroom = decimal.Round(annualRevenue - annualDebtService, 2, MidpointRounding.AwayFromZero);
        var debtServiceCoverageRatio = annualDebtService > 0m
            ? decimal.Round(annualRevenue / annualDebtService, 2, MidpointRounding.AwayFromZero)
            : 0m;
        const decimal covenantThreshold = 1.25m;
        var covenantHeadroom = decimal.Round(debtServiceCoverageRatio - covenantThreshold, 2, MidpointRounding.AwayFromZero);
        var covenantStatus = BuildCovenantStatus(debtServiceCoverageRatio, covenantThreshold);

        logger.LogInformation(
            "Built debt coverage for {Enterprise} FY {FiscalYear}: revenue={Revenue} debtService={DebtService} dscr={Dscr}",
            normalizedEnterprise,
            fiscalYear,
            annualRevenue.ToString(CultureInfo.InvariantCulture),
            annualDebtService.ToString(CultureInfo.InvariantCulture),
            debtServiceCoverageRatio.ToString(CultureInfo.InvariantCulture));

        var executiveSummary = $"{normalizedEnterprise} FY {fiscalYear} posts a {debtServiceCoverageRatio:0.00}x DSCR against a {covenantThreshold:0.00}x covenant floor.";

        return new DebtCoverageResult(
            enterprise.Name,
            fiscalYear,
            annualRevenue,
            annualDebtService,
            reserveHeadroom,
            debtServiceCoverageRatio,
            covenantThreshold,
            covenantHeadroom,
            covenantStatus,
            executiveSummary,
            DateTime.UtcNow,
            BuildWaterfallPoints(annualRevenue, annualDebtService, reserveHeadroom));
    }

    private static IReadOnlyList<DebtCoverageWaterfallPoint> BuildWaterfallPoints(decimal annualRevenue, decimal annualDebtService, decimal reserveHeadroom)
        =>
        [
            new DebtCoverageWaterfallPoint("Annual Revenue", (double)annualRevenue),
            new DebtCoverageWaterfallPoint("Debt Service", -(double)annualDebtService),
            new DebtCoverageWaterfallPoint("Reserve Headroom", (double)reserveHeadroom)
        ];

    private static bool IsDebtServiceEntry(BudgetEntry entry)
    {
        if (entry.MunicipalAccount?.Type == AccountType.Debt)
        {
            return true;
        }

        var combinedText = string.Join(' ',
            entry.Description,
            entry.AccountName,
            entry.AccountNumber,
            entry.MunicipalAccount?.TypeDescription ?? string.Empty,
            entry.MunicipalAccount?.FundDescription ?? string.Empty,
            entry.EntityName);

        return DebtKeywords.Any(keyword => combinedText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildCovenantStatus(decimal ratio, decimal threshold)
        => ratio >= threshold
            ? "Compliant"
            : ratio >= threshold * 0.9m
                ? "Watchlist"
                : "At Risk";
}