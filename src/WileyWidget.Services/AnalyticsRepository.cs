using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models.Amplify;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Repository implementation for analytics-specific data operations
    /// </summary>
    public class AnalyticsRepository : IAnalyticsRepository
    {
        private sealed record ReserveLedgerRow(DateOnly EntryDate, int SourceRowNumber, decimal Amount, decimal? RunningBalance, string? AccountName, string? OriginalFileName);

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<AnalyticsRepository> _logger;

        public AnalyticsRepository(
            IDbContextFactory<AppDbContext> contextFactory,
            ILogger<AnalyticsRepository> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets historical reserve data points for forecasting
        /// </summary>
        public async Task<IEnumerable<ReserveDataPoint>> GetHistoricalReserveDataAsync(DateTime startDate, DateTime endDate, string? entryScope = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var normalizedEntryScope = NormalizeEntryScope(entryScope);
            var startDateOnly = DateOnly.FromDateTime(startDate);
            var endDateOnly = DateOnly.FromDateTime(endDate);

            var reserveTransactions = await context.LedgerEntries
                .AsNoTracking()
                .Where(t => t.EntryDate.HasValue)
                .Where(t => t.EntryDate!.Value >= startDateOnly && t.EntryDate.Value <= endDateOnly)
                .Where(t => normalizedEntryScope == null || t.EntryScope == normalizedEntryScope)
                .Select(t => new ReserveLedgerRow(
                    t.EntryDate!.Value,
                    t.SourceRowNumber,
                    t.Amount ?? 0m,
                    t.RunningBalance,
                    t.AccountName,
                    t.SourceFile.OriginalFileName))
                .ToListAsync(cancellationToken);

            reserveTransactions = PreferGeneralLedgerRows(
                reserveTransactions.Where(t => IsReserveAccount(t.AccountName)).ToList(),
                row => row.OriginalFileName);

            if (!reserveTransactions.Any())
            {
                _logger.LogInformation("No reserve transactions found for period {Start} to {End} (EntryScope={EntryScope})", startDate, endDate, normalizedEntryScope);
                return Array.Empty<ReserveDataPoint>();
            }

            // Calculate running reserve balance
            var dataPoints = new List<ReserveDataPoint>();
            decimal runningBalance = 0;

            foreach (var transaction in reserveTransactions)
            {
                runningBalance += transaction.Amount;

                dataPoints.Add(new ReserveDataPoint
                {
                    Date = transaction.EntryDate.ToDateTime(TimeOnly.MinValue),
                    Reserves = runningBalance
                });
            }

            _logger.LogInformation("Retrieved {Count} reserve data points for forecasting (EntryScope={EntryScope})", dataPoints.Count, normalizedEntryScope);
            return dataPoints;
        }

        /// <summary>
        /// Gets current reserve balance
        /// </summary>
        public async Task<decimal> GetCurrentReserveBalanceAsync(string? entryScope = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var normalizedEntryScope = NormalizeEntryScope(entryScope);

            var reserveTransactions = await context.LedgerEntries
                .AsNoTracking()
                .Where(t => t.EntryDate.HasValue)
                .Where(t => normalizedEntryScope == null || t.EntryScope == normalizedEntryScope)
                .Select(t => new ReserveLedgerRow(
                    t.EntryDate!.Value,
                    t.SourceRowNumber,
                    t.Amount ?? 0m,
                    t.RunningBalance,
                    t.AccountName,
                    t.SourceFile.OriginalFileName))
                .ToListAsync(cancellationToken);

            reserveTransactions = PreferGeneralLedgerRows(
                reserveTransactions.Where(t => IsReserveAccount(t.AccountName)).ToList(),
                row => row.OriginalFileName);

            if (!reserveTransactions.Any())
            {
                _logger.LogInformation("No reserve transactions found for current balance calculation (EntryScope={EntryScope})", normalizedEntryScope);
                return 0;
            }

            var latestRunningBalances = reserveTransactions
                .Where(t => t.RunningBalance.HasValue && !string.IsNullOrWhiteSpace(t.AccountName))
                .GroupBy(t => t.AccountName!, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(t => t.EntryDate)
                    .ThenByDescending(t => t.SourceRowNumber)
                    .First())
                .ToList();

            var balance = latestRunningBalances.Count > 0
                ? latestRunningBalances.Sum(t => t.RunningBalance ?? 0m)
                : reserveTransactions.Sum(t => t.Amount);

            _logger.LogInformation("Calculated current reserve balance {Balance:C} (EntryScope={EntryScope})", balance, normalizedEntryScope);
            return balance;
        }

        /// <summary>
        /// Gets budget entries for variance analysis
        /// </summary>
        public async Task<IEnumerable<BudgetEntry>> GetBudgetEntriesForVarianceAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.BudgetEntries
                .AsNoTracking()
                .Include(be => be.Fund)
                .Include(be => be.MunicipalAccount)
                .Where(be => be.StartPeriod >= startDate && be.EndPeriod <= endDate)
                .Where(be => be.BudgetedAmount > 0) // Only entries with budget amounts
                .OrderBy(be => be.AccountNumber)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Gets municipal account information for analytics
        /// </summary>
        public async Task<IEnumerable<MunicipalAccount>> GetMunicipalAccountsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.MunicipalAccounts
                .AsNoTracking()
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Gets available entity names for filtering
        /// </summary>
        public async Task<IEnumerable<string>> GetAvailableEntitiesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var entityRows = await context.SourceFiles
                .AsNoTracking()
                .Select(be => new
                {
                    be.CanonicalEntity,
                    be.OriginalFileName
                })
                .ToListAsync(cancellationToken);

            return entityRows
                .Select(be => !string.IsNullOrWhiteSpace(be.CanonicalEntity)
                    ? be.CanonicalEntity!
                    : be.OriginalFileName)
                .Where(entity => !string.IsNullOrWhiteSpace(entity))
                .Distinct()
                .OrderBy(entity => entity)
                .ToList();
        }

        /// <summary>
        /// Gets the weighted current-rate baseline across enterprises.
        /// </summary>
        public async Task<decimal?> GetPortfolioCurrentRateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var enterpriseRates = await context.Enterprises
                .AsNoTracking()
                .Where(e => e.CurrentRate > 0)
                .Select(e => new
                {
                    e.CurrentRate,
                    e.CitizenCount
                })
                .ToListAsync(cancellationToken);

            if (enterpriseRates.Count == 0)
            {
                _logger.LogWarning("No enterprise current-rate data found for scenario analysis");
                return null;
            }

            var weightedCitizenCount = enterpriseRates.Where(e => e.CitizenCount > 0).Sum(e => e.CitizenCount);
            var baselineRate = weightedCitizenCount > 0
                ? enterpriseRates.Sum(e => e.CurrentRate * e.CitizenCount) / weightedCitizenCount
                : enterpriseRates.Average(e => e.CurrentRate);

            baselineRate = Math.Round(baselineRate, 2, MidpointRounding.AwayFromZero);

            _logger.LogInformation(
                "Calculated portfolio current-rate baseline {CurrentRate} across {EnterpriseCount} enterprise(s)",
                baselineRate,
                enterpriseRates.Count);

            return baselineRate;
        }

        /// <summary>
        /// Gets trend data for revenue, expenses, and net position grouped by fiscal year,
        /// extended with simple linear projections for <paramref name="projectionYears"/> future years.
        /// </summary>
        public async Task<List<TrendSeries>> GetTrendDataAsync(int projectionYears = 3, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Pull lightweight projection from DB; group in memory to avoid EF Core
            // translation issues with conditional aggregations inside GroupBy.
            var entries = await context.BudgetEntries
                .AsNoTracking()
                .Where(be => be.FiscalYear > 2000)
                .Select(be => new
                {
                    be.FiscalYear,
                    be.AccountNumber,
                    be.ActualAmount,
                    be.BudgetedAmount
                })
                .ToListAsync(cancellationToken);

            if (!entries.Any())
            {
                _logger.LogWarning("GetTrendDataAsync: No budget entries found for trend analysis");
                return new List<TrendSeries>();
            }

            // GASB account number prefixes: 4xx = Revenue, 5xx/6xx = Expenditures
            var byYear = entries
                .GroupBy(e => e.FiscalYear)
                .Select(g => new
                {
                    FiscalYear = g.Key,
                    ActualRevenue = g.Where(e => e.AccountNumber.StartsWith("4")).Sum(e => e.ActualAmount),
                    ActualExpenses = g.Where(e => e.AccountNumber.StartsWith("5") || e.AccountNumber.StartsWith("6")).Sum(e => e.ActualAmount),
                    BudgetedRevenue = g.Where(e => e.AccountNumber.StartsWith("4")).Sum(e => e.BudgetedAmount),
                    BudgetedExpenses = g.Where(e => e.AccountNumber.StartsWith("5") || e.AccountNumber.StartsWith("6")).Sum(e => e.BudgetedAmount)
                })
                .OrderBy(g => g.FiscalYear)
                .ToList();

            // Build historical points (one point per fiscal year, dated Jan 1 of that year)
            var revenuePoints = byYear.Select(y => new TrendPoint(new DateTime(y.FiscalYear, 1, 1), y.ActualRevenue)).ToList();
            var expensePoints = byYear.Select(y => new TrendPoint(new DateTime(y.FiscalYear, 1, 1), y.ActualExpenses)).ToList();
            var budgetedRevenuePoints = byYear.Select(y => new TrendPoint(new DateTime(y.FiscalYear, 1, 1), y.BudgetedRevenue)).ToList();
            var netPositionPoints = byYear.Select(y => new TrendPoint(new DateTime(y.FiscalYear, 1, 1), y.ActualRevenue - y.ActualExpenses)).ToList();

            // Append projected future years using average YoY growth from available history
            if (projectionYears > 0 && byYear.Count >= 2)
            {
                int lastYear = byYear.Last().FiscalYear;
                decimal revenueGrowth = CalculateAvgGrowthRate(byYear.Select(y => y.ActualRevenue).ToList());
                decimal expenseGrowth = CalculateAvgGrowthRate(byYear.Select(y => y.ActualExpenses).ToList());

                decimal projRevenue = byYear.Last().ActualRevenue;
                decimal projExpenses = byYear.Last().ActualExpenses;

                for (int i = 1; i <= projectionYears; i++)
                {
                    projRevenue = Math.Round(projRevenue * (1 + revenueGrowth), 2);
                    projExpenses = Math.Round(projExpenses * (1 + expenseGrowth), 2);
                    var projDate = new DateTime(lastYear + i, 1, 1);
                    revenuePoints.Add(new TrendPoint(projDate, projRevenue));
                    expensePoints.Add(new TrendPoint(projDate, projExpenses));
                    netPositionPoints.Add(new TrendPoint(projDate, Math.Round(projRevenue - projExpenses, 2)));
                    // Budgeted projection uses same growth rate as actual revenue/expenses
                    budgetedRevenuePoints.Add(new TrendPoint(projDate, Math.Round(byYear.Last().BudgetedRevenue * (decimal)Math.Pow((double)(1 + revenueGrowth), i), 2)));
                }
            }

            _logger.LogInformation(
                "GetTrendDataAsync: {HistoricalYears} historical fiscal year(s) + {ProjectedYears} projected year(s)",
                byYear.Count, projectionYears);

            return new List<TrendSeries>
            {
                new TrendSeries("Revenue",          revenuePoints),
                new TrendSeries("Expenses",         expensePoints),
                new TrendSeries("Budgeted Revenue", budgetedRevenuePoints),
                new TrendSeries("Net Position",     netPositionPoints)
            };
        }

        /// <summary>
        /// Calculates the average year-over-year growth rate from a list of ordered values.
        /// Clamped to ±50% to prevent runaway projections from outlier data.
        /// Returns 0.02 (2%) if fewer than two non-zero values are present.
        /// </summary>
        private static decimal CalculateAvgGrowthRate(List<decimal> values)
        {
            if (values.Count < 2) return 0.02m;

            decimal total = 0m;
            int count = 0;

            for (int i = 1; i < values.Count; i++)
            {
                if (values[i - 1] != 0)
                {
                    decimal rate = (values[i] - values[i - 1]) / Math.Abs(values[i - 1]);
                    total += Math.Clamp(rate, -0.50m, 0.50m);
                    count++;
                }
            }

            return count > 0 ? total / count : 0.02m;
        }

        /// <summary>
        /// Runs a scenario analysis
        /// </summary>
        public async Task<ScenarioResult> RunScenarioAsync(decimal rateIncreasePercent, decimal expenseIncreasePercent, decimal revenueTarget, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Clamp extreme inputs to avoid unrealistic projections
            rateIncreasePercent = Math.Max(-50m, Math.Min(200m, rateIncreasePercent));
            expenseIncreasePercent = Math.Max(-50m, Math.Min(200m, expenseIncreasePercent));

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Prefer current fiscal year data; fall back to most recent available year
            var currentYear = DateTime.Now.Year;
            var yearEntries = await context.BudgetEntries
                .AsNoTracking()
                .Where(be => be.FiscalYear == currentYear)
                .Select(be => new { be.AccountNumber, be.ActualAmount, be.BudgetedAmount })
                .ToListAsync(cancellationToken);

            if (!yearEntries.Any())
            {
                var latestYear = await context.BudgetEntries
                    .AsNoTracking()
                    .OrderByDescending(be => be.FiscalYear)
                    .Select(be => be.FiscalYear)
                    .FirstOrDefaultAsync(cancellationToken);

                if (latestYear == 0)
                {
                    _logger.LogWarning("RunScenarioAsync: No budget entries available; returning empty scenario");
                    return new ScenarioResult("No data available", 0m, -revenueTarget);
                }

                yearEntries = await context.BudgetEntries
                    .AsNoTracking()
                    .Where(be => be.FiscalYear == latestYear)
                    .Select(be => new { be.AccountNumber, be.ActualAmount, be.BudgetedAmount })
                    .ToListAsync(cancellationToken);
                currentYear = latestYear;
            }

            // Use actuals when available; fall back to budgeted amounts
            decimal baselineRevenue = yearEntries.Where(e => e.AccountNumber.StartsWith("4"))
                .Select(e => e.ActualAmount == 0 ? e.BudgetedAmount : e.ActualAmount)
                .Sum();

            decimal baselineExpenses = yearEntries.Where(e => e.AccountNumber.StartsWith("5") || e.AccountNumber.StartsWith("6"))
                .Select(e => e.ActualAmount == 0 ? e.BudgetedAmount : e.ActualAmount)
                .Sum();

            if (baselineRevenue == 0 && baselineExpenses == 0)
            {
                _logger.LogWarning("RunScenarioAsync: No revenue or expense data for fiscal year {Year}", currentYear);
                return new ScenarioResult($"No data for FY {currentYear}", 0m, -revenueTarget);
            }

            var projectedRevenue = baselineRevenue * (1 + rateIncreasePercent / 100m);
            var projectedExpenses = baselineExpenses * (1 + expenseIncreasePercent / 100m);
            var projectedNet = Math.Round(projectedRevenue - projectedExpenses, 2);

            var varianceToTarget = Math.Round(projectedRevenue - revenueTarget, 2);
            var description = $"FY {currentYear}: +{rateIncreasePercent:0.##}% revenue, +{expenseIncreasePercent:0.##}% expenses vs target {revenueTarget:C0}";

            _logger.LogInformation(
                "Scenario analysis: year={Year}, baselineRevenue={BaselineRevenue}, baselineExpenses={BaselineExpenses}, projectedRevenue={ProjectedRevenue}, projectedExpenses={ProjectedExpenses}, target={Target}",
                currentYear, baselineRevenue, baselineExpenses, projectedRevenue, projectedExpenses, revenueTarget);

            return new ScenarioResult(description, projectedNet, varianceToTarget);
        }

        private static string? NormalizeEntryScope(string? entryScope)
            => string.IsNullOrWhiteSpace(entryScope)
                ? null
                : entryScope.Trim();

        private static bool IsReserveAccount(string? accountName)
            => !string.IsNullOrWhiteSpace(accountName)
                && (accountName.StartsWith("1", StringComparison.Ordinal)
                    || accountName.StartsWith("2", StringComparison.Ordinal)
                    || accountName.StartsWith("3", StringComparison.Ordinal));

        private static List<T> PreferGeneralLedgerRows<T>(List<T> rows, Func<T, string?> fileNameSelector)
        {
            if (rows.Count == 0)
            {
                return rows;
            }

            var generalLedgerRows = rows
                .Where(row => LooksLikeGeneralLedgerFile(fileNameSelector(row)))
                .ToList();

            return generalLedgerRows.Count > 0 ? generalLedgerRows : rows;
        }

        private static bool LooksLikeGeneralLedgerFile(string? fileName)
            => !string.IsNullOrWhiteSpace(fileName)
                && fileName.Contains("GeneralLedger", StringComparison.OrdinalIgnoreCase);
    }
}
