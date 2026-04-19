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
        LogInitializationStarted();
        await InitializeWorkspacePersistenceAsync(cancellationToken);
        LogInitializationCompleted();
    }

    private async Task InitializeWorkspacePersistenceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RestorePersistedStateAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is JSException or JsonException or InvalidOperationException or AggregateException)
        {
            HandleInitializationError(ex);
        }

        SetupChangeHandler();
        await SaveInitialStateAsync(cancellationToken);
    }

    private void LogInitializationStarted()
    {
        Console.WriteLine("[startup] WorkspacePersistenceService.InitializeAsync entered.");
        logger?.LogInformation("Workspace persistence initialization started.");
    }

    private void LogInitializationCompleted()
    {
        Console.WriteLine("[startup] WorkspacePersistenceService.InitializeAsync completed.");
        logger?.LogInformation("Workspace persistence initialization completed.");
    }

    private async Task RestorePersistedStateAsync(CancellationToken cancellationToken)
    {
        var persistedJson = await jsRuntime.InvokeAsync<string?>("wileyWorkspaceStorage.getItem", cancellationToken, StorageKey);
        if (string.IsNullOrWhiteSpace(persistedJson))
        {
            SetDefaultStateSource();
            return;
        }

        await ApplyPersistedStateAsync(persistedJson, cancellationToken);
    }

    private async Task ApplyPersistedStateAsync(string persistedJson, CancellationToken cancellationToken)
    {
        try
        {
            var persistedState = DeserializePersistedState(persistedJson);
            if (persistedState is null)
            {
                SetDefaultStateSource();
                return;
            }

            await ApplyPersistedWorkspaceStateAsync(persistedState).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsPersistenceStateException(ex))
        {
            await HandleCorruptStorageAsync(ex, cancellationToken);
        }
    }

    private async Task HandleCorruptStorageAsync(Exception ex, CancellationToken cancellationToken)
    {
        logger?.LogWarning(ex, "Workspace persistence ignored corrupt browser storage and will start from the bootstrap state instead.");
        await TryRemoveCorruptStorageAsync(cancellationToken);
        ResetWorkspaceStateSource();
    }

    private async Task TryRemoveCorruptStorageAsync(CancellationToken cancellationToken)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("wileyWorkspaceStorage.removeItem", cancellationToken, StorageKey);
        }
        catch (Exception removeEx)
        {
            logger?.LogWarning(removeEx, "Workspace persistence could not clear corrupt browser storage.");
        }
    }

    private void SetDefaultStateSource()
    {
        ResetWorkspaceStateSource();
        logger?.LogInformation("Workspace persistence found no stored browser state; using startup source {Source}.", workspaceState.StartupSource);
    }

    private void HandleInitializationError(Exception ex)
    {
        logger?.LogWarning(ex, "Workspace persistence could not access browser storage; continuing with startup source {Source}.", workspaceState.StartupSource);
        ResetWorkspaceStateSource();
    }

    private void SetupChangeHandler()
    {
        workspaceState.Changed += HandleWorkspaceChanged;
    }

    private async Task SaveInitialStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is JSException or JsonException or InvalidOperationException or AggregateException)
        {
            logger?.LogWarning(ex, "Workspace persistence could not save browser storage during initialization.");
        }
    }

    private void ResetWorkspaceStateSource()
    {
        workspaceState.SetCurrentStateSource(workspaceState.StartupSource, workspaceState.StartupSourceStatus);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => SaveAsyncCore(cancellationToken);

    private Task SaveAsyncCore(CancellationToken cancellationToken)
        => TrySaveWorkspaceStateAsync(cancellationToken);

    private async Task TrySaveWorkspaceStateAsync(CancellationToken cancellationToken)
    {
        if (!CanSaveWorkspaceState())
        {
            LogWorkspaceSaveSkipped();
            return;
        }

        try
        {
            await PersistWorkspaceStateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsPersistenceStateException(ex))
        {
            LogWorkspaceSaveFailure(ex);
        }
    }

    private bool CanSaveWorkspaceState()
    {
        return initialized && !suppressSave;
    }

    private async Task PersistWorkspaceStateAsync(CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(workspaceState.ToBootstrapData(), JsonOptions);
        await jsRuntime.InvokeVoidAsync("wileyWorkspaceStorage.setItem", cancellationToken, StorageKey, payload).ConfigureAwait(false);
        logger?.LogDebug("Workspace state saved to browser storage for {Enterprise} FY {FiscalYear}.", workspaceState.SelectedEnterprise, workspaceState.SelectedFiscalYear);
    }

    private void LogWorkspaceSaveSkipped()
    {
        logger?.LogDebug("Workspace persistence save skipped (initialized={Initialized}, suppressSave={SuppressSave}).", initialized, suppressSave);
    }

    private void LogWorkspaceSaveFailure(Exception ex)
    {
        logger?.LogWarning(ex, "Workspace persistence save failed.");
    }

    private async Task ApplyPersistedWorkspaceStateAsync(WorkspaceBootstrapData persistedState)
    {
        suppressSave = true;
        workspaceState.ApplyBootstrap(persistedState);
        suppressSave = false;
        workspaceState.SetCurrentStateSource(WorkspaceStartupSource.BrowserStorageRestore, "Current workspace state was restored from browser storage.");
        logger?.LogInformation("Workspace persistence restored state from browser storage for {Enterprise} FY {FiscalYear}.", persistedState.SelectedEnterprise, persistedState.SelectedFiscalYear);
        await Task.CompletedTask;
    }

    private static WorkspaceBootstrapData? DeserializePersistedState(string persistedJson)
        => JsonSerializer.Deserialize<WorkspaceBootstrapData>(persistedJson, JsonOptions);

    private static bool IsPersistenceStateException(Exception ex)
        => ex is JSException or JsonException or InvalidOperationException or AggregateException;

    private void HandleWorkspaceChanged() => _ = SaveAsync();

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