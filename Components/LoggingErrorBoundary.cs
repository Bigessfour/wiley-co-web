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
        var boundaryName = string.IsNullOrWhiteSpace(BoundaryName) ? nameof(LoggingErrorBoundary) : BoundaryName.Trim();
        var uri = NavigationManager.Uri;

        using (Logger.BeginScope(new Dictionary<string, object?>
        {
            ["BoundaryName"] = boundaryName,
            ["Uri"] = uri
        }))
        {
            Logger.LogError(exception, "Unhandled UI exception in {BoundaryName} at {Uri}", boundaryName, uri);
        }

        try
        {
            await JSRuntime.InvokeVoidAsync("console.error", $"[{boundaryName}] Unhandled UI exception at {uri}", exception.ToString());
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