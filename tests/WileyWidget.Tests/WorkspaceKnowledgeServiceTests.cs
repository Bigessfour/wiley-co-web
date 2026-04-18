using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Tests;

public sealed class WorkspaceKnowledgeServiceTests
{
    [Fact]
    public async Task BuildAsync_StartsReserveForecastVarianceAndOverviewWorkInParallel()
    {
        var tracker = new ConcurrentDependencyTracker(expectedCount: 4);
        var service = CreateService(
            new TrackingAnalyticsService(tracker),
            new TrackingAnalyticsRepository(tracker),
            new TrackingBudgetAnalyticsRepository(tracker));

        var buildTask = service.BuildAsync(CreateInput());

        await tracker.WaitForAllStartedAsync();
        Assert.Equal(4, tracker.StartedNames.Count);

        tracker.Release();
        var result = await buildTask;

        Assert.Equal("Water Utility", result.SelectedEnterprise);
        Assert.Equal(2026, result.SelectedFiscalYear);
        Assert.True(tracker.MaxConcurrency >= 4);
        Assert.Single(result.TopVariances);
        Assert.Equal("Chemicals", result.TopVariances[0].AccountName);
    }

    [Fact]
    public async Task BuildAsync_WhenDependencyFails_WrapsDependencyNameInUnavailableException()
    {
        var service = CreateService(
            new StaticAnalyticsService(),
            new StaticAnalyticsRepository(),
            new ThrowingBudgetAnalyticsRepository());

        var exception = await Assert.ThrowsAsync<WorkspaceKnowledgeUnavailableException>(() => service.BuildAsync(CreateInput()));

        Assert.Contains("top budget variances", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task BuildAsync_WhenCanceled_PropagatesOperationCanceledException()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var service = CreateService(
            new CancellationAwareAnalyticsService(),
            new CancellationAwareAnalyticsRepository(),
            new CancellationAwareBudgetAnalyticsRepository());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.BuildAsync(CreateInput(), cancellationTokenSource.Token));
    }

    private static WorkspaceKnowledgeInput CreateInput()
        => new("Water Utility", 2026, 55.25m, 13250m, 240m, 1500m);

    private static WorkspaceKnowledgeService CreateService(
        IAnalyticsService analyticsService,
        IAnalyticsRepository analyticsRepository,
        IBudgetAnalyticsRepository budgetAnalyticsRepository)
    {
        return new WorkspaceKnowledgeService(
            new NullEnterpriseRepository(),
            analyticsService,
            analyticsRepository,
            budgetAnalyticsRepository,
            NullLogger<WorkspaceKnowledgeService>.Instance);
    }

    private sealed class ConcurrentDependencyTracker
    {
        private readonly object sync = new();
        private readonly int expectedCount;
        private readonly HashSet<string> startedNames = [];
        private readonly TaskCompletionSource allStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int inFlight;
        private int maxConcurrency;

        public ConcurrentDependencyTracker(int expectedCount)
        {
            this.expectedCount = expectedCount;
        }

        public IReadOnlyCollection<string> StartedNames
        {
            get
            {
                lock (sync)
                {
                    return startedNames.ToArray();
                }
            }
        }

        public int MaxConcurrency
        {
            get
            {
                lock (sync)
                {
                    return maxConcurrency;
                }
            }
        }

        public Task WaitForAllStartedAsync() => allStarted.Task;

        public void Release() => gate.TrySetResult();

        public async Task<T> TrackAsync<T>(string dependencyName, Func<T> valueFactory)
        {
            lock (sync)
            {
                startedNames.Add(dependencyName);
                if (startedNames.Count >= expectedCount)
                {
                    allStarted.TrySetResult();
                }
            }

            var currentConcurrency = Interlocked.Increment(ref inFlight);
            lock (sync)
            {
                if (currentConcurrency > maxConcurrency)
                {
                    maxConcurrency = currentConcurrency;
                }
            }

            await gate.Task;
            Interlocked.Decrement(ref inFlight);
            return valueFactory();
        }
    }

    private sealed class TrackingAnalyticsService : IAnalyticsService
    {
        private readonly ConcurrentDependencyTracker tracker;

        public TrackingAnalyticsService(ConcurrentDependencyTracker tracker)
        {
            this.tracker = tracker;
        }

        public Task<ReserveForecastResult> GenerateReserveForecastAsync(int yearsAhead, string? entryScope = null, CancellationToken cancellationToken = default)
            => tracker.TrackAsync("reserve-forecast", () => new ReserveForecastResult
            {
                CurrentReserves = 120000m,
                RecommendedReserveLevel = 95000m,
                RiskAssessment = "Stable"
            });

        public Task<BudgetOverviewData> GetBudgetOverviewAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
            => tracker.TrackAsync("budget-overview", () => new BudgetOverviewData
            {
                TotalBudget = 13250m,
                TotalActual = 14500m,
                TotalVariance = 1250m,
                OverBudgetCount = 2,
                UnderBudgetCount = 3
            });

        public Task<BudgetAnalysisResult> PerformExploratoryAnalysisAsync(DateTime startDate, DateTime endDate, string? entityName = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RateScenarioResult> RunRateScenarioAsync(RateScenarioParameters parameters, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<BudgetMetric>> GetBudgetMetricsAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<SummaryKpi>> GetSummaryKpisAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<TrendSeries>> GetTrendDataAsync(int projectionYears = 3, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ScenarioResult> RunScenarioAsync(decimal rateIncreasePercent, decimal expenseIncreasePercent, decimal revenueTarget, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<VarianceRecord>> GetVarianceDetailsAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TrackingAnalyticsRepository : IAnalyticsRepository
    {
        private readonly ConcurrentDependencyTracker tracker;

        public TrackingAnalyticsRepository(ConcurrentDependencyTracker tracker)
        {
            this.tracker = tracker;
        }

        public Task<decimal> GetCurrentReserveBalanceAsync(string? entryScope = null, CancellationToken cancellationToken = default)
            => tracker.TrackAsync("reserve-balance", () => 120000m);

        public Task<IEnumerable<ReserveDataPoint>> GetHistoricalReserveDataAsync(DateTime startDate, DateTime endDate, string? entryScope = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<MunicipalAccount>> GetMunicipalAccountsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<string>> GetAvailableEntitiesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<decimal?> GetPortfolioCurrentRateAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<TrendSeries>> GetTrendDataAsync(int projectionYears = 3, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ScenarioResult> RunScenarioAsync(decimal rateIncreasePercent, decimal expenseIncreasePercent, decimal revenueTarget, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TrackingBudgetAnalyticsRepository : IBudgetAnalyticsRepository
    {
        private readonly ConcurrentDependencyTracker tracker;

        public TrackingBudgetAnalyticsRepository(ConcurrentDependencyTracker tracker)
        {
            this.tracker = tracker;
        }

        public Task<List<VarianceAnalysis>> GetTopVariancesAsync(int topN, int fiscalYear, CancellationToken ct = default)
            => tracker.TrackAsync("top-variances", () => new List<VarianceAnalysis>
            {
                new()
                {
                    AccountName = "Chemicals",
                    BudgetedAmount = 10000m,
                    ActualAmount = 12500m,
                    VarianceAmount = 2500m,
                    VariancePercentage = 25m
                }
            });

        public Task<List<ReserveDataPoint>> GetReserveHistoryAsync(DateTime from, DateTime to, string? entryScope = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Dictionary<string, decimal>> GetCategoryBreakdownAsync(DateTime start, DateTime end, string? entityName, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<TrendAnalysis> GetTrendAnalysisAsync(DateTime start, DateTime end, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<BudgetOverviewData> GetBudgetOverviewDataAsync(int fiscalYear, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<List<BudgetMetric>> GetBudgetMetricsAsync(int fiscalYear, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<List<SummaryKpi>> GetSummaryKpisAsync(int fiscalYear, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<List<VarianceRecord>> GetVarianceDetailsAsync(int fiscalYear, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class StaticAnalyticsService : IAnalyticsService
    {
        public Task<ReserveForecastResult> GenerateReserveForecastAsync(int yearsAhead, string? entryScope = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ReserveForecastResult
            {
                CurrentReserves = 120000m,
                RecommendedReserveLevel = 95000m,
                RiskAssessment = "Stable"
            });

        public Task<BudgetOverviewData> GetBudgetOverviewAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new BudgetOverviewData
            {
                TotalBudget = 13250m,
                TotalActual = 14500m,
                TotalVariance = 1250m,
                OverBudgetCount = 2,
                UnderBudgetCount = 3
            });

        public Task<BudgetAnalysisResult> PerformExploratoryAnalysisAsync(DateTime startDate, DateTime endDate, string? entityName = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RateScenarioResult> RunRateScenarioAsync(RateScenarioParameters parameters, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<BudgetMetric>> GetBudgetMetricsAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<SummaryKpi>> GetSummaryKpisAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<TrendSeries>> GetTrendDataAsync(int projectionYears = 3, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ScenarioResult> RunScenarioAsync(decimal rateIncreasePercent, decimal expenseIncreasePercent, decimal revenueTarget, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<VarianceRecord>> GetVarianceDetailsAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StaticAnalyticsRepository : IAnalyticsRepository
    {
        public Task<decimal> GetCurrentReserveBalanceAsync(string? entryScope = null, CancellationToken cancellationToken = default)
            => Task.FromResult(120000m);

        public Task<IEnumerable<ReserveDataPoint>> GetHistoricalReserveDataAsync(DateTime startDate, DateTime endDate, string? entryScope = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<MunicipalAccount>> GetMunicipalAccountsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<string>> GetAvailableEntitiesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<decimal?> GetPortfolioCurrentRateAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<TrendSeries>> GetTrendDataAsync(int projectionYears = 3, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ScenarioResult> RunScenarioAsync(decimal rateIncreasePercent, decimal expenseIncreasePercent, decimal revenueTarget, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingBudgetAnalyticsRepository : IBudgetAnalyticsRepository
    {
        public Task<List<VarianceAnalysis>> GetTopVariancesAsync(int topN, int fiscalYear, CancellationToken ct = default)
            => throw new InvalidOperationException("Variance query failed.");

        public Task<List<ReserveDataPoint>> GetReserveHistoryAsync(DateTime from, DateTime to, string? entryScope = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Dictionary<string, decimal>> GetCategoryBreakdownAsync(DateTime start, DateTime end, string? entityName, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<TrendAnalysis> GetTrendAnalysisAsync(DateTime start, DateTime end, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<BudgetOverviewData> GetBudgetOverviewDataAsync(int fiscalYear, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<List<BudgetMetric>> GetBudgetMetricsAsync(int fiscalYear, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<List<SummaryKpi>> GetSummaryKpisAsync(int fiscalYear, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<List<VarianceRecord>> GetVarianceDetailsAsync(int fiscalYear, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class CancellationAwareAnalyticsService : IAnalyticsService
    {
        public Task<ReserveForecastResult> GenerateReserveForecastAsync(int yearsAhead, string? entryScope = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ReserveForecastResult());
        }

        public Task<BudgetOverviewData> GetBudgetOverviewAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new BudgetOverviewData());
        }

        public Task<BudgetAnalysisResult> PerformExploratoryAnalysisAsync(DateTime startDate, DateTime endDate, string? entityName = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RateScenarioResult> RunRateScenarioAsync(RateScenarioParameters parameters, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<BudgetMetric>> GetBudgetMetricsAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<SummaryKpi>> GetSummaryKpisAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<TrendSeries>> GetTrendDataAsync(int projectionYears = 3, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ScenarioResult> RunScenarioAsync(decimal rateIncreasePercent, decimal expenseIncreasePercent, decimal revenueTarget, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<VarianceRecord>> GetVarianceDetailsAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class CancellationAwareAnalyticsRepository : IAnalyticsRepository
    {
        public Task<decimal> GetCurrentReserveBalanceAsync(string? entryScope = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(0m);
        }

        public Task<IEnumerable<ReserveDataPoint>> GetHistoricalReserveDataAsync(DateTime startDate, DateTime endDate, string? entryScope = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<MunicipalAccount>> GetMunicipalAccountsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<string>> GetAvailableEntitiesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<decimal?> GetPortfolioCurrentRateAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<TrendSeries>> GetTrendDataAsync(int projectionYears = 3, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ScenarioResult> RunScenarioAsync(decimal rateIncreasePercent, decimal expenseIncreasePercent, decimal revenueTarget, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class CancellationAwareBudgetAnalyticsRepository : IBudgetAnalyticsRepository
    {
        public Task<List<VarianceAnalysis>> GetTopVariancesAsync(int topN, int fiscalYear, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new List<VarianceAnalysis>());
        }

        public Task<List<ReserveDataPoint>> GetReserveHistoryAsync(DateTime from, DateTime to, string? entryScope = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Dictionary<string, decimal>> GetCategoryBreakdownAsync(DateTime start, DateTime end, string? entityName, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<TrendAnalysis> GetTrendAnalysisAsync(DateTime start, DateTime end, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<BudgetOverviewData> GetBudgetOverviewDataAsync(int fiscalYear, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<List<BudgetMetric>> GetBudgetMetricsAsync(int fiscalYear, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<List<SummaryKpi>> GetSummaryKpisAsync(int fiscalYear, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<List<VarianceRecord>> GetVarianceDetailsAsync(int fiscalYear, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class NullEnterpriseRepository : IEnterpriseRepository
    {
        public Task<IEnumerable<Enterprise>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<Enterprise>>([]);

        public Task<Enterprise?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<Enterprise?>(null);

        public Task<IEnumerable<Enterprise>> GetByTypeAsync(string type, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<Enterprise>>([]);

        public Task<Enterprise> AddAsync(Enterprise enterprise, CancellationToken cancellationToken = default)
            => Task.FromResult(enterprise);

        public Task<Enterprise> UpdateAsync(Enterprise enterprise, CancellationToken cancellationToken = default)
            => Task.FromResult(enterprise);

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}