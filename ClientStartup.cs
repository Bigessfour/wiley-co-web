using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;
using Syncfusion.Licensing;
using WileyCoWeb.Components;
using WileyCoWeb.State;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace WileyCoWeb.Services
{
    static partial class ClientStartup
    {
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

        public static async Task RunAsync(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            var startupDiagnostics = new List<(LogLevel Level, string Message, Exception? Exception)>();
            var clientBaseAddress = builder.HostEnvironment.BaseAddress;

            var startupState = await ResolveClientStartupStateAsync(builder, clientBaseAddress, startupDiagnostics).ConfigureAwait(false);

            ConfigureClientBuilder(builder, startupState.ResolvedApiBaseAddress);

            var host = builder.Build();
            var startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WileyCoWeb.Startup");

            LogClientStartupState(
                startupLogger,
                startupDiagnostics,
                startupState.SyncfusionLicenseKey,
                startupState.SyncfusionKeySource,
                startupState.WorkspaceApiSource,
                clientBaseAddress,
                startupState.ResolvedApiBaseAddress,
                builder.HostEnvironment.Environment);

            StartWorkspaceBootstrapTask(host.Services, startupLogger);
            await host.RunAsync().ConfigureAwait(false);
        }

        static async Task<(string? SyncfusionLicenseKey, string SyncfusionKeySource, string WorkspaceApiSource, Uri ResolvedApiBaseAddress)> ResolveClientStartupStateAsync(
            WebAssemblyHostBuilder builder,
            string clientBaseAddress,
            IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
        {
            var syncfusionStartup = await ResolveSyncfusionStartupAsync(builder, clientBaseAddress, startupDiagnostics).ConfigureAwait(false);
            ApplySyncfusionLicense(syncfusionStartup.SyncfusionLicenseKey);

            var workspaceStartup = await ResolveWorkspaceApiStartupAsync(builder, clientBaseAddress, startupDiagnostics).ConfigureAwait(false);

            return (syncfusionStartup.SyncfusionLicenseKey, syncfusionStartup.SyncfusionKeySource, workspaceStartup.WorkspaceApiSource, workspaceStartup.ResolvedApiBaseAddress);
        }

        static async Task<(string? SyncfusionLicenseKey, string SyncfusionKeySource)> ResolveSyncfusionStartupAsync(
            WebAssemblyHostBuilder builder,
            string clientBaseAddress,
            IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
        {
            var syncfusionStartup = ResolveSyncfusionStartupFromPrimarySources(builder);

            if (!string.IsNullOrWhiteSpace(syncfusionStartup.SyncfusionLicenseKey))
            {
                return syncfusionStartup;
            }

            var syncfusionLicenseKey = await LoadSyncfusionLicenseKeyFromLocalSettingsAsync(clientBaseAddress, startupDiagnostics).ConfigureAwait(false);
            return (syncfusionLicenseKey, string.IsNullOrWhiteSpace(syncfusionLicenseKey) ? syncfusionStartup.SyncfusionKeySource : "local-settings-file");
        }

        static (string? SyncfusionLicenseKey, string SyncfusionKeySource) ResolveSyncfusionStartupFromPrimarySources(WebAssemblyHostBuilder builder)
        {
            var syncfusionLicenseKeyFromConfig = NormalizeSyncfusionLicenseKey(builder.Configuration["SyncfusionLicenseKey"]);
            var syncfusionLicenseKeyFromEnvironment = NormalizeSyncfusionLicenseKey(Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY"));
            var syncfusionLicenseKey = syncfusionLicenseKeyFromConfig ?? syncfusionLicenseKeyFromEnvironment;

            return (syncfusionLicenseKey, ResolveSyncfusionKeySource(syncfusionLicenseKeyFromConfig, syncfusionLicenseKeyFromEnvironment));
        }

        static void ApplySyncfusionLicense(string? syncfusionLicenseKey)
        {
            if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
            {
                SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
            }
        }

        static string ResolveSyncfusionKeySource(string? syncfusionLicenseKeyFromConfig, string? syncfusionLicenseKeyFromEnvironment)
        {
            if (syncfusionLicenseKeyFromConfig is not null)
            {
                return "config:SyncfusionLicenseKey";
            }

            if (syncfusionLicenseKeyFromEnvironment is not null)
            {
                return "env:SYNCFUSION_LICENSE_KEY";
            }

            return "not-found";
        }

        static async Task<(string? WorkspaceApiBaseAddress, string WorkspaceApiSource, Uri ResolvedApiBaseAddress)> ResolveWorkspaceApiStartupAsync(
            WebAssemblyHostBuilder builder,
            string clientBaseAddress,
            IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
        {
            // Prefer the deployed public settings file so Amplify runtime cutovers are driven by the artifact the browser actually downloads.
            var localSettingsApiBaseAddress = await LoadWorkspaceApiBaseAddressFromLocalSettingsAsync(clientBaseAddress, startupDiagnostics).ConfigureAwait(false);
            var environmentApiBaseAddress = Environment.GetEnvironmentVariable("WILEY_WORKSPACE_API_BASE_ADDRESS");
            var configDirectApiBaseAddress = builder.Configuration["WILEY_WORKSPACE_API_BASE_ADDRESS"];
            var configNamedApiBaseAddress = builder.Configuration["WorkspaceApiBaseAddress"];
            var apiBaseAddress = SelectWorkspaceApiBaseAddress(localSettingsApiBaseAddress, environmentApiBaseAddress, configDirectApiBaseAddress, configNamedApiBaseAddress);
            var workspaceApiSource = ResolveWorkspaceApiSource(localSettingsApiBaseAddress, environmentApiBaseAddress, configDirectApiBaseAddress, configNamedApiBaseAddress, clientBaseAddress);
            var resolvedApiBaseAddress = !string.IsNullOrWhiteSpace(apiBaseAddress) && Uri.TryCreate(apiBaseAddress, UriKind.Absolute, out var apiUri)
                ? apiUri
                : ResolveLocalApiBaseAddress(clientBaseAddress);

            return (apiBaseAddress, workspaceApiSource, resolvedApiBaseAddress);
        }

        static string? SelectWorkspaceApiBaseAddress(
            string? localSettingsApiBaseAddress,
            string? environmentApiBaseAddress,
            string? configDirectApiBaseAddress,
            string? configNamedApiBaseAddress)
        {
            foreach (var candidate in EnumerateWorkspaceApiBaseAddressCandidates(
                localSettingsApiBaseAddress,
                environmentApiBaseAddress,
                configDirectApiBaseAddress,
                configNamedApiBaseAddress))
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        static IEnumerable<string?> EnumerateWorkspaceApiBaseAddressCandidates(
            string? localSettingsApiBaseAddress,
            string? environmentApiBaseAddress,
            string? configDirectApiBaseAddress,
            string? configNamedApiBaseAddress)
        {
            yield return localSettingsApiBaseAddress;
            yield return environmentApiBaseAddress;
            yield return configDirectApiBaseAddress;
            yield return configNamedApiBaseAddress;
        }

        static string ResolveWorkspaceApiSource(
            string? localSettingsApiBaseAddress,
            string? environmentApiBaseAddress,
            string? configDirectApiBaseAddress,
            string? configNamedApiBaseAddress,
            string clientBaseAddress)
        {
            var configuredSources = new (string? Value, string Source)[]
            {
            (localSettingsApiBaseAddress, "local-settings-file"),
            (environmentApiBaseAddress, "env:WILEY_WORKSPACE_API_BASE_ADDRESS"),
            (configDirectApiBaseAddress, "config:WILEY_WORKSPACE_API_BASE_ADDRESS"),
            (configNamedApiBaseAddress, "config:WorkspaceApiBaseAddress")
            };

            var configuredSource = configuredSources.FirstOrDefault(source => !string.IsNullOrWhiteSpace(source.Value));
            return string.IsNullOrWhiteSpace(configuredSource.Value) ? GetDefaultWorkspaceApiSource(clientBaseAddress) : configuredSource.Source;
        }

        static void ConfigureClientBuilder(WebAssemblyHostBuilder builder, Uri resolvedApiBaseAddress)
        {
            ConfigureClientRootComponents(builder);
            ConfigureClientLogging(builder);
            ConfigureClientServices(builder, resolvedApiBaseAddress);
        }

        static void ConfigureClientRootComponents(WebAssemblyHostBuilder builder)
        {
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");
        }

        static void ConfigureClientLogging(WebAssemblyHostBuilder builder)
        {
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information);
            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.Information);
            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Migrations", LogLevel.Information);
            builder.Logging.AddFilter("WileyWidget", LogLevel.Debug);
            builder.Logging.AddFilter("WileyWidget.Data", LogLevel.Debug);
            builder.Logging.AddFilter("WileyWidget.Services", LogLevel.Debug);
            builder.Logging.AddFilter("WileyWidget.Business", LogLevel.Debug);
        }

        static void ConfigureClientServices(WebAssemblyHostBuilder builder, Uri resolvedApiBaseAddress)
        {
            builder.Services.AddScoped(sp => new HttpClient
            {
                BaseAddress = resolvedApiBaseAddress,
                Timeout = TimeSpan.FromMinutes(4)
            });
            builder.Services.AddScoped(sp => new WorkspaceLocalBootstrapService(
                new HttpClient
                {
                    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
                    Timeout = TimeSpan.FromSeconds(30)
                }));
            builder.Services.AddSingleton<WorkspaceState>();
            builder.Services.AddScoped<WorkspaceBootstrapService>();
            builder.Services.AddScoped<WorkspacePersistenceService>();
            builder.Services.AddScoped<WorkspaceSnapshotApiService>();
            builder.Services.AddScoped<UtilityCustomerApiService>();
            builder.Services.AddScoped<WorkspaceKnowledgeApiService>();
            builder.Services.AddScoped<QuickBooksImportApiService>();
            builder.Services.AddScoped<WorkspaceAiApiService>();
            builder.Services.AddScoped<WorkspaceDocumentExportService>();
            builder.Services.AddScoped<BrowserDownloadService>();

            builder.Services.AddSyncfusionBlazor();
        }

        static void LogClientStartupState(
            ILogger startupLogger,
            IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics,
            string? syncfusionLicenseKey,
            string syncfusionKeySource,
            string workspaceApiSource,
            string clientBaseAddress,
            Uri resolvedApiBaseAddress,
            string environment)
        {
            LogClientStartupDiagnostics(startupLogger, startupDiagnostics);
            LogSyncfusionStartupState(startupLogger, syncfusionLicenseKey, syncfusionKeySource);
            LogWorkspaceStartupState(startupLogger, environment, clientBaseAddress, syncfusionKeySource, syncfusionLicenseKey, workspaceApiSource, resolvedApiBaseAddress);
        }

        static void LogClientStartupDiagnostics(
            ILogger startupLogger,
            IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
        {
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
        }

        static void LogSyncfusionStartupState(ILogger startupLogger, string? syncfusionLicenseKey, string syncfusionKeySource)
        {
            if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
            {
                startupLogger.LogInformation("Syncfusion license key loaded from {SyncfusionKeySource}: PRESENT (length {LicenseKeyLength})", syncfusionKeySource, syncfusionLicenseKey.Length);
                return;
            }

            startupLogger.LogWarning("Syncfusion license key was not found in configuration or the environment. Configure Amplify build secrets or set SYNCFUSION_LICENSE_KEY before starting the app.");
        }

        static void LogWorkspaceStartupState(
            ILogger startupLogger,
            string environment,
            string clientBaseAddress,
            string syncfusionKeySource,
            string? syncfusionLicenseKey,
            string workspaceApiSource,
            Uri resolvedApiBaseAddress)
        {
            startupLogger.LogInformation(
                "WileyWidget.Client.StartupBaseline Environment={Environment} ClientBaseAddress={ClientBaseAddress} SyncfusionKeySource={SyncfusionKeySource} SyncfusionKeyPresent={SyncfusionKeyPresent} WorkspaceApiSource={WorkspaceApiSource} WorkspaceApiBaseAddress={WorkspaceApiBaseAddress}",
                environment,
                clientBaseAddress,
                syncfusionKeySource,
                !string.IsNullOrWhiteSpace(syncfusionLicenseKey),
                workspaceApiSource,
                resolvedApiBaseAddress);
        }

        static void StartWorkspaceBootstrapTask(IServiceProvider services, ILogger startupLogger)
        {
            var bootstrapStopwatch = Stopwatch.StartNew();
            var bootstrapTask = services.GetRequiredService<WorkspaceBootstrapService>().LoadAsync();
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
        }
    }
}
