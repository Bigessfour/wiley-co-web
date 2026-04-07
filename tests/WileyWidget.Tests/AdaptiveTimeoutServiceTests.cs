using Microsoft.Extensions.Logging;
using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class AdaptiveTimeoutServiceTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder => { });

    private AdaptiveTimeoutService CreateService()
    {
        return new AdaptiveTimeoutService(_loggerFactory.CreateLogger<AdaptiveTimeoutService>());
    }

    [Fact]
    public void GetRecommendedTimeoutSeconds_ReturnsBaseTimeout_WhenSampleCountIsTooLow()
    {
        var service = CreateService();

        for (var sample = 1; sample <= 9; sample++)
        {
            service.RecordLatency(sample);
        }

        var timeout = service.GetRecommendedTimeoutSeconds();

        Assert.Equal(15.0, timeout);
    }

    [Fact]
    public void GetStatistics_ReturnsExpectedValues_WhenTenSamplesAreTracked()
    {
        var service = CreateService();

        for (var sample = 1; sample <= 10; sample++)
        {
            service.RecordLatency(sample);
        }

        var statistics = service.GetStatistics();

        Assert.Equal(10, statistics.SampleCount);
        Assert.Equal(5.5, statistics.AverageLatencySeconds);
        Assert.Equal(10.0, statistics.P95LatencySeconds);
        Assert.Equal(15.0, statistics.RecommendedTimeoutSeconds);
    }

    [Fact]
    public void RecordLatency_IgnoresNegativeValues()
    {
        var service = CreateService();

        service.RecordLatency(-2);
        service.RecordLatency(4.5);

        var statistics = service.GetStatistics();

        Assert.Equal(1, statistics.SampleCount);
        Assert.Equal(4.5, statistics.AverageLatencySeconds);
    }

    [Fact]
    public void GetRecommendedTimeoutSeconds_ClampsToMinimum_WhenCalculatedValueIsTooSmall()
    {
        var service = CreateService();

        for (var sample = 1; sample <= 10; sample++)
        {
            service.RecordLatency(1);
        }

        var timeout = service.GetRecommendedTimeoutSeconds();

        Assert.Equal(5.0, timeout);
    }

    [Fact]
    public void GetRecommendedTimeoutSeconds_ClampsToMaximum_WhenCalculatedValueIsTooLarge()
    {
        var service = CreateService();

        for (var sample = 1; sample <= 10; sample++)
        {
            service.RecordLatency(100);
        }

        var timeout = service.GetRecommendedTimeoutSeconds();

        Assert.Equal(60.0, timeout);
    }

    [Fact]
    public void Reset_ClearsTrackedLatencies()
    {
        var service = CreateService();

        service.RecordLatency(4.5);
        service.RecordLatency(8.5);

        service.Reset();

        var statistics = service.GetStatistics();

        Assert.Equal(0, statistics.SampleCount);
        Assert.Equal(15.0, statistics.AverageLatencySeconds);
        Assert.Equal(15.0, statistics.P95LatencySeconds);
        Assert.Equal(15.0, statistics.RecommendedTimeoutSeconds);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }
}