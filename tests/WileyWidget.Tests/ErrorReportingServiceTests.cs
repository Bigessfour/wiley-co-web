using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Tests;

public sealed class ErrorReportingServiceTests
{
	private readonly ILogger<ErrorReportingService> _logger = LoggerFactory.Create(builder => { }).CreateLogger<ErrorReportingService>();

	[Fact]
	public void ReportError_RaisesEvents_AndRecordsTelemetry()
	{
		var service = new ErrorReportingService(_logger)
		{
			SuppressUserDialogs = true
		};

		var reported = new List<(Exception Exception, string? Context)>();
		var telemetryEvents = new List<TelemetryEvent>();
		var telemetryService = new FakeTelemetryService();

		service.ErrorReported += (exception, context) => reported.Add((exception, context));
		service.TelemetryCollected += telemetryEvents.Add;
		service.SetTelemetryService(telemetryService);

		service.ReportError(new InvalidOperationException("boom"), "TestContext", showToUser: true);

		Assert.Contains(reported, item => item.Context == "TestContext" && item.Exception.Message == "boom");
		Assert.Contains(telemetryEvents, item => item.EventName == "Exception_Occurred");
		Assert.Single(telemetryService.RecordedExceptions);
	}

	[Fact]
	public void TrackEvent_Metric_AndDependency_PopulateTelemetryStore()
	{
		var service = new ErrorReportingService(_logger)
		{
			SuppressUserDialogs = true
		};

		service.TrackEvent("CustomEvent", new Dictionary<string, object> { ["Key"] = "Value" });
		service.TrackMetric("LatencyMs", 42.5);
		service.TrackDependency("SqlQuery", "SELECT 1", TimeSpan.FromMilliseconds(12), success: true);

		var eventNames = service.GetRecentTelemetryEvents(10).Select(item => item.EventName).ToList();

		Assert.Contains("CustomEvent", eventNames);
		Assert.Contains("Metric_LatencyMs", eventNames);
		Assert.Contains("Dependency_SqlQuery", eventNames);

		var exported = service.ExportTelemetryData();

		Assert.Contains("CustomEvent", exported, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("LatencyMs", exported, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("SqlQuery", exported, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task RecoveryAndFallbackPaths_ReturnExpectedResults()
	{
		var service = new ErrorReportingService(_logger)
		{
			SuppressUserDialogs = true
		};

		var recovered = await service.TryRecoverAsync(new InvalidOperationException("boom"), "Authentication", () => Task.FromResult(true));
		var notRecovered = await service.TryRecoverAsync(new InvalidOperationException("boom"), "NoStrategy", () => Task.FromResult(false));

		var fallbackResult = await service.HandleWithFallbackAsync(
			() => Task.FromException<string>(new InvalidOperationException("primary failed")),
			() => Task.FromResult("fallback"),
			context: "FallbackTest",
			defaultValue: "default");

		var defaultResult = await service.HandleWithFallbackAsync(
			() => Task.FromException<string>(new InvalidOperationException("primary failed")),
			() => Task.FromException<string>(new InvalidOperationException("fallback failed")),
			context: "FallbackDefault",
			defaultValue: "default");

		Assert.True(recovered);
		Assert.False(notRecovered);
		Assert.Equal("fallback", fallbackResult);
		Assert.Equal("default", defaultResult);
	}

	[Fact]
	public async Task IncrementCounter_AndSingleAttemptStrategy_BehaveAsExpected()
	{
		var service = new ErrorReportingService(_logger)
		{
			SuppressUserDialogs = true
		};

		Assert.Equal(1, service.IncrementCounter("sample"));
		Assert.Equal(2, service.IncrementCounter("sample"));

		var strategy = new SingleAttemptRecoveryStrategy();
		var success = await strategy.ExecuteAsync(() => Task.FromResult(true));
		var failure = await strategy.ExecuteAsync(() => Task.FromException<bool>(new InvalidOperationException("fail")));

		Assert.True(success);
		Assert.False(failure);
	}

	private sealed class FakeTelemetryService : ITelemetryService
	{
		public List<Exception> RecordedExceptions { get; } = new();

		void ITelemetryService.RecordException(Exception exception, params (string key, object? value)[] additionalTags)
		{
			_ = additionalTags;
			RecordedExceptions.Add(exception);
		}

		Activity? ITelemetryService.StartActivity(string operationName, params (string key, object? value)[] tags)
		{
			_ = RecordedExceptions;
			_ = operationName;
			_ = tags;
			return null!;
		}
	}
}