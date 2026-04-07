using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;
using Syncfusion.Licensing;
using WileyCoWeb.Components;
using WileyCoWeb.Services;
using WileyCoWeb.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var syncfusionLicenseKey = builder.Configuration["SyncfusionLicenseKey"]
    ?? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
{
    SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
}
else
{
    Console.WriteLine("WARNING: Syncfusion license key was not found in configuration or the environment. Configure Amplify build secrets or set SYNCFUSION_LICENSE_KEY before starting the app.");
}

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
    var apiBaseAddress = Environment.GetEnvironmentVariable("WILEY_WORKSPACE_API_BASE_ADDRESS");
    var baseAddress = !string.IsNullOrWhiteSpace(apiBaseAddress) && Uri.TryCreate(apiBaseAddress, UriKind.Absolute, out var apiUri)
        ? apiUri
        : new Uri(builder.HostEnvironment.BaseAddress);

    return new HttpClient { BaseAddress = baseAddress };
});
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
await host.Services.GetRequiredService<WorkspaceBootstrapService>().LoadAsync();
await host.RunAsync();
