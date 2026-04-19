using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace WileyCoWeb.Components;

public sealed class LoggingErrorBoundary : ErrorBoundary, IDisposable
{
    [Parameter]
    public string BoundaryName { get; set; } = "App";

    [Inject]
    private ILogger<LoggingErrorBoundary> Logger { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        NavigationManager.LocationChanged += HandleLocationChanged;
    }

    protected override async Task OnErrorAsync(Exception exception)
    {
        var boundaryName = ResolveBoundaryName();
        var uri = NavigationManager.Uri;

        using (CreateLoggingScope(boundaryName, uri))
        {
            Logger.LogError(exception, "Unhandled UI exception in {BoundaryName} at {Uri}", boundaryName, uri);
        }

        await TryLogBrowserConsoleErrorAsync(exception, boundaryName, uri).ConfigureAwait(false);
    }

    private string ResolveBoundaryName()
        => string.IsNullOrWhiteSpace(BoundaryName) ? nameof(LoggingErrorBoundary) : BoundaryName.Trim();

    private IDisposable CreateLoggingScope(string boundaryName, string uri)
    {
        return Logger.BeginScope(new Dictionary<string, object?>
        {
            ["BoundaryName"] = boundaryName,
            ["Uri"] = uri
        })!;
    }

    private async Task TryLogBrowserConsoleErrorAsync(Exception exception, string boundaryName, string uri)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("console.error", $"[{boundaryName}] Unhandled UI exception at {uri}", exception.ToString()).ConfigureAwait(false);
        }
        catch (Exception jsException) when (jsException is JSException or InvalidOperationException)
        {
            Logger.LogDebug(jsException, "Browser console logging failed for {BoundaryName}.", boundaryName);
        }
    }

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        _ = sender;
        _ = args;

        if (CurrentException is null)
        {
            return;
        }

        _ = InvokeAsync(Recover);
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= HandleLocationChanged;
    }
}