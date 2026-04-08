using System.Net;
using System.Text;
using System.Text.Json;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class WorkspaceBootstrapServiceTests
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	[Fact]
	public async Task LoadAsync_UsesLiveApiSnapshot_WhenApiResponseSucceeds()
	{
		var state = new WorkspaceState();
		var apiHandler = new StubHttpMessageHandler(_ => CreateJsonResponse(new WorkspaceBootstrapData(
			"Water Utility",
			2026,
			"Live API Scenario",
			55.25m,
			13250m,
			240m,
			DateTime.UtcNow.ToString("O"))));
		var fallbackHandler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Fallback should not be called when the API succeeds."));

		var service = new WorkspaceBootstrapService(
			new HttpClient(fallbackHandler) { BaseAddress = new Uri("https://workspace.local/") },
			"https://workspace.local/",
			state,
			new WorkspaceSnapshotApiService(new HttpClient(apiHandler) { BaseAddress = new Uri("https://workspace.local/") }));

		await service.LoadAsync("Water Utility", 2026);

		Assert.Equal("Water Utility", state.SelectedEnterprise);
		Assert.Equal(2026, state.SelectedFiscalYear);
		Assert.Equal("Live API Scenario", state.ActiveScenarioName);
		Assert.Equal(WorkspaceStartupSource.ApiSnapshot, state.StartupSource);
		Assert.Equal(WorkspaceStartupSource.ApiSnapshot, state.CurrentStateSource);
		Assert.False(state.IsUsingStartupFallback);
		Assert.False(state.IsUsingBrowserRestoredState);
		Assert.Contains("live workspace API snapshot", state.StartupSourceStatus);
		Assert.Equal(0, fallbackHandler.CallCount);
	}

	[Fact]
	public async Task LoadAsync_FallsBackToLocalBootstrap_WhenApiRequestFails()
	{
		var state = new WorkspaceState();
		var apiHandler = new StubHttpMessageHandler(_ => throw new HttpRequestException("API unavailable"));
		var fallbackHandler = new StubHttpMessageHandler(_ => CreateJsonResponse(new WorkspaceBootstrapData(
			"Fallback Utility",
			2025,
			"Offline Fallback Scenario",
			48.50m,
			11900m,
			225m,
			DateTime.UtcNow.ToString("O"))));

		var service = new WorkspaceBootstrapService(
			new HttpClient(fallbackHandler) { BaseAddress = new Uri("https://workspace.local/") },
			"https://workspace.local/",
			state,
			new WorkspaceSnapshotApiService(new HttpClient(apiHandler) { BaseAddress = new Uri("https://workspace.local/") }));

		await service.LoadAsync();

		Assert.Equal("Fallback Utility", state.SelectedEnterprise);
		Assert.Equal(2025, state.SelectedFiscalYear);
		Assert.Equal("Offline Fallback Scenario", state.ActiveScenarioName);
		Assert.Equal(WorkspaceStartupSource.LocalBootstrapFallback, state.StartupSource);
		Assert.Equal(WorkspaceStartupSource.LocalBootstrapFallback, state.CurrentStateSource);
		Assert.True(state.IsUsingStartupFallback);
		Assert.False(state.IsUsingBrowserRestoredState);
		Assert.Contains("local fallback data", state.StartupSourceStatus);
		Assert.Equal(1, fallbackHandler.CallCount);
	}

	private static HttpResponseMessage CreateJsonResponse<TValue>(TValue value)
	{
		return new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json")
		};
	}

	#pragma warning disable S1144
	private sealed class StubHttpMessageHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

		public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
		{
			this.responder = responder;
		}

		public int CallCount { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			_ = cancellationToken;
			CallCount++;
			return Task.FromResult(responder(request));
		}
	}
	#pragma warning restore S1144
}