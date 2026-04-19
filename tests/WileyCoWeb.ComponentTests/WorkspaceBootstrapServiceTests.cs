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
		var apiHandler = new StubHttpMessageHandler(_ => CreateJsonResponse(WorkspaceTestData.CreateWaterUtilityBootstrap(
			WorkspaceTestData.LiveApiScenario,
			WorkspaceTestData.WaterCurrentRate,
			WorkspaceTestData.WaterTotalCosts,
			WorkspaceTestData.WaterProjectedVolume,
			DateTime.UtcNow.ToString("O"))));
		var fallbackHandler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Fallback should not be called when the API succeeds."));

		var service = new WorkspaceBootstrapService(
			state,
			new WorkspaceSnapshotApiService(new HttpClient(apiHandler) { BaseAddress = new Uri("https://workspace.local/") }),
			new WorkspaceLocalBootstrapService(new HttpClient(fallbackHandler) { BaseAddress = new Uri("https://client.local/") }));

		await service.LoadAsync(WorkspaceTestData.WaterUtility, WorkspaceTestData.WaterFiscalYear);

		Assert.Equal(WorkspaceTestData.WaterUtility, state.SelectedEnterprise);
		Assert.Equal(WorkspaceTestData.WaterFiscalYear, state.SelectedFiscalYear);
		Assert.Equal(WorkspaceTestData.LiveApiScenario, state.ActiveScenarioName);
		Assert.Equal(WorkspaceStartupSource.ApiSnapshot, state.StartupSource);
		Assert.Equal(WorkspaceStartupSource.ApiSnapshot, state.CurrentStateSource);
		Assert.False(state.IsUsingStartupFallback);
		Assert.False(state.IsUsingBrowserRestoredState);
		Assert.Contains("live workspace API snapshot", state.StartupSourceStatus);
		Assert.Equal(0, fallbackHandler.CallCount);
	}

	[Fact]
	public async Task LoadAsync_UsesLocalBootstrapAsset_WhenApiRequestFails()
	{
		var state = new WorkspaceState();
		var apiHandler = new StubHttpMessageHandler(_ => throw new HttpRequestException("API unavailable"));
		var localBootstrapHandler = new StubHttpMessageHandler(request =>
		{
			Assert.EndsWith("/data/workspace-bootstrap.json", request.RequestUri?.AbsolutePath, StringComparison.OrdinalIgnoreCase);
			return CreateJsonResponse(WorkspaceTestData.CreateWaterUtilityBootstrap(
				WorkspaceTestData.BasePlanningScenario,
				WorkspaceTestData.WaterCurrentRate,
				WorkspaceTestData.WaterTotalCosts,
				WorkspaceTestData.WaterProjectedVolume,
				DateTime.UtcNow.ToString("O")));
		});

		var service = new WorkspaceBootstrapService(
			state,
			new WorkspaceSnapshotApiService(new HttpClient(apiHandler) { BaseAddress = new Uri("https://workspace.local/") }),
			new WorkspaceLocalBootstrapService(new HttpClient(localBootstrapHandler) { BaseAddress = new Uri("https://client.local/") }));

		await service.LoadAsync();

		Assert.Equal(WorkspaceStartupSource.LocalBootstrapFallback, state.StartupSource);
		Assert.Equal(WorkspaceTestData.WaterUtility, state.SelectedEnterprise);
		Assert.Equal(WorkspaceTestData.WaterFiscalYear, state.SelectedFiscalYear);
		Assert.Equal(WorkspaceTestData.BasePlanningScenario, state.ActiveScenarioName);
		Assert.NotEmpty(state.ScenarioItems);
		Assert.True(state.IsUsingStartupFallback);
		Assert.Contains("local fallback data", state.StartupSourceStatus, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(1, localBootstrapHandler.CallCount);
	}

	[Fact]
	public async Task LoadAsync_FallsBack_WhenApiRequestFails()
	{
		var state = new WorkspaceState();
		var apiHandler = new StubHttpMessageHandler(_ => throw new HttpRequestException("API unavailable"));

		var service = new WorkspaceBootstrapService(
			state,
			new WorkspaceSnapshotApiService(new HttpClient(apiHandler) { BaseAddress = new Uri("https://workspace.local/") }),
			new WorkspaceLocalBootstrapService(new HttpClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("local bootstrap unavailable"))) { BaseAddress = new Uri("https://client.local/") }));

		await service.LoadAsync();

		Assert.Equal(WorkspaceStartupSource.LocalBootstrapFallback, state.StartupSource);
		Assert.Equal(WorkspaceStartupSource.LocalBootstrapFallback, state.CurrentStateSource);
		Assert.True(state.IsUsingStartupFallback);
		Assert.Empty(state.ScenarioItems);
		Assert.Contains("local fallback data", state.StartupSourceStatus, StringComparison.OrdinalIgnoreCase);
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