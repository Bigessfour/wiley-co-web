using System.Text.Json;
using Microsoft.JSInterop;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class WorkspacePersistenceServiceTests
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	[Fact]
	public async Task InitializeAsync_RestoresBrowserState_AndMarksCurrentStateSource()
	{
		var persistedState = WorkspaceTestData.CreateBrowserRestoredBootstrap();
		var jsRuntime = new StubJsRuntime()
			.WithGetItemResult("wileyWorkspaceStorage.getItem", JsonSerializer.Serialize(persistedState, JsonOptions));
		var state = new WorkspaceState();
		state.SetStartupSource(WorkspaceStartupSource.ApiSnapshot, "Workspace started from the live workspace API snapshot.");

		await using var service = new WorkspacePersistenceService(jsRuntime, state);
		await service.InitializeAsync();

		Assert.Equal(WorkspaceTestData.SanitationUtility, state.SelectedEnterprise);
		Assert.Equal(WorkspaceStartupSource.ApiSnapshot, state.StartupSource);
		Assert.Equal(WorkspaceStartupSource.BrowserStorageRestore, state.CurrentStateSource);
		Assert.True(state.IsUsingBrowserRestoredState);
		Assert.Contains("browser storage", state.CurrentStateSourceStatus);
	}

	[Fact]
	public async Task InitializeAsync_WithoutPersistedState_KeepsStartupSourceAsCurrentState()
	{
		var jsRuntime = new StubJsRuntime();
		var state = new WorkspaceState();
		state.SetStartupSource(WorkspaceStartupSource.LocalBootstrapFallback, "Workspace started from local fallback data because the workspace API was unavailable.");

		await using var service = new WorkspacePersistenceService(jsRuntime, state);
		await service.InitializeAsync();

		Assert.Equal(WorkspaceStartupSource.LocalBootstrapFallback, state.StartupSource);
		Assert.Equal(WorkspaceStartupSource.LocalBootstrapFallback, state.CurrentStateSource);
		Assert.False(state.IsUsingBrowserRestoredState);
		Assert.Contains("local fallback data", state.CurrentStateSourceStatus);
	}

	private sealed class StubJsRuntime : IJSRuntime
	{
		private readonly Dictionary<string, object?> values = new(StringComparer.Ordinal);

		public StubJsRuntime WithGetItemResult(string identifier, string? value)
		{
			values[$"get:{identifier}"] = value;
			return this;
		}

		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
		{
			_ = values.Count;
			return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
		}

		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
		{
			_ = cancellationToken;
			if (identifier == "wileyWorkspaceStorage.getItem")
			{
				values.TryGetValue($"get:{identifier}", out var storedValue);
				return ValueTask.FromResult((TValue)(storedValue is null ? default! : storedValue));
			}

			if (identifier == "wileyWorkspaceStorage.setItem")
			{
				values[$"set:{args?[0]}"] = args?[1];
				return ValueTask.FromResult(default(TValue)!);
			}

			if (identifier == "wileyWorkspaceStorage.removeItem")
			{
				values.Remove($"set:{args?[0]}");
				return ValueTask.FromResult(default(TValue)!);
			}

			throw new NotSupportedException($"Unsupported JS identifier '{identifier}'.");
		}
	}
}