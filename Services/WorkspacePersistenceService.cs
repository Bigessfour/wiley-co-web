using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WileyCoWeb.Contracts;
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
    private readonly ILogger<WorkspacePersistenceService>? logger;
    private bool initialized;
    private bool suppressSave;

    public WorkspacePersistenceService(IJSRuntime jsRuntime, WorkspaceState workspaceState, ILogger<WorkspacePersistenceService>? logger = null)
    {
        this.jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        this.workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        this.logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            logger?.LogDebug("Workspace persistence initialize skipped because it was already initialized.");
            return;
        }

        initialized = true;
        Console.WriteLine("[startup] WorkspacePersistenceService.InitializeAsync entered.");
        logger?.LogInformation("Workspace persistence initialization started.");

        try
        {
            var persistedJson = await jsRuntime.InvokeAsync<string?>("wileyWorkspaceStorage.getItem", cancellationToken, StorageKey);
            if (!string.IsNullOrWhiteSpace(persistedJson))
            {
                try
                {
                    var persistedState = JsonSerializer.Deserialize<WorkspaceBootstrapData>(persistedJson, JsonOptions);
                    if (persistedState != null)
                    {
                        suppressSave = true;
                        workspaceState.ApplyBootstrap(persistedState);
                        suppressSave = false;
                        workspaceState.SetCurrentStateSource(WorkspaceStartupSource.BrowserStorageRestore, "Current workspace state was restored from browser storage.");
                        logger?.LogInformation("Workspace persistence restored state from browser storage for {Enterprise} FY {FiscalYear}.", persistedState.SelectedEnterprise, persistedState.SelectedFiscalYear);
                    }
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException or AggregateException)
                {
                    logger?.LogWarning(ex, "Workspace persistence ignored corrupt browser storage and will start from the bootstrap state instead.");
                    try
                    {
                        await jsRuntime.InvokeVoidAsync("wileyWorkspaceStorage.removeItem", cancellationToken, StorageKey);
                    }
                    catch (Exception removeEx)
                    {
                        logger?.LogWarning(removeEx, "Workspace persistence could not clear corrupt browser storage.");
                    }

                    workspaceState.SetCurrentStateSource(workspaceState.StartupSource, workspaceState.StartupSourceStatus);
                }
            }
            else
            {
                workspaceState.SetCurrentStateSource(workspaceState.StartupSource, workspaceState.StartupSourceStatus);
                logger?.LogInformation("Workspace persistence found no stored browser state; using startup source {Source}.", workspaceState.StartupSource);
            }
        }
        catch (Exception ex) when (ex is JSException or JsonException or InvalidOperationException or AggregateException)
        {
            logger?.LogWarning(ex, "Workspace persistence could not access browser storage; continuing with startup source {Source}.", workspaceState.StartupSource);
            workspaceState.SetCurrentStateSource(workspaceState.StartupSource, workspaceState.StartupSourceStatus);
        }

        workspaceState.Changed += HandleWorkspaceChanged;
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is JSException or JsonException or InvalidOperationException or AggregateException)
        {
            logger?.LogWarning(ex, "Workspace persistence could not save browser storage during initialization.");
        }
        Console.WriteLine("[startup] WorkspacePersistenceService.InitializeAsync completed.");
        logger?.LogInformation("Workspace persistence initialization completed.");
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!initialized || suppressSave)
        {
            logger?.LogDebug("Workspace persistence save skipped (initialized={Initialized}, suppressSave={SuppressSave}).", initialized, suppressSave);
            return;
        }

        try
        {
            var payload = JsonSerializer.Serialize(workspaceState.ToBootstrapData(), JsonOptions);
            await jsRuntime.InvokeVoidAsync("wileyWorkspaceStorage.setItem", cancellationToken, StorageKey, payload);
            logger?.LogDebug("Workspace state saved to browser storage for {Enterprise} FY {FiscalYear}.", workspaceState.SelectedEnterprise, workspaceState.SelectedFiscalYear);
        }
        catch (Exception ex) when (ex is JSException or JsonException or InvalidOperationException or AggregateException)
        {
            logger?.LogWarning(ex, "Workspace persistence save failed.");
        }
    }

    private void HandleWorkspaceChanged()
    {
        _ = SaveAsync();
    }

    public async Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        await jsRuntime.InvokeVoidAsync("wileyWorkspaceStorage.removeItem", cancellationToken, StorageKey);
        logger?.LogInformation("Workspace state removed from browser storage.");
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