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
    ?? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY")
    ?? Environment.GetEnvironmentVariable("SYNCUSION_LICENSE_KEY");

if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
{
    SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
}
else
{
    Console.WriteLine("WARNING: Syncfusion license key was not found in configuration or environment variables.");
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

builder.Services.AddSyncfusionBlazor();

var host = builder.Build();
await host.Services.GetRequiredService<WorkspaceBootstrapService>().LoadAsync();
await host.RunAsync();
