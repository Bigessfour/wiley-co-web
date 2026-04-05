using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using WileyCoWeb.State;

namespace WileyCoWeb.Services;

public sealed class WorkspacePersistenceService : IAsyncDisposable, IDisposable
{
    private const string StorageKey = "wiley.workspace.state.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IJSRuntime jsRuntime;
    private readonly WorkspaceState workspaceState;
    private bool initialized;
    private bool suppressSave;

    public WorkspacePersistenceService(IJSRuntime jsRuntime, WorkspaceState workspaceState)
    {
        this.jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        this.workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        var persistedJson = await jsRuntime.InvokeAsync<string?>("wileyWorkspaceStorage.getItem", cancellationToken, StorageKey);
        if (!string.IsNullOrWhiteSpace(persistedJson))
        {
            var persistedState = JsonSerializer.Deserialize<WorkspaceBootstrapData>(persistedJson, JsonOptions);
            if (persistedState != null)
            {
                suppressSave = true;
                workspaceState.ApplyBootstrap(persistedState);
                suppressSave = false;
            }
        }

        workspaceState.Changed += HandleWorkspaceChanged;
        await SaveAsync(cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!initialized || suppressSave)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(workspaceState.ToBootstrapData(), JsonOptions);
        await jsRuntime.InvokeVoidAsync("wileyWorkspaceStorage.setItem", cancellationToken, StorageKey, payload);
    }

    private void HandleWorkspaceChanged()
    {
        _ = SaveAsync();
    }

    public async Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        await jsRuntime.InvokeVoidAsync("wileyWorkspaceStorage.removeItem", cancellationToken, StorageKey);
    }

    public void Dispose()
    {
        workspaceState.Changed -= HandleWorkspaceChanged;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}