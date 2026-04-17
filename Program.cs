using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;
using Syncfusion.Licensing;
using WileyCoWeb.Components;
using WileyCoWeb.Services;
using WileyCoWeb.State;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
Console.WriteLine("[startup] WebAssembly host builder created.");
var startupDiagnostics = new List<(LogLevel Level, string Message, Exception? Exception)>();
var clientBaseAddress = builder.HostEnvironment.BaseAddress;
var syncfusionLicenseKeyFromConfig = NormalizeSyncfusionLicenseKey(builder.Configuration["SyncfusionLicenseKey"]);
var syncfusionLicenseKeyFromEnvironment = NormalizeSyncfusionLicenseKey(Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY"));
var syncfusionLicenseKey = syncfusionLicenseKeyFromConfig ?? syncfusionLicenseKeyFromEnvironment;
var syncfusionKeySource = syncfusionLicenseKeyFromConfig is not null
    ? "config:SyncfusionLicenseKey"
    : syncfusionLicenseKeyFromEnvironment is not null
        ? "env:SYNCFUSION_LICENSE_KEY"
        : "not-found";

if (string.IsNullOrWhiteSpace(syncfusionLicenseKey))
{
    syncfusionLicenseKey = await LoadSyncfusionLicenseKeyFromLocalSettingsAsync(clientBaseAddress, startupDiagnostics);
    if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
    {
        syncfusionKeySource = "local-settings-file";
    }
}

if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
{
    SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
}

// Prefer the deployed public settings file so Amplify runtime cutovers are driven by the artifact the browser actually downloads.
var localSettingsApiBaseAddress = await LoadWorkspaceApiBaseAddressFromLocalSettingsAsync(clientBaseAddress, startupDiagnostics);
var environmentApiBaseAddress = Environment.GetEnvironmentVariable("WILEY_WORKSPACE_API_BASE_ADDRESS");
var configDirectApiBaseAddress = builder.Configuration["WILEY_WORKSPACE_API_BASE_ADDRESS"];
var configNamedApiBaseAddress = builder.Configuration["WorkspaceApiBaseAddress"];
var apiBaseAddress = localSettingsApiBaseAddress
    ?? environmentApiBaseAddress
    ?? configDirectApiBaseAddress
    ?? configNamedApiBaseAddress;
var workspaceApiSource = !string.IsNullOrWhiteSpace(localSettingsApiBaseAddress)
    ? "local-settings-file"
    : !string.IsNullOrWhiteSpace(environmentApiBaseAddress)
        ? "env:WILEY_WORKSPACE_API_BASE_ADDRESS"
        : !string.IsNullOrWhiteSpace(configDirectApiBaseAddress)
            ? "config:WILEY_WORKSPACE_API_BASE_ADDRESS"
            : !string.IsNullOrWhiteSpace(configNamedApiBaseAddress)
                ? "config:WorkspaceApiBaseAddress"
                : GetDefaultWorkspaceApiSource(clientBaseAddress);
var resolvedApiBaseAddress = !string.IsNullOrWhiteSpace(apiBaseAddress) && Uri.TryCreate(apiBaseAddress, UriKind.Absolute, out var apiUri)
    ? apiUri
    : ResolveLocalApiBaseAddress(clientBaseAddress);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Migrations", LogLevel.Information);
builder.Logging.AddFilter("WileyWidget", LogLevel.Debug);
builder.Logging.AddFilter("WileyWidget.Data", LogLevel.Debug);
builder.Logging.AddFilter("WileyWidget.Services", LogLevel.Debug);
builder.Logging.AddFilter("WileyWidget.Business", LogLevel.Debug);

builder.Services.AddScoped(sp =>
{
    return new HttpClient { BaseAddress = resolvedApiBaseAddress };
});
builder.Services.AddSingleton<WorkspaceState>();
builder.Services.AddScoped<WorkspaceBootstrapService>();
builder.Services.AddScoped<WorkspacePersistenceService>();
builder.Services.AddScoped<WorkspaceSnapshotApiService>();
builder.Services.AddScoped<WorkspaceKnowledgeApiService>();
builder.Services.AddScoped<QuickBooksImportApiService>();
builder.Services.AddScoped<WorkspaceAiApiService>();
builder.Services.AddScoped<WorkspaceDocumentExportService>();
builder.Services.AddScoped<BrowserDownloadService>();

builder.Services.AddSyncfusionBlazor();

var host = builder.Build();
Console.WriteLine("[startup] WebAssembly host built.");
var startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WileyCoWeb.Startup");

foreach (var (level, message, exception) in startupDiagnostics)
{
    if (exception is null)
    {
        startupLogger.Log(level, message);
    }
    else
    {
        startupLogger.Log(level, exception, message);
    }
}

if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
{
    startupLogger.LogInformation("Syncfusion license key loaded from {SyncfusionKeySource}: PRESENT (length {LicenseKeyLength})", syncfusionKeySource, syncfusionLicenseKey.Length);
}
else
{
    startupLogger.LogWarning("Syncfusion license key was not found in configuration or the environment. Configure Amplify build secrets or set SYNCFUSION_LICENSE_KEY before starting the app.");
}

startupLogger.LogInformation(
    "WileyWidget.Client.StartupBaseline Environment={Environment} ClientBaseAddress={ClientBaseAddress} SyncfusionKeySource={SyncfusionKeySource} SyncfusionKeyPresent={SyncfusionKeyPresent} WorkspaceApiSource={WorkspaceApiSource} WorkspaceApiBaseAddress={WorkspaceApiBaseAddress}",
    builder.HostEnvironment.Environment,
    clientBaseAddress,
    syncfusionKeySource,
    !string.IsNullOrWhiteSpace(syncfusionLicenseKey),
    workspaceApiSource,
    resolvedApiBaseAddress);

var bootstrapStopwatch = Stopwatch.StartNew();
var bootstrapTask = host.Services.GetRequiredService<WorkspaceBootstrapService>().LoadAsync();
Console.WriteLine("[startup] Workspace bootstrap task started.");
_ = bootstrapTask.ContinueWith(task =>
{
    if (task.IsFaulted)
    {
        startupLogger.LogWarning(task.Exception, "Workspace bootstrap finished with a background error after {ElapsedMs}ms.", bootstrapStopwatch.ElapsedMilliseconds);
        return;
    }

    startupLogger.LogInformation("Workspace bootstrap finished in the background after {ElapsedMs}ms.", bootstrapStopwatch.ElapsedMilliseconds);
}, TaskContinuationOptions.ExecuteSynchronously);

await host.RunAsync();

static Uri ResolveLocalApiBaseAddress(string clientBaseAddress)
{
    var clientUri = new Uri(clientBaseAddress);

    if (clientUri.IsLoopback)
    {
        return new UriBuilder(clientUri)
        {
            Port = clientUri.Port + 1
        }.Uri;
    }

    return clientUri;
}

static string GetDefaultWorkspaceApiSource(string clientBaseAddress)
{
    var clientUri = new Uri(clientBaseAddress);
    return clientUri.IsLoopback
        ? "default:loopback-port+1"
        : "default:same-origin";
}

static string? NormalizeSyncfusionLicenseKey(string? rawLicenseKey)
{
    if (string.IsNullOrWhiteSpace(rawLicenseKey))
    {
        return null;
    }

    var normalizedLicenseKey = rawLicenseKey.Trim();

    if (normalizedLicenseKey.Length >= 2
        && normalizedLicenseKey.StartsWith('"')
        && normalizedLicenseKey.EndsWith('"'))
    {
        normalizedLicenseKey = normalizedLicenseKey[1..^1].Trim();
    }

    return string.IsNullOrWhiteSpace(normalizedLicenseKey)
        ? null
        : normalizedLicenseKey;
}

async Task<string?> LoadSyncfusionLicenseKeyFromLocalSettingsAsync(string baseAddress, IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
{
    try
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };
        var localSettingsJson = await httpClient.GetStringAsync("appsettings.Syncfusion.local.json");
        using var document = JsonDocument.Parse(localSettingsJson);

        if (!document.RootElement.TryGetProperty("SyncfusionLicenseKey", out var licenseKeyElement))
        {
            startupDiagnostics.Add((
                LogLevel.Warning,
                "appsettings.Syncfusion.local.json was loaded but did not contain SyncfusionLicenseKey. The client will continue with environment/config fallback.",
                null));
            return null;
        }

        var normalizedLicenseKey = NormalizeSyncfusionLicenseKey(licenseKeyElement.GetString());
        if (string.IsNullOrWhiteSpace(normalizedLicenseKey))
        {
            startupDiagnostics.Add((
                LogLevel.Warning,
                "appsettings.Syncfusion.local.json contained SyncfusionLicenseKey, but the value was empty after normalization. The client will continue with environment/config fallback.",
                null));
            return null;
        }

        return normalizedLicenseKey;
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        startupDiagnostics.Add((
            LogLevel.Information,
            "appsettings.Syncfusion.local.json was not found. The client will continue with environment/config fallback.",
            null));
        return null;
    }
    catch (JsonException ex)
    {
        startupDiagnostics.Add((
            LogLevel.Warning,
            "appsettings.Syncfusion.local.json could not be parsed. The client will continue with environment/config fallback.",
            ex));
        return null;
    }
    catch (Exception ex)
    {
        startupDiagnostics.Add((
            LogLevel.Warning,
            "appsettings.Syncfusion.local.json could not be loaded. The client will continue with environment/config fallback.",
            ex));
        return null;
    }
}

async Task<string?> LoadWorkspaceApiBaseAddressFromLocalSettingsAsync(string baseAddress, IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
{
    try
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };
        var localSettingsJson = await httpClient.GetStringAsync("appsettings.Workspace.local.json");
        using var document = JsonDocument.Parse(localSettingsJson);

        if (!document.RootElement.TryGetProperty("WorkspaceApiBaseAddress", out var apiBaseAddressElement))
        {
            startupDiagnostics.Add((
                LogLevel.Warning,
                "appsettings.Workspace.local.json was loaded but did not contain WorkspaceApiBaseAddress. The client will continue with environment/config/default fallback.",
                null));
            return null;
        }

        var apiBaseAddress = apiBaseAddressElement.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(apiBaseAddress))
        {
            startupDiagnostics.Add((
                LogLevel.Warning,
                "appsettings.Workspace.local.json contained WorkspaceApiBaseAddress, but the value was empty. The client will continue with environment/config/default fallback.",
                null));
            return null;
        }

        if (!Uri.TryCreate(apiBaseAddress, UriKind.Absolute, out _))
        {
            startupDiagnostics.Add((
                LogLevel.Warning,
                $"appsettings.Workspace.local.json contained a non-absolute WorkspaceApiBaseAddress '{apiBaseAddress}'. The client will continue with environment/config/default fallback.",
                null));
            return null;
        }

        return apiBaseAddress;
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        startupDiagnostics.Add((
            LogLevel.Information,
            "appsettings.Workspace.local.json was not found. The client will continue with environment/config/default fallback.",
            null));
        return null;
    }
    catch (JsonException ex)
    {
        startupDiagnostics.Add((
            LogLevel.Warning,
            "appsettings.Workspace.local.json could not be parsed. The client will continue with environment/config/default fallback.",
            ex));
        return null;
    }
    catch (Exception ex)
    {
        startupDiagnostics.Add((
            LogLevel.Warning,
            "appsettings.Workspace.local.json could not be loaded. The client will continue with environment/config/default fallback.",
            ex));
        return null;
    }
}
