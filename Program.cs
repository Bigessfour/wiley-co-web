using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;
using Syncfusion.Licensing;
using WileyCoWeb.Components;
using WileyCoWeb.Services;
using WileyCoWeb.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
Console.WriteLine("[startup] WebAssembly host builder created.");
var syncfusionLicenseKey = builder.Configuration["SyncfusionLicenseKey"]
    ?? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
{
    SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
}

var apiBaseAddress = Environment.GetEnvironmentVariable("WILEY_WORKSPACE_API_BASE_ADDRESS");
var resolvedApiBaseAddress = !string.IsNullOrWhiteSpace(apiBaseAddress) && Uri.TryCreate(apiBaseAddress, UriKind.Absolute, out var apiUri)
    ? apiUri
    : ResolveLocalApiBaseAddress(builder.HostEnvironment.BaseAddress);

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
builder.Services.AddScoped(_ => builder.HostEnvironment.BaseAddress);
builder.Services.AddSingleton<WorkspaceState>();
builder.Services.AddScoped<WorkspaceBootstrapService>();
builder.Services.AddScoped<WorkspacePersistenceService>();
builder.Services.AddScoped<WorkspaceSnapshotApiService>();
builder.Services.AddScoped<QuickBooksImportApiService>();
builder.Services.AddScoped<WorkspaceAiApiService>();
builder.Services.AddScoped<WorkspaceDocumentExportService>();
builder.Services.AddScoped<BrowserDownloadService>();

builder.Services.AddSyncfusionBlazor();

var host = builder.Build();
Console.WriteLine("[startup] WebAssembly host built.");
var startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WileyCoWeb.Startup");

if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
{
    startupLogger.LogInformation("Syncfusion license key loaded: PRESENT (length {LicenseKeyLength})", syncfusionLicenseKey.Length);
}
else
{
    startupLogger.LogWarning("Syncfusion license key was not found in configuration or the environment. Configure Amplify build secrets or set SYNCFUSION_LICENSE_KEY before starting the app.");
}

startupLogger.LogInformation("Workspace client API base address: {BaseAddress}", resolvedApiBaseAddress);

var bootstrapTask = host.Services.GetRequiredService<WorkspaceBootstrapService>().LoadAsync();
Console.WriteLine("[startup] Workspace bootstrap task started.");
_ = bootstrapTask.ContinueWith(task =>
{
    if (task.IsFaulted)
    {
        startupLogger.LogWarning(task.Exception, "Workspace bootstrap finished with a background error.");
        return;
    }

    startupLogger.LogInformation("Workspace bootstrap finished in the background.");
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
