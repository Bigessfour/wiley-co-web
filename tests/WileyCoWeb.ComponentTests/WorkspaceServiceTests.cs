using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class WorkspaceServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task WorkspacePersistenceService_LoadsPersistedState_AndSavesChanges()
    {
        var runtime = new FakeJsRuntime();
        var persisted = new WorkspaceBootstrapData(
            "Trash",
            2025,
            "Persisted scenario",
            33m,
            123000m,
            5800m,
            "2026-04-05T12:00:00.0000000Z");

        runtime.Storage["wiley.workspace.state.v1"] = JsonSerializer.Serialize(persisted, JsonOptions);

        var state = new WorkspaceState();
        await using var service = new WorkspacePersistenceService(runtime, state);

        await service.InitializeAsync();

        Assert.Equal("Trash", state.SelectedEnterprise);
        Assert.Equal(2025, state.SelectedFiscalYear);
        Assert.Equal("Persisted scenario", state.ActiveScenarioName);
        Assert.Contains(runtime.Calls, call => call.Identifier == "wileyWorkspaceStorage.getItem");
        Assert.Contains(runtime.Calls, call => call.Identifier == "wileyWorkspaceStorage.setItem");

        state.SetSelection("Sewer", 2026);
        await service.SaveAsync();

        Assert.Contains("Sewer", runtime.Storage["wiley.workspace.state.v1"]!, StringComparison.OrdinalIgnoreCase);

        await service.RemoveAsync();
        Assert.False(runtime.Storage.ContainsKey("wiley.workspace.state.v1"));
    }

    [Fact]
    public async Task WorkspacePersistenceService_IgnoresSaveRequests_BeforeInitialization()
    {
        var runtime = new FakeJsRuntime();
        var state = new WorkspaceState();
        await using var service = new WorkspacePersistenceService(runtime, state);

        await service.SaveAsync();

        Assert.Empty(runtime.Calls);
        Assert.Empty(runtime.Storage);
    }

    [Fact]
    public async Task WorkspacePersistenceService_InitializeAsync_IsIdempotent()
    {
        var runtime = new FakeJsRuntime();
        var state = new WorkspaceState();
        await using var service = new WorkspacePersistenceService(runtime, state);

        await service.InitializeAsync();
        await service.InitializeAsync();

        Assert.Single(runtime.Calls, call => call.Identifier == "wileyWorkspaceStorage.getItem");
        Assert.Single(runtime.Calls, call => call.Identifier == "wileyWorkspaceStorage.setItem");
    }

    [Fact]
    public async Task WorkspaceBootstrapService_UsesApiSnapshot_WhenAvailable()
    {
        var state = new WorkspaceState();
        var apiBootstrap = new WorkspaceBootstrapData(
            "Water Utility",
            2026,
            "API snapshot",
            31.25m,
            98000m,
            4500m,
            "2026-04-05T12:00:00.0000000Z");

        var client = new HttpClient(new RoutedHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/api/workspace/snapshot", StringComparison.OrdinalIgnoreCase) == true)
            {
                return JsonResponse(apiBootstrap);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var snapshotService = new WorkspaceSnapshotApiService(client);
        var service = new WorkspaceBootstrapService(client, state, snapshotService);

        await service.LoadAsync();

        Assert.Equal("Water Utility", state.SelectedEnterprise);
        Assert.Equal(2026, state.SelectedFiscalYear);
        Assert.Equal("API snapshot", state.ActiveScenarioName);
        Assert.Equal(31.25m, state.CurrentRate);
        Assert.Equal(98000m, state.TotalCosts);
        Assert.Equal(4500m, state.ProjectedVolume);
    }

    [Fact]
    public async Task WorkspaceBootstrapService_TargetedLoad_UsesEnterpriseAndFiscalYearQuery()
    {
        var state = new WorkspaceState();
        string? requestedUri = null;
        var apiBootstrap = new WorkspaceBootstrapData(
            "Water Utility",
            2026,
            "Targeted API snapshot",
            31.25m,
            98000m,
            4500m,
            "2026-04-05T12:00:00.0000000Z");

        var client = new HttpClient(new RoutedHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri?.OriginalString;

            if (request.RequestUri?.AbsolutePath.EndsWith("/api/workspace/snapshot", StringComparison.OrdinalIgnoreCase) == true)
            {
                return JsonResponse(apiBootstrap);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var snapshotService = new WorkspaceSnapshotApiService(client);
        var service = new WorkspaceBootstrapService(client, state, snapshotService);

        await service.LoadAsync("Water Utility", 2026);

        Assert.Contains("/api/workspace/snapshot?", requestedUri);
        Assert.Contains("enterprise=Water%20Utility", requestedUri);
        Assert.Contains("fiscalYear=2026", requestedUri);
        Assert.Equal("Water Utility", state.SelectedEnterprise);
        Assert.Equal(2026, state.SelectedFiscalYear);
    }

    [Fact]
    public async Task WorkspaceBootstrapService_FallsBackToStaticBootstrap_WhenApiFails()
    {
        var state = new WorkspaceState();
        var staticBootstrap = new WorkspaceBootstrapData(
            "Trash Utility",
            2025,
            "Static bootstrap",
            21.50m,
            72000m,
            3200m,
            "2026-04-05T12:00:00.0000000Z");

        var client = new HttpClient(new RoutedHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/api/workspace/snapshot", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (request.RequestUri?.AbsolutePath.EndsWith("/data/workspace-bootstrap.json", StringComparison.OrdinalIgnoreCase) == true)
            {
                return JsonResponse(staticBootstrap);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var snapshotService = new WorkspaceSnapshotApiService(client);
        var service = new WorkspaceBootstrapService(client, state, snapshotService);

        await service.LoadAsync();

        Assert.Equal("Trash Utility", state.SelectedEnterprise);
        Assert.Equal(2025, state.SelectedFiscalYear);
        Assert.Equal("Static bootstrap", state.ActiveScenarioName);
    }

    [Fact]
    public async Task WorkspaceBootstrapService_ThrowsWhenBothSourcesAreUnavailable()
    {
        var state = new WorkspaceState();
        var client = new HttpClient(new RoutedHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null")
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var snapshotService = new WorkspaceSnapshotApiService(client);
        var service = new WorkspaceBootstrapService(client, state, snapshotService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoadAsync());

        Assert.Contains("workspace snapshot response was empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WorkspaceSnapshotApiService_PostsSnapshot_AndParsesResponse()
    {
        object? capturedPayload = null;
        HttpMethod? capturedMethod = null;
        var client = new HttpClient(new RoutedHttpMessageHandler(async request =>
        {
            capturedMethod = request.Method;
            capturedPayload = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new WorkspaceSnapshotSaveResponse(42, "Water FY2026 rate snapshot", "2026-04-05T12:00:00Z"), JsonOptions), Encoding.UTF8, "application/json")
            };
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var service = new WorkspaceSnapshotApiService(client);
        var response = await service.SaveRateSnapshotAsync(new { Enterprise = "Water Utility", FiscalYear = 2026 });

        Assert.Equal(HttpMethod.Post, capturedMethod);
        Assert.Contains("Water Utility", capturedPayload!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(42, response.SnapshotId);
        Assert.Equal("Water FY2026 rate snapshot", response.SnapshotName);
    }

    [Fact]
    public async Task WorkspaceSnapshotApiService_ThrowsForFailureStatus()
    {
        var client = new HttpClient(new RoutedHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("snapshot rejected")
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var service = new WorkspaceSnapshotApiService(client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveRateSnapshotAsync(new { Enterprise = "Water Utility" }));

        Assert.Contains("400", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("snapshot rejected", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WorkspaceSnapshotApiService_ThrowsWhenSaveResponseIsEmpty()
    {
        var client = new HttpClient(new RoutedHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null")
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var service = new WorkspaceSnapshotApiService(client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveRateSnapshotAsync(new { Enterprise = "Water Utility" }));

        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WorkspaceSnapshotApiService_ThrowsForNullSnapshot()
    {
        var service = new WorkspaceSnapshotApiService(new HttpClient(new RoutedHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveRateSnapshotAsync(null!));
    }

    private static HttpResponseMessage JsonResponse<T>(T payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };
    }

    #pragma warning disable
    private sealed class FakeJsRuntime : IJSRuntime
    {
        public Dictionary<string, string?> Storage { get; } = new(StringComparer.Ordinal);

        public List<(string Identifier, object?[] Arguments)> Calls { get; } = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var arguments = args ?? Array.Empty<object?>();
            Calls.Add((identifier, arguments));

            object? result = identifier switch
            {
                "wileyWorkspaceStorage.getItem" => Storage.TryGetValue(arguments[0]?.ToString() ?? string.Empty, out var value) ? value : null,
                "wileyWorkspaceStorage.setItem" => StoreValue(arguments),
                "wileyWorkspaceStorage.removeItem" => RemoveValue(arguments),
                _ => null
            };

            return new ValueTask<TValue>(result is null ? default! : (TValue)result);
        }

        private object? StoreValue(object?[] arguments)
        {
            Storage[arguments[0]?.ToString() ?? string.Empty] = arguments[1]?.ToString();
            return null;
        }

        private object? RemoveValue(object?[] arguments)
        {
            Storage.Remove(arguments[0]?.ToString() ?? string.Empty);
            return null;
        }
    }

    #pragma warning restore

    #pragma warning disable
    private sealed class RoutedHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

        public RoutedHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            : this(request => Task.FromResult(responder(request)))
        {
        }

        public RoutedHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _responder(request);
        }
    }
    #pragma warning restore
}
