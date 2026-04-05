using Moq;
using Xunit;
using WileyWidget.Services;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using System.Threading;

namespace WileyWidget.Tests;

public class AnalyticsServicePhase1Tests
{
    private readonly Mock<IBudgetRepository> _budgetRepositoryMock;
    private readonly Mock<IAnalyticsRepository> _analyticsRepositoryMock;
    private readonly Mock<IBudgetAnalyticsRepository> _budgetAnalyticsRepositoryMock;
    private readonly Mock<ILogger<AnalyticsService>> _loggerMock;
    private readonly AnalyticsService _service;

    public AnalyticsServicePhase1Tests()
    {
        _budgetRepositoryMock = new Mock<IBudgetRepository>();
        _analyticsRepositoryMock = new Mock<IAnalyticsRepository>();
        _budgetAnalyticsRepositoryMock = new Mock<IBudgetAnalyticsRepository>();
        _loggerMock = new Mock<ILogger<AnalyticsService>>();

        _service = new AnalyticsService(
            _budgetRepositoryMock.Object,
            _analyticsRepositoryMock.Object,
            _budgetAnalyticsRepositoryMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task RunRateScenarioAsync_CalculatesExpectedIncreases()
    {
        // Arrange
        var parameters = new RateScenarioParameters
        {
            RateIncreasePercentage = 0.10m, // 10%
            ExpenseIncreasePercentage = 0.05m // 5%
        };

        var budgetEntries = new List<BudgetEntry>
        {
            new BudgetEntry { ActualAmount = 1000, BudgetedAmount = 1100 },
            new BudgetEntry { ActualAmount = 2000, BudgetedAmount = 2200 }
        };

        _budgetRepositoryMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(budgetEntries);

        _analyticsRepositoryMock.Setup(r => r.GetPortfolioCurrentRateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(100.0m);

        // Act
        var result = await _service.RunRateScenarioAsync(parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(110.0m, result.ProjectedRate); // 100 * 1.10
        Assert.Equal(300.0m, result.RevenueImpact); // (1000 + 2000) * 0.10
        _budgetRepositoryMock.Verify(r => r.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunRateScenarioAsync_ThrowsException_WhenNoBudgetData()
    {
        // Arrange
        var parameters = new RateScenarioParameters
        {
            RateIncreasePercentage = 0.10m,
            ExpenseIncreasePercentage = 0.05m
        };

        _budgetRepositoryMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetEntry>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunRateScenarioAsync(parameters));
    }

    [Fact]
    public async Task RunRateScenarioAsync_ThrowsException_WhenNoRateData()
    {
        // Arrange
        var parameters = new RateScenarioParameters
        {
            RateIncreasePercentage = 0.10m,
            ExpenseIncreasePercentage = 0.05m
        };

        var budgetEntries = new List<BudgetEntry>
        {
            new BudgetEntry { ActualAmount = 1000, BudgetedAmount = 1100 }
        };

        _budgetRepositoryMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(budgetEntries);

        _analyticsRepositoryMock.Setup(r => r.GetPortfolioCurrentRateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunRateScenarioAsync(parameters));
    }

    [Fact]
    public async Task GenerateReserveForecastAsync_CalculatesExpectedTrend()
    {
        // Arrange
        var historicalData = new List<ReserveDataPoint>
        {
            new ReserveDataPoint { Date = new DateTime(2025, 1, 1), Reserves = 100000 },
            new ReserveDataPoint { Date = new DateTime(2025, 2, 1), Reserves = 110000 }
        };

        _budgetAnalyticsRepositoryMock.Setup(r => r.GetReserveHistoryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(historicalData);

        _analyticsRepositoryMock.Setup(r => r.GetCurrentReserveBalanceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(110000m);

        _budgetRepositoryMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetEntry> { new BudgetEntry { BudgetedAmount = 500000 } });

        // Act
        var result = await _service.GenerateReserveForecastAsync(1); // 1 year

        // Assert
        Assert.NotNull(result);
        Assert.Equal(12, result.ForecastPoints.Count); // 12 months in 1 year

        // Match AnalyticsService trend logic: monthly trend is normalized by 30.44 days.
        var months = (decimal)((historicalData[1].Date - historicalData[0].Date).TotalDays / 30.44);
        var expectedMonthlyTrend = (historicalData[1].Reserves - historicalData[0].Reserves) / months;
        var expectedFirstMonth = historicalData[1].Reserves + expectedMonthlyTrend;
        var expectedTwelfthMonth = historicalData[1].Reserves + (expectedMonthlyTrend * 12);
        const decimal tolerance = 0.001m;

        Assert.InRange(result.ForecastPoints[0].PredictedReserves, expectedFirstMonth - tolerance, expectedFirstMonth + tolerance);
        Assert.InRange(result.ForecastPoints[11].PredictedReserves, expectedTwelfthMonth - tolerance, expectedTwelfthMonth + tolerance);

        // Forecast should grow monotonically with a positive trend.
        for (var i = 1; i < result.ForecastPoints.Count; i++)
        {
            Assert.True(result.ForecastPoints[i].PredictedReserves >= result.ForecastPoints[i - 1].PredictedReserves);
        }

        // Confidence interval should remain 10% of the predicted reserve in current implementation.
        var expectedFirstCi = Math.Abs(result.ForecastPoints[0].PredictedReserves * 0.1m);
        Assert.InRange(result.ForecastPoints[0].ConfidenceInterval, expectedFirstCi - tolerance, expectedFirstCi + tolerance);
    }
}
