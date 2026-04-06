using Microsoft.Extensions.Logging;
using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class StartupTimelineServiceTests
{
    [Fact]
    public void PhaseConfig_GetOrDefault_ReturnsConfiguredAndFallbackValues()
    {
        var known = PhaseConfig.GetOrDefault("Theme Initialization");
        var unknown = PhaseConfig.GetOrDefault("Not A Real Phase");

        Assert.Equal(2, known.ExpectedOrder);
        Assert.True(known.IsUiCritical);
        Assert.Equal(0, unknown.ExpectedOrder);
        Assert.False(unknown.IsUiCritical);
    }

    [Fact]
    public void StartupTimelineReport_DetectsViolations_AndSummarizesDurations()
    {
        var report = new StartupTimelineReport
        {
            UiThreadId = 11,
            Events =
            {
                new StartupEvent
                {
                    Name = "Theme Initialization",
                    Type = "Phase",
                    ExpectedOrder = 2,
                    ChronologicalOrder = 1,
                    ThreadId = 11,
                    StartTime = new DateTime(2026, 1, 1, 8, 0, 0),
                    EndTime = new DateTime(2026, 1, 1, 8, 0, 2)
                },
                new StartupEvent
                {
                    Name = "MainForm Creation",
                    Type = "Phase",
                    ExpectedOrder = 1,
                    ChronologicalOrder = 2,
                    ThreadId = 11,
                    DependsOn = "Theme Initialization",
                    StartTime = new DateTime(2026, 1, 1, 8, 0, 1),
                    EndTime = new DateTime(2026, 1, 1, 8, 0, 4)
                },
                new StartupEvent
                {
                    Name = "Ribbon Init",
                    Type = "Phase",
                    ExpectedOrder = 3,
                    ChronologicalOrder = 3,
                    ThreadId = 99,
                    IsUiCritical = true,
                    StartTime = new DateTime(2026, 1, 1, 8, 0, 5),
                    EndTime = new DateTime(2026, 1, 1, 8, 0, 8)
                }
            }
        };

        var orderViolations = report.GetOrderViolations();
        var dependencyViolations = report.GetDependencyViolations();
        var threadIssues = report.GetThreadAffinityIssues();
        var summary = report.GetSummaryStats();

        Assert.NotEmpty(orderViolations);
        Assert.NotEmpty(dependencyViolations);
        Assert.NotEmpty(threadIssues);
        Assert.Equal("MainForm Creation", summary.LongestUiPhaseName);
        Assert.Equal(3000, summary.LongestUiPhaseMs);
        Assert.Equal(1, summary.PotentialFreezes);
    }

    [Fact]
    public void StartupTimelineService_RecordsPhasesAndGeneratesIssues_WhenEnabled()
    {
        var previous = Environment.GetEnvironmentVariable("WILEYWIDGET_TRACK_STARTUP_TIMELINE");

        try
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_TRACK_STARTUP_TIMELINE", "true");

            var service = new StartupTimelineService(LoggerFactory.Create(builder => { }).CreateLogger<StartupTimelineService>());

            Assert.True(service.IsEnabled);

            using (service.BeginPhaseScope("License Registration"))
            {
                service.RecordOperation("Scope Active", "License Registration", 1);
            }

            service.RecordPhaseStart("Theme Initialization");
            service.RecordOperation("Load Ribbon", "Theme Initialization", 2501);
            service.RecordFormLifecycleEvent("MainForm", "Load");
            service.RecordPhaseStart("MainForm Creation");

            Assert.Equal("MainForm Creation", service.CurrentPhase);

            var report = service.GenerateReport();

            Assert.Contains(report.Errors, error => error.Contains("dependency", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(report.Errors, error => error.Contains("never completed", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_TRACK_STARTUP_TIMELINE", previous);
        }
    }
}