using System.Text.Json;
using System.Diagnostics;
using System.ComponentModel.DataAnnotations;
using System.Net;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Syncfusion.Licensing;
using WileyCoWeb.Contracts;
using WileyWidget.Data;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Models.Amplify;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.HealthChecks;
using WileyWidget.Services.Logging;
using BusinessActivityLogRepository = WileyWidget.Business.Interfaces.IActivityLogRepository;

namespace WileyCoWeb.Api;

public partial class Program
{
    private const string ScenarioRecordPrefix = "RecordType:Scenario";
    private const string RateSnapshotRecordPrefix = "RecordType:RateSnapshot";

    protected Program()
    {
    }

    public static async Task Main(string[] args)
    {
        var startupStopwatch = Stopwatch.StartNew();
        WebApplicationBuilder? builder = null;
        ILoggerFactory? bootstrapLoggerFactory = null;
        ILogger? bootstrapLogger = null;

        try
        {
            builder = WebApplication.CreateBuilder(args);
            bootstrapLoggerFactory = CreateStartupLoggerFactory();
            bootstrapLogger = bootstrapLoggerFactory.CreateLogger("WileyCoWeb.Api.Startup");
            bootstrapLogger.LogInformation(
                "Workspace API startup beginning. Environment={Environment} ContentRoot={ContentRoot} ApplicationName={ApplicationName} CurrentDirectory={CurrentDirectory}",
                builder.Environment.EnvironmentName,
                builder.Environment.ContentRootPath,
                builder.Environment.ApplicationName,
                Environment.CurrentDirectory);

            var xaiSecretResolution = await ConfigureXaiSecretAsync(builder.Configuration, builder.Environment).ConfigureAwait(false);

            // AWS X-Ray: distributed tracing for all incoming requests.
            // Credentials are resolved from the IAM execution role (Amplify / ECS task role) — no connection string needed.
            AWSXRayRecorder.InitializeInstance(builder.Configuration);
            Console.WriteLine("[API Startup] AWS X-Ray tracing initialized (service: WileyCoWeb.Api).");

            // --- Syncfusion license resolution (track source for telemetry) ---
            var syncfusionKeySource = "not-found";
            string? syncfusionLicenseKey = null;

            var sfFromConfigDirect = builder.Configuration["SYNCFUSION_LICENSE_KEY"];
            if (!string.IsNullOrWhiteSpace(sfFromConfigDirect))
            {
                syncfusionLicenseKey = sfFromConfigDirect;
                syncfusionKeySource = "config:SYNCFUSION_LICENSE_KEY";
            }
            else
            {
                var sfFromConfigNamed = builder.Configuration["SyncfusionLicenseKey"];
                if (!string.IsNullOrWhiteSpace(sfFromConfigNamed))
                {
                    syncfusionLicenseKey = sfFromConfigNamed;
                    syncfusionKeySource = "config:SyncfusionLicenseKey";
                }
                else
                {
                    var sfFromEnv = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                    if (!string.IsNullOrWhiteSpace(sfFromEnv))
                    {
                        syncfusionLicenseKey = sfFromEnv;
                        syncfusionKeySource = "env:SYNCFUSION_LICENSE_KEY";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(syncfusionLicenseKey)
                && (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("IntegrationTest")))
            {
                syncfusionLicenseKey = await LoadSyncfusionLicenseKeyFromLocalSettingsAsync(builder.Environment).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
                    syncfusionKeySource = "local-settings-file";
            }

            if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
            {
                SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey.Trim());
                Console.WriteLine($"[API Startup] Syncfusion license key registered (source: {syncfusionKeySource}, length: {syncfusionLicenseKey.Trim().Length}).");
            }
            else
            {
                Console.WriteLine("[API Startup] WARNING: SYNCFUSION_LICENSE_KEY not found from any source (config, env, local settings). " +
                    "Server-side PDF/Excel features may trigger license popups. Set SYNCFUSION_LICENSE_KEY env var or AWS Secrets Manager.");
            }

            // Capture xAI key resolution state after ConfigureXaiSecretAsync has run
            var xaiEnvironmentApiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
            var xaiConfigDirectApiKey = builder.Configuration["XAI_API_KEY"];
            var xaiConfigNamedApiKey = builder.Configuration["XAI:ApiKey"];
            var xaiKeyResolved = !string.IsNullOrWhiteSpace(
                xaiEnvironmentApiKey
                ?? xaiConfigDirectApiKey
                ?? xaiConfigNamedApiKey);
            var xaiKeySource = !string.IsNullOrWhiteSpace(xaiEnvironmentApiKey)
                ? "env:XAI_API_KEY"
                : !string.IsNullOrWhiteSpace(xaiSecretResolution.ResolvedKeySource)
                    && !string.Equals(xaiSecretResolution.ResolvedKeySource, "not-found", StringComparison.OrdinalIgnoreCase)
                    ? xaiSecretResolution.ResolvedKeySource
                    : !string.IsNullOrWhiteSpace(xaiConfigDirectApiKey)
                        ? "config:XAI_API_KEY"
                        : !string.IsNullOrWhiteSpace(xaiConfigNamedApiKey)
                            ? "config:XAI:ApiKey"
                            : "not-found";

            var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? builder.Configuration["ConnectionStrings:DefaultConnection"]
                ?? builder.Configuration["DATABASE_URL"]
                ?? Environment.GetEnvironmentVariable("DATABASE_URL");

            var allowDegradedStartup = builder.Environment.IsEnvironment("IntegrationTest")
                || builder.Configuration.GetValue<bool>("Database:AllowDegradedStartup");
            var seedDevelopmentData = builder.Configuration.GetValue<bool>("Database:SeedDevelopmentData");

            await TryActivateDegradedModeForUnavailableDevelopmentDatabaseAsync(
                configuredConnectionString,
                allowDegradedStartup,
                builder.Environment,
                bootstrapLogger).ConfigureAwait(false);

            if (!allowDegradedStartup)
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:EnableInMemoryFallback"] = "false"
                });
            }

            if (string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                const string missingConnectionStringMessage = "No database connection string was configured for the API host.";

                if (allowDegradedStartup)
                {
                    if (builder.Environment.IsEnvironment("IntegrationTest"))
                    {
                        bootstrapLogger.LogInformation("Workspace API skipped degraded fallback activation because the IntegrationTest host supplies its own in-memory database without a connection string.");
                    }
                    else
                    {
                        AppDbStartupState.ActivateFallback(missingConnectionStringMessage);
                    }
                }
                else
                {
                    throw new InvalidOperationException($"{missingConnectionStringMessage} Configure ConnectionStrings:DefaultConnection or DATABASE_URL before starting the workspace API.");
                }
            }

            ConfigureApiLogging(builder.Logging, !builder.Environment.IsEnvironment("IntegrationTest"));

            var allowedWorkspaceClientOrigins = BuildAllowedWorkspaceClientOrigins(builder.Configuration);

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("OpenWorkspaceClient", policy =>
                {
                    // Restricted per Q evaluation and plan hardening (Amplify + local dev only; no AllowAnyOrigin in prod)
                    policy.AllowAnyHeader()
                        .AllowAnyMethod()
                            .SetIsOriginAllowed(origin => IsAllowedWorkspaceClientOrigin(origin, allowedWorkspaceClientOrigins))
                        .AllowCredentials();
                });
            });
            builder.Services.AddHttpClient();
            builder.Services.AddMemoryCache();

            builder.Services.AddSingleton<IDbContextFactory<AppDbContext>>(_ => new AppDbContextFactory(builder.Configuration));
            builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
            builder.Services.AddSingleton<BusinessActivityLogRepository, ActivityLogRepository>();
            builder.Services.AddSingleton<IAccountsRepository, AccountsRepository>();
            builder.Services.AddSingleton<IAuditRepository, AuditRepository>();
            builder.Services.AddSingleton<IBudgetRepository, BudgetRepository>();
            builder.Services.AddSingleton<IEnterpriseRepository, EnterpriseRepository>();
            builder.Services.AddSingleton<IDepartmentRepository, DepartmentRepository>();
            builder.Services.AddSingleton<IMunicipalAccountRepository, MunicipalAccountRepository>();
            builder.Services.AddSingleton<IVendorRepository, VendorRepository>();
            builder.Services.AddSingleton<IScenarioSnapshotRepository, ScenarioSnapshotRepository>();
            builder.Services.AddSingleton<IDataAnonymizerService, DataAnonymizerService>();
            builder.Services.AddTransient<IAnalyticsRepository, AnalyticsRepository>();
            builder.Services.AddTransient<IBudgetAnalyticsRepository, BudgetAnalyticsRepository>();
            builder.Services.AddTransient<IAnalyticsService, AnalyticsService>();
            builder.Services.AddTransient<IWorkspaceKnowledgeService, WorkspaceKnowledgeService>();
            builder.Services.AddSingleton<WorkspaceSnapshotComposer>();
            builder.Services.AddSingleton<WorkspaceSnapshotExportArchiveService>();
            builder.Services.AddSingleton<WorkspaceReferenceDataImportService>();
            builder.Services.AddSingleton<QuickBooksImportService>();
            builder.Services.AddSingleton<QuickBooksImportAssistantService>();
            builder.Services.AddSingleton<WorkspaceAiAssistantService>();
            builder.Services.AddSingleton<UserContext>();
            builder.Services.AddSingleton<IUserContext>(sp => sp.GetRequiredService<UserContext>());
            builder.Services.AddSingleton<IConversationRepository, EfConversationRepository>();
            builder.Services.AddSingleton<IWileyWidgetContextService, WileyWidgetContextService>();

            // Deterministic license tracking via health check (covers the new registration logic in this file)
            builder.Services.AddHealthChecks()
                .AddCheck<SyncfusionLicenseHealthCheck>("syncfusion-license", Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded);

            var app = builder.Build();
            var logger = app.Logger;

            // Emit startup key-resolution event as a structured log entry.
            // Amplify ships stdout to CloudWatch Logs automatically; query with CloudWatch Logs Insights.
            LogStartupKeyResolution(
                logger,
                syncfusionKeySource,
                syncfusionLicenseKey,
                xaiKeySource,
                xaiKeyResolved,
                xaiSecretResolution,
                builder.Environment.EnvironmentName);

            LogRuntimeBaseline(
                logger,
                builder,
                configuredConnectionString,
                allowDegradedStartup,
                seedDevelopmentData,
                allowedWorkspaceClientOrigins,
                startupStopwatch.ElapsedMilliseconds);

            if (AppDbStartupState.IsDegradedMode)
            {
                if (seedDevelopmentData)
                {
                    await SeedDevelopmentDataAsync(app.Services);
                    logger.LogWarning("Workspace API is running in degraded mode with explicitly seeded development data.");
                }
                else
                {
                    logger.LogWarning("Workspace API is running in degraded mode without seeded sample data. Configure a real database or disable degraded startup.");
                }
            }

            logger.LogInformation("Workspace API host initialized in {ElapsedMs}ms.", startupStopwatch.ElapsedMilliseconds);

            app.Use(async (context, next) =>
            {
                var stopwatch = Stopwatch.StartNew();
                logger.LogInformation("API request started: {Method} {Path}{QueryString}", context.Request.Method, context.Request.Path, context.Request.QueryString);

                try
                {
                    await next().ConfigureAwait(false);
                    logger.LogInformation(
                        "API request completed: {Method} {Path}{QueryString} -> {StatusCode} in {ElapsedMs}ms",
                        context.Request.Method,
                        context.Request.Path,
                        context.Request.QueryString,
                        context.Response.StatusCode,
                        stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "API request failed: {Method} {Path}{QueryString} after {ElapsedMs}ms",
                        context.Request.Method,
                        context.Request.Path,
                        context.Request.QueryString,
                        stopwatch.ElapsedMilliseconds);
                    throw;
                }
            });

            app.Use(async (context, next) =>
            {
                var userContext = context.RequestServices.GetRequiredService<UserContext>();
                PopulateUserContext(context, userContext);

                try
                {
                    await next().ConfigureAwait(false);
                }
                finally
                {
                    userContext.SetCurrentUser(null, null, null);
                }
            });

            app.UseXRay("WileyCoWeb.Api");
            app.UseCors("OpenWorkspaceClient");
            MapWorkspaceSnapshotEndpoints(app);
            app.MapHealthChecks("/health");  // Exposes deterministic license status (and other checks) at /health

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            bootstrapLoggerFactory ??= CreateStartupLoggerFactory();
            bootstrapLogger ??= bootstrapLoggerFactory.CreateLogger("WileyCoWeb.Api.Startup");

            LogFatalStartupException(
                bootstrapLogger,
                ex,
                builder,
                startupStopwatch.ElapsedMilliseconds,
                args.Length);

            throw;
        }
        finally
        {
            bootstrapLoggerFactory?.Dispose();
        }
    }

    private static ILoggerFactory CreateStartupLoggerFactory()
    {
        return LoggerFactory.Create(logging => ConfigureApiLogging(logging, includeWorkspaceFileLogger: true));
    }

    private static void ConfigureApiLogging(ILoggingBuilder logging, bool includeWorkspaceFileLogger)
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Debug);
        logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "HH:mm:ss.fff ";
            options.SingleLine = true;
            options.IncludeScopes = false;
        });
        logging.AddFilter("Microsoft", LogLevel.Information);
        logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
        logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Information);
        logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Information);
        logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information);
        logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
        logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.Information);
        logging.AddFilter("Microsoft.EntityFrameworkCore.Migrations", LogLevel.Information);
        logging.AddFilter("WileyWidget", LogLevel.Debug);
        logging.AddFilter("WileyWidget.Data", LogLevel.Debug);
        logging.AddFilter("WileyWidget.Services", LogLevel.Debug);
        logging.AddFilter("WileyWidget.Business", LogLevel.Debug);

        if (includeWorkspaceFileLogger)
        {
            logging.AddProvider(new WorkspaceFileLoggerProvider("wiley-widget.log"));
        }
    }

    private static async Task TryActivateDegradedModeForUnavailableDevelopmentDatabaseAsync(
        string? configuredConnectionString,
        bool allowDegradedStartup,
        IWebHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!allowDegradedStartup
            || AppDbStartupState.IsDegradedMode
            || string.IsNullOrWhiteSpace(configuredConnectionString)
            || !environment.IsDevelopment())
        {
            return;
        }

        const string fallbackReason = "Configured development database was unreachable during startup.";

        try
        {
            var probeConnectionString = BuildConnectivityProbeConnectionString(configuredConnectionString);
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(probeConnectionString)
                .Options;

            await using var context = new AppDbContext(options);
            if (await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                logger.LogInformation("Workspace API validated the configured development database before service registration.");
                return;
            }
        }
        catch (Exception ex)
        {
            AppDbStartupState.ActivateFallback(fallbackReason);
            logger.LogWarning(ex, "Workspace API activated degraded mode because the configured development database could not be reached during startup.");
            return;
        }

        AppDbStartupState.ActivateFallback(fallbackReason);
        logger.LogWarning("Workspace API activated degraded mode because the configured development database could not be reached during startup.");
    }

    private static string BuildConnectivityProbeConnectionString(string configuredConnectionString)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(Environment.ExpandEnvironmentVariables(configuredConnectionString))
        {
            Pooling = false
        };

        if (connectionStringBuilder.Timeout <= 0 || connectionStringBuilder.Timeout > 5)
        {
            connectionStringBuilder.Timeout = 5;
        }

        if (connectionStringBuilder.CommandTimeout <= 0 || connectionStringBuilder.CommandTimeout > 5)
        {
            connectionStringBuilder.CommandTimeout = 5;
        }

        return connectionStringBuilder.ConnectionString;
    }

    private static void LogRuntimeBaseline(
        ILogger logger,
        WebApplicationBuilder builder,
        string? configuredConnectionString,
        bool allowDegradedStartup,
        bool seedDevelopmentData,
        IReadOnlySet<string> allowedWorkspaceClientOrigins,
        long startupElapsedMilliseconds)
    {
        var aspNetCoreUrls = builder.Configuration["ASPNETCORE_URLS"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
            ?? "(default)";
        var allowedOrigins = allowedWorkspaceClientOrigins.Count == 0
            ? "(none)"
            : string.Join(',', allowedWorkspaceClientOrigins.OrderBy(origin => origin, StringComparer.OrdinalIgnoreCase));

        logger.LogInformation(
            "WileyWidget.Startup.RuntimeBaseline Environment={Environment} ContentRoot={ContentRoot} ApplicationName={ApplicationName} CurrentDirectory={CurrentDirectory} AspNetCoreUrls={AspNetCoreUrls} DatabaseConfigured={DatabaseConfigured} AllowDegradedStartup={AllowDegradedStartup} DegradedModeActive={DegradedModeActive} SeedDevelopmentData={SeedDevelopmentData} AllowedWorkspaceClientOriginCount={AllowedWorkspaceClientOriginCount} AllowedWorkspaceClientOrigins={AllowedWorkspaceClientOrigins} StartupElapsedMs={StartupElapsedMs}",
            builder.Environment.EnvironmentName,
            builder.Environment.ContentRootPath,
            builder.Environment.ApplicationName,
            Environment.CurrentDirectory,
            aspNetCoreUrls,
            !string.IsNullOrWhiteSpace(configuredConnectionString),
            allowDegradedStartup,
            AppDbStartupState.IsDegradedMode,
            seedDevelopmentData,
            allowedWorkspaceClientOrigins.Count,
            allowedOrigins,
            startupElapsedMilliseconds);
    }

    private static void LogFatalStartupException(
        ILogger logger,
        Exception exception,
        WebApplicationBuilder? builder,
        long startupElapsedMilliseconds,
        int argsCount)
    {
        logger.LogCritical(
            exception,
            "Workspace API fatal startup failure after {StartupElapsedMs}ms. Environment={Environment} ContentRoot={ContentRoot} ApplicationName={ApplicationName} CurrentDirectory={CurrentDirectory} ArgsCount={ArgsCount}",
            startupElapsedMilliseconds,
            builder?.Environment.EnvironmentName ?? "(unavailable)",
            builder?.Environment.ContentRootPath ?? "(unavailable)",
            builder?.Environment.ApplicationName ?? "(unavailable)",
            Environment.CurrentDirectory,
            argsCount);
    }

    private static bool IsAllowedWorkspaceClientOrigin(string origin, IReadOnlySet<string> allowedOrigins)
    {
        if (string.IsNullOrWhiteSpace(origin)
            || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || IPAddress.TryParse(uri.Host, out var ipAddress) && IPAddress.IsLoopback(ipAddress)))
        {
            return true;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedOrigin = $"{uri.Scheme}://{uri.Authority}";
        return allowedOrigins.Contains(normalizedOrigin)
            || IsAllowedAmplifyPreviewOrigin(uri, allowedOrigins);
    }

    private static IReadOnlySet<string> BuildAllowedWorkspaceClientOrigins(IConfiguration configuration)
    {
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var configuredOrigins = configuration["WorkspaceClientOrigins"];
        if (!string.IsNullOrWhiteSpace(configuredOrigins))
        {
            foreach (var origin in configuredOrigins.Split([';', ',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                origins.Add(origin);
            }
        }

        foreach (var configuredOrigin in configuration.GetSection("WorkspaceClientOrigins").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(configuredOrigin.Value))
            {
                origins.Add(configuredOrigin.Value.Trim());
            }
        }

        return origins;
    }

    private static bool IsAllowedAmplifyPreviewOrigin(Uri originUri, IReadOnlySet<string> allowedOrigins)
    {
        if (!originUri.Host.EndsWith(".amplifyapp.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var allowedOrigin in allowedOrigins)
        {
            if (!Uri.TryCreate(allowedOrigin, UriKind.Absolute, out var allowedUri)
                || !allowedUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var allowedAmplifySuffix = GetAmplifyPreviewHostSuffix(allowedUri.Host);
            if (string.IsNullOrWhiteSpace(allowedAmplifySuffix))
            {
                continue;
            }

            if (originUri.Host.Equals(allowedAmplifySuffix, StringComparison.OrdinalIgnoreCase)
                || originUri.Host.EndsWith($".{allowedAmplifySuffix}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetAmplifyPreviewHostSuffix(string host)
    {
        if (string.IsNullOrWhiteSpace(host)
            || !host.EndsWith(".amplifyapp.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return labels.Length < 3 ? null : string.Join('.', labels[^3..]);
    }

    private static async Task<XaiSecretResolutionResult> ConfigureXaiSecretAsync(ConfigurationManager configuration, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var secretName = configuration["XAI:SecretName"]
            ?? configuration["XAI_SECRET_NAME"]
            ?? "Grok";
        var regionName = configuration["WILEY_AWS_REGION"]
            ?? configuration["AWS_REGION"]
            ?? configuration["AWS_DEFAULT_REGION"]
            ?? "us-east-2";

        var environmentApiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
        var configDirectApiKey = configuration["XAI_API_KEY"];
        var configNamedApiKey = configuration["XAI:ApiKey"];

        if (!string.IsNullOrWhiteSpace(environmentApiKey))
        {
            return new XaiSecretResolutionResult(
                ResolvedKeySource: "env:XAI_API_KEY",
                EnvironmentKeyPresent: true,
                ConfigDirectKeyPresent: !string.IsNullOrWhiteSpace(configDirectApiKey),
                ConfigNamedKeyPresent: !string.IsNullOrWhiteSpace(configNamedApiKey),
                SecretFetchAttempted: false,
                SecretName: secretName,
                RegionName: regionName,
                SecretFetchStatus: "skipped_existing_environment_key",
                SecretFetchErrorCode: null,
                SecretFetchErrorMessage: null,
                ConfigurationInjected: false);
        }

        if (!string.IsNullOrWhiteSpace(configDirectApiKey))
        {
            return new XaiSecretResolutionResult(
                ResolvedKeySource: "config:XAI_API_KEY",
                EnvironmentKeyPresent: false,
                ConfigDirectKeyPresent: true,
                ConfigNamedKeyPresent: !string.IsNullOrWhiteSpace(configNamedApiKey),
                SecretFetchAttempted: false,
                SecretName: secretName,
                RegionName: regionName,
                SecretFetchStatus: "skipped_existing_direct_config_key",
                SecretFetchErrorCode: null,
                SecretFetchErrorMessage: null,
                ConfigurationInjected: false);
        }

        if (!string.IsNullOrWhiteSpace(configNamedApiKey))
        {
            return new XaiSecretResolutionResult(
                ResolvedKeySource: "config:XAI:ApiKey",
                EnvironmentKeyPresent: false,
                ConfigDirectKeyPresent: false,
                ConfigNamedKeyPresent: true,
                SecretFetchAttempted: false,
                SecretName: secretName,
                RegionName: regionName,
                SecretFetchStatus: "skipped_existing_named_config_key",
                SecretFetchErrorCode: null,
                SecretFetchErrorMessage: null,
                ConfigurationInjected: false);
        }

        if (environment.IsEnvironment("IntegrationTest"))
        {
            return new XaiSecretResolutionResult(
                ResolvedKeySource: "not-found",
                EnvironmentKeyPresent: false,
                ConfigDirectKeyPresent: false,
                ConfigNamedKeyPresent: false,
                SecretFetchAttempted: false,
                SecretName: secretName,
                RegionName: regionName,
                SecretFetchStatus: "skipped_integration_test",
                SecretFetchErrorCode: null,
                SecretFetchErrorMessage: null,
                ConfigurationInjected: false);
        }

        var secretLookup = await TryGetSecretAsync(secretName, regionName).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(secretLookup.SecretValue))
        {
            return new XaiSecretResolutionResult(
                ResolvedKeySource: "not-found",
                EnvironmentKeyPresent: false,
                ConfigDirectKeyPresent: false,
                ConfigNamedKeyPresent: false,
                SecretFetchAttempted: true,
                SecretName: secretName,
                RegionName: regionName,
                SecretFetchStatus: secretLookup.Status,
                SecretFetchErrorCode: secretLookup.ErrorCode,
                SecretFetchErrorMessage: secretLookup.ErrorMessage,
                ConfigurationInjected: false);
        }

        var normalizedApiKey = TryExtractApiKey(secretLookup.SecretValue);
        if (string.IsNullOrWhiteSpace(normalizedApiKey))
        {
            return new XaiSecretResolutionResult(
                ResolvedKeySource: "not-found",
                EnvironmentKeyPresent: false,
                ConfigDirectKeyPresent: false,
                ConfigNamedKeyPresent: false,
                SecretFetchAttempted: true,
                SecretName: secretName,
                RegionName: regionName,
                SecretFetchStatus: "secret_loaded_but_api_key_extraction_failed",
                SecretFetchErrorCode: null,
                SecretFetchErrorMessage: "The resolved secret did not contain a usable API key value.",
                ConfigurationInjected: false);
        }

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XAI_API_KEY"] = normalizedApiKey,
            ["XAI:ApiKey"] = normalizedApiKey,
            ["XAI:SecretName"] = secretName
        });

        return new XaiSecretResolutionResult(
            ResolvedKeySource: $"secrets-manager:{secretName}",
            EnvironmentKeyPresent: false,
            ConfigDirectKeyPresent: false,
            ConfigNamedKeyPresent: false,
            SecretFetchAttempted: true,
            SecretName: secretName,
            RegionName: regionName,
            SecretFetchStatus: "secret_loaded_and_injected",
            SecretFetchErrorCode: null,
            SecretFetchErrorMessage: null,
            ConfigurationInjected: true);
    }

    private static async Task<string?> LoadSyncfusionLicenseKeyFromLocalSettingsAsync(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var candidatePaths = new[]
        {
            Path.Combine(environment.ContentRootPath, "appsettings.Syncfusion.local.json"),
            Path.Combine(environment.ContentRootPath, "..", "appsettings.Syncfusion.local.json")
        };

        foreach (var candidatePath in candidatePaths)
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            try
            {
                var localSettingsJson = await File.ReadAllTextAsync(candidatePath).ConfigureAwait(false);
                using var document = JsonDocument.Parse(localSettingsJson);

                if (document.RootElement.TryGetProperty("SyncfusionLicenseKey", out var licenseKeyElement))
                {
                    var licenseKey = licenseKeyElement.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(licenseKey))
                    {
                        return licenseKey;
                    }
                }
            }
            catch
            {
                // Ignore local file issues and keep the existing warning path.
            }
        }

        return null;
    }

    private static void LogStartupKeyResolution(
        ILogger logger,
        string syncfusionKeySource,
        string? syncfusionLicenseKey,
        string xaiKeySource,
        bool xaiKeyResolved,
        XaiSecretResolutionResult xaiSecretResolution,
        string environmentName)
    {
        // Only surface a safe truncated fingerprint — never the full value
        static string KeyFingerprint(string? key) =>
            string.IsNullOrWhiteSpace(key)
                ? "(empty)"
                : (key.Trim().Length > 8 ? key.Trim()[..8] + "..." : "(too-short)");

        static string? TruncateForLog(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return normalized.Length > 200 ? normalized[..200] : normalized;
        }

        logger.LogInformation(
            "WileyWidget.Startup.KeyResolution Environment={Environment} SyncfusionKeySource={SyncfusionKeySource} " +
            "SyncfusionKeyPresent={SyncfusionKeyPresent} SyncfusionKeyLength={SyncfusionKeyLength} " +
            "SyncfusionKeyFingerprint={SyncfusionKeyFingerprint} XaiKeySource={XaiKeySource} XaiKeyPresent={XaiKeyPresent} " +
            "XaiEnvironmentKeyPresent={XaiEnvironmentKeyPresent} XaiConfigDirectKeyPresent={XaiConfigDirectKeyPresent} " +
            "XaiConfigNamedKeyPresent={XaiConfigNamedKeyPresent} XaiSecretFetchAttempted={XaiSecretFetchAttempted} " +
            "XaiSecretName={XaiSecretName} XaiAwsRegion={XaiAwsRegion} XaiSecretFetchStatus={XaiSecretFetchStatus} " +
            "XaiSecretFetchErrorCode={XaiSecretFetchErrorCode} XaiSecretFetchErrorMessage={XaiSecretFetchErrorMessage} " +
            "XaiConfigurationInjected={XaiConfigurationInjected}",
            environmentName,
            syncfusionKeySource,
            !string.IsNullOrWhiteSpace(syncfusionLicenseKey),
            syncfusionLicenseKey?.Trim().Length ?? 0,
            KeyFingerprint(syncfusionLicenseKey),
            xaiKeySource,
            xaiKeyResolved,
            xaiSecretResolution.EnvironmentKeyPresent,
            xaiSecretResolution.ConfigDirectKeyPresent,
            xaiSecretResolution.ConfigNamedKeyPresent,
            xaiSecretResolution.SecretFetchAttempted,
            xaiSecretResolution.SecretName,
            xaiSecretResolution.RegionName,
            xaiSecretResolution.SecretFetchStatus,
            xaiSecretResolution.SecretFetchErrorCode,
            TruncateForLog(xaiSecretResolution.SecretFetchErrorMessage),
            xaiSecretResolution.ConfigurationInjected);
    }



    private static async Task<SecretLookupResult> TryGetSecretAsync(string secretName, string regionName)
    {
        if (string.IsNullOrWhiteSpace(secretName) || string.IsNullOrWhiteSpace(regionName))
        {
            return new SecretLookupResult("invalid_lookup_configuration", null, "invalid_lookup_configuration", "Secret name or AWS region was missing.");
        }

        try
        {
            using var client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(regionName));
            var response = await client.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = secretName,
                VersionStage = "AWSCURRENT"
            }).ConfigureAwait(false);

            return new SecretLookupResult("secret_loaded", response.SecretString);
        }
        catch (ResourceNotFoundException ex)
        {
            return new SecretLookupResult("resource_not_found", null, nameof(ResourceNotFoundException), ex.Message);
        }
        catch (InvalidRequestException ex)
        {
            return new SecretLookupResult("invalid_request", null, nameof(InvalidRequestException), ex.Message);
        }
        catch (InvalidParameterException ex)
        {
            return new SecretLookupResult("invalid_parameter", null, nameof(InvalidParameterException), ex.Message);
        }
        catch (AmazonSecretsManagerException ex)
        {
            return new SecretLookupResult("amazon_secrets_manager_exception", null, ex.ErrorCode ?? nameof(AmazonSecretsManagerException), ex.Message);
        }
        catch (Exception ex)
        {
            return new SecretLookupResult("unexpected_secret_resolution_exception", null, ex.GetType().Name, ex.Message);
        }
    }

    private static string? TryExtractApiKey(string secretValue)
    {
        if (string.IsNullOrWhiteSpace(secretValue))
        {
            return null;
        }

        var trimmed = secretValue.Trim();
        if (!trimmed.StartsWith('{'))
        {
            return trimmed;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return trimmed;
            }

            foreach (var propertyName in new[] { "XAI_API_KEY", "ApiKey", "XaiApiKey", "GrokApiKey", "XAI:ApiKey" })
            {
                if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }

            return trimmed;
        }
        catch (JsonException)
        {
            return trimmed;
        }
    }

    private static void MapWorkspaceSnapshotEndpoints(WebApplication app)
    {
        MapWorkspaceSnapshotGetEndpoint(app);
        MapWorkspaceSnapshotPostEndpoint(app);
        MapWorkspaceKnowledgeEndpoint(app);
        MapWorkspaceBaselinePutEndpoint(app);
        MapWorkspaceScenarioListEndpoint(app);
        MapWorkspaceScenarioGetEndpoint(app);
        MapWorkspaceScenarioPostEndpoint(app);
        MapWorkspaceSnapshotExportsPostEndpoint(app);
        MapWorkspaceSnapshotExportsGetEndpoint(app);
        MapWorkspaceExportDownloadEndpoint(app);
        MapWorkspaceReferenceDataImportEndpoint(app);
        MapUtilityCustomerEndpoints(app);
        MapWorkspaceAiChatEndpoint(app);
        MapQuickBooksImportEndpoints(app);
    }

    private sealed record XaiSecretResolutionResult(
        string ResolvedKeySource,
        bool EnvironmentKeyPresent,
        bool ConfigDirectKeyPresent,
        bool ConfigNamedKeyPresent,
        bool SecretFetchAttempted,
        string SecretName,
        string RegionName,
        string SecretFetchStatus,
        string? SecretFetchErrorCode,
        string? SecretFetchErrorMessage,
        bool ConfigurationInjected);

    private sealed record SecretLookupResult(
        string Status,
        string? SecretValue,
        string? ErrorCode = null,
        string? ErrorMessage = null);

    private static void MapWorkspaceAiChatEndpoint(WebApplication app)
    {
        app.MapPost("/api/ai/chat", MapWorkspaceAiChatMessageEndpoint);
        app.MapPost("/api/ai/chat/reset", MapWorkspaceAiChatResetEndpoint);
        app.MapGet("/api/ai/recommendations", MapWorkspaceRecommendationHistoryEndpoint);
    }

    private static void MapQuickBooksImportEndpoints(WebApplication app)
    {
        app.MapPost("/api/imports/quickbooks/preview", MapQuickBooksPreviewEndpoint);
        app.MapPost("/api/imports/quickbooks/commit", MapQuickBooksCommitEndpoint);
        app.MapPost("/api/imports/quickbooks/assistant", MapQuickBooksAssistantEndpoint);
    }

    private static async Task<IResult> MapQuickBooksPreviewEndpoint(HttpRequest request, QuickBooksImportService importService, CancellationToken cancellationToken)
    {
        var importRequest = await ReadQuickBooksImportRequestAsync(request, cancellationToken);
        if (importRequest is null)
        {
            return Results.BadRequest("A QuickBooks export file, enterprise, and fiscal year are required.");
        }

        var preview = await importService.PreviewAsync(importRequest.FileBytes, importRequest.FileName, importRequest.SelectedEnterprise, importRequest.SelectedFiscalYear, cancellationToken);
        return Results.Ok(preview);
    }

    private static async Task<IResult> MapQuickBooksCommitEndpoint(HttpRequest request, QuickBooksImportService importService, CancellationToken cancellationToken)
    {
        var importRequest = await ReadQuickBooksImportRequestAsync(request, cancellationToken);
        if (importRequest is null)
        {
            return Results.BadRequest("A QuickBooks export file, enterprise, and fiscal year are required.");
        }

        var commitResult = await importService.CommitAsync(importRequest.FileBytes, importRequest.FileName, importRequest.SelectedEnterprise, importRequest.SelectedFiscalYear, cancellationToken);
        return commitResult.IsDuplicate
            ? Results.Conflict(commitResult)
            : Results.Ok(commitResult);
    }

    private static async Task<IResult> MapQuickBooksAssistantEndpoint(HttpRequest request, QuickBooksImportAssistantService assistantService, CancellationToken cancellationToken)
    {
        var guidanceRequest = await request.ReadFromJsonAsync<QuickBooksImportGuidanceRequest>(cancellationToken: cancellationToken);
        if (guidanceRequest is null || string.IsNullOrWhiteSpace(guidanceRequest.Question))
        {
            return Results.BadRequest("A question is required for QuickBooks import assistance.");
        }

        var guidance = await assistantService.AskAsync(guidanceRequest, cancellationToken);
        return Results.Ok(guidance);
    }

    private static async Task<IResult> MapWorkspaceAiChatMessageEndpoint(HttpRequest request, WorkspaceAiAssistantService assistantService, CancellationToken cancellationToken)
    {
        var chatRequest = await request.ReadFromJsonAsync<WorkspaceChatRequest>(cancellationToken: cancellationToken);
        if (chatRequest is null || string.IsNullOrWhiteSpace(chatRequest.Question))
        {
            return Results.BadRequest("A question is required for workspace chat.");
        }

        var chatResponse = await assistantService.AskAsync(chatRequest, cancellationToken);
        return Results.Ok(chatResponse);
    }

    private static async Task<IResult> MapWorkspaceAiChatResetEndpoint(HttpRequest request, WorkspaceAiAssistantService assistantService, CancellationToken cancellationToken)
    {
        var resetRequest = await request.ReadFromJsonAsync<WorkspaceConversationResetRequest>(cancellationToken: cancellationToken);
        if (resetRequest is null)
        {
            return Results.BadRequest("Workspace context is required to reset the Jarvis conversation.");
        }

        await assistantService.ResetConversationAsync(resetRequest, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> MapWorkspaceRecommendationHistoryEndpoint(
        [FromQuery] string? enterprise,
        [FromQuery] int? fiscalYear,
        [FromQuery] int? limit,
        WorkspaceAiAssistantService assistantService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(enterprise) || fiscalYear is null)
        {
            return Results.BadRequest("Enterprise and fiscal year are required to load recommendation history.");
        }

        var history = await assistantService.GetRecommendationHistoryAsync(
            new WorkspaceRecommendationHistoryRequest(enterprise.Trim(), fiscalYear.Value, limit ?? 12),
            cancellationToken);

        return Results.Ok(history);
    }

    private static void PopulateUserContext(HttpContext context, UserContext userContext)
    {
        var principal = context.User;

        var userId = ResolveClaim(principal, "sub")
            ?? ResolveClaim(principal, ClaimTypes.NameIdentifier)
            ?? context.Request.Headers["X-Wiley-User-Id"].FirstOrDefault()
            ?? "anonymous";

        var displayName = ResolveClaim(principal, "name")
            ?? ResolveClaim(principal, "preferred_username")
            ?? ResolveClaim(principal, ClaimTypes.Name)
            ?? ResolveClaim(principal, ClaimTypes.Email)?.Split('@', 2)[0]
            ?? context.Request.Headers["X-Wiley-User-Name"].FirstOrDefault()
            ?? "Guest";

        var email = ResolveClaim(principal, ClaimTypes.Email)
            ?? ResolveClaim(principal, "email")
            ?? context.Request.Headers["X-Wiley-User-Email"].FirstOrDefault();

        userContext.SetCurrentUser(userId, displayName, email);
    }

    private static string? ResolveClaim(ClaimsPrincipal principal, string claimType)
        => principal.FindFirst(claimType)?.Value;



    private static async Task SeedDevelopmentDataAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        await context.Database.EnsureCreatedAsync();

        if (await context.Enterprises.AnyAsync())
        {
            return;
        }

        context.Enterprises.AddRange(
            new WileyWidget.Models.Enterprise
            {
                Name = "Water Utility",
                CurrentRate = 31.25m,
                MonthlyExpenses = 98000m,
                CitizenCount = 4500,
                IsDeleted = false
            },
            new WileyWidget.Models.Enterprise
            {
                Name = "Sanitation Utility",
                CurrentRate = 21.50m,
                MonthlyExpenses = 72000m,
                CitizenCount = 3200,
                IsDeleted = false
            },
            new WileyWidget.Models.Enterprise
            {
                Name = "Archived Utility",
                CurrentRate = 12m,
                MonthlyExpenses = 800m,
                CitizenCount = 20,
                IsDeleted = true
            });

        context.UtilityCustomers.AddRange(
            new WileyWidget.Models.UtilityCustomer
            {
                AccountNumber = "1001",
                FirstName = "Ada",
                LastName = "Lovelace",
                CustomerType = WileyWidget.Models.CustomerType.Residential,
                ServiceAddress = "1 Main St",
                ServiceCity = "Wiley",
                ServiceState = "CO",
                ServiceZipCode = "81092",
                ServiceLocation = WileyWidget.Models.ServiceLocation.InsideCityLimits
            },
            new WileyWidget.Models.UtilityCustomer
            {
                AccountNumber = "1002",
                FirstName = "Grace",
                LastName = "Hopper",
                CompanyName = "Harbor Foods",
                CustomerType = WileyWidget.Models.CustomerType.Commercial,
                ServiceAddress = "2 Market St",
                ServiceCity = "Wiley",
                ServiceState = "CO",
                ServiceZipCode = "81092",
                ServiceLocation = WileyWidget.Models.ServiceLocation.OutsideCityLimits
            });

        await context.SaveChangesAsync();
    }

    private static async Task<QuickBooksImportRequest?> ReadQuickBooksImportRequestAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file == null)
        {
            return null;
        }

        var selectedEnterprise = form["selectedEnterprise"].ToString();
        if (string.IsNullOrWhiteSpace(selectedEnterprise))
        {
            return null;
        }

        if (!int.TryParse(form["selectedFiscalYear"].ToString(), out var selectedFiscalYear) || selectedFiscalYear <= 0)
        {
            return null;
        }

        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);

        return new QuickBooksImportRequest(
            memoryStream.ToArray(),
            file.FileName,
            selectedEnterprise,
            selectedFiscalYear);
    }

    private sealed record QuickBooksImportRequest(byte[] FileBytes, string FileName, string SelectedEnterprise, int SelectedFiscalYear);

    private static void MapWorkspaceReferenceDataImportEndpoint(WebApplication app)
    {
        app.MapPost("/api/workspace/reference-data/import", async (
            WorkspaceReferenceDataImportRequest? request,
            WorkspaceReferenceDataImportService importService,
            IWebHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var response = await importService.ImportAsync(request, environment.ContentRootPath, cancellationToken);
                return Results.Ok(response);
            }
            catch (DirectoryNotFoundException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });
    }

    private static void MapUtilityCustomerEndpoints(WebApplication app)
    {
        var customers = app.MapGroup("/api/utility-customers");

        customers.MapGet(string.Empty, async (
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var records = await context.UtilityCustomers
                .AsNoTracking()
                .OrderBy(customer => customer.AccountNumber)
                .ThenBy(customer => customer.LastName)
                .ThenBy(customer => customer.FirstName)
                .Select(customer => ToUtilityCustomerRecord(customer))
                .ToListAsync(cancellationToken);

            return Results.Ok(records);
        });

        customers.MapGet("/{id:int}", async (
            int id,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var customer = await context.UtilityCustomers
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            return customer == null
                ? Results.NotFound()
                : Results.Ok(ToUtilityCustomerRecord(customer));
        });

        customers.MapPost(string.Empty, async (
            UtilityCustomerUpsertRequest request,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            if (await context.UtilityCustomers.AnyAsync(item => item.AccountNumber == request.AccountNumber, cancellationToken))
            {
                return Results.Conflict($"A utility customer with account number '{request.AccountNumber}' already exists.");
            }

            var customer = new UtilityCustomer();
            ApplyUtilityCustomerRequest(customer, request, isNew: true);

            var validationErrors = ValidateModel(customer);
            if (validationErrors != null)
            {
                return Results.ValidationProblem(validationErrors);
            }

            context.UtilityCustomers.Add(customer);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/utility-customers/{customer.Id}", ToUtilityCustomerRecord(customer));
        });

        customers.MapPut("/{id:int}", async (
            int id,
            UtilityCustomerUpsertRequest request,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var customer = await context.UtilityCustomers.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (customer == null)
            {
                return Results.NotFound();
            }

            if (await context.UtilityCustomers.AnyAsync(item => item.Id != id && item.AccountNumber == request.AccountNumber, cancellationToken))
            {
                return Results.Conflict($"A utility customer with account number '{request.AccountNumber}' already exists.");
            }

            ApplyUtilityCustomerRequest(customer, request, isNew: false);

            var validationErrors = ValidateModel(customer);
            if (validationErrors != null)
            {
                return Results.ValidationProblem(validationErrors);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToUtilityCustomerRecord(customer));
        });

        customers.MapDelete("/{id:int}", async (
            int id,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var customer = await context.UtilityCustomers.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (customer == null)
            {
                return Results.NotFound();
            }

            context.UtilityCustomers.Remove(customer);
            await context.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
    }

    private static void MapWorkspaceKnowledgeEndpoint(WebApplication app)
    {
        var logger = app.Logger;

        app.MapPost("/api/workspace/knowledge", async (
            WorkspaceKnowledgeRequest request,
            IWorkspaceKnowledgeService knowledgeService,
            CancellationToken cancellationToken) =>
        {
            if (request.Snapshot is null)
            {
                return Results.BadRequest("A workspace snapshot is required to calculate live knowledge.");
            }

            if (string.IsNullOrWhiteSpace(request.Snapshot.SelectedEnterprise))
            {
                return Results.BadRequest("A workspace enterprise is required to calculate live knowledge.");
            }

            if (request.Snapshot.SelectedFiscalYear <= 0)
            {
                return Results.BadRequest("A valid fiscal year is required to calculate live knowledge.");
            }

            var snapshot = request.Snapshot;
            var knowledgeInput = new WorkspaceKnowledgeInput(
                snapshot.SelectedEnterprise,
                snapshot.SelectedFiscalYear,
                snapshot.CurrentRate ?? 0m,
                snapshot.TotalCosts ?? 0m,
                snapshot.ProjectedVolume ?? 0m,
                snapshot.ScenarioItems?.Sum(item => item.Cost) ?? 0m,
                Math.Clamp(request.TopVarianceCount, 1, 20),
                Math.Clamp(request.ForecastYears, 1, 10));

            try
            {
                var knowledge = await knowledgeService.BuildAsync(knowledgeInput, cancellationToken);
                return Results.Ok(ToWorkspaceKnowledgeResponse(knowledge));
            }
            catch (WorkspaceKnowledgeNotFoundException ex)
            {
                return Results.NotFound(ex.Message);
            }
            catch (WorkspaceKnowledgeUnavailableException ex)
            {
                logger.LogError(ex, "Workspace knowledge request failed for {Enterprise} FY {FiscalYear}", snapshot.SelectedEnterprise, snapshot.SelectedFiscalYear);
                return Results.Problem(
                    title: "Workspace knowledge unavailable",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });
    }

    private static UtilityCustomerRecord ToUtilityCustomerRecord(UtilityCustomer customer)
        => new(
            customer.Id,
            customer.AccountNumber,
            customer.FirstName,
            customer.LastName,
            customer.CompanyName,
            customer.DisplayName,
            customer.CustomerTypeDescription,
            customer.ServiceAddress,
            customer.ServiceCity,
            customer.ServiceState,
            customer.ServiceZipCode,
            customer.ServiceLocationDescription,
            customer.StatusDescription,
            customer.CurrentBalance,
            customer.AccountOpenDate.ToString("O"),
            customer.PhoneNumber,
            customer.EmailAddress,
            customer.MeterNumber,
            customer.Notes);

    private static void ApplyUtilityCustomerRequest(UtilityCustomer customer, UtilityCustomerUpsertRequest request, bool isNew)
    {
        customer.AccountNumber = request.AccountNumber.Trim();
        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.CompanyName = request.CompanyName;
        customer.CustomerType = MapCustomerType(request.CustomerType);
        customer.ServiceAddress = request.ServiceAddress.Trim();
        customer.ServiceCity = request.ServiceCity.Trim();
        customer.ServiceState = request.ServiceState.Trim().ToUpperInvariant();
        customer.ServiceZipCode = request.ServiceZipCode.Trim();
        customer.ServiceLocation = MapServiceLocation(request.ServiceLocation);
        customer.Status = MapCustomerStatus(request.Status);
        customer.CurrentBalance = decimal.Round(request.CurrentBalance, 2, MidpointRounding.AwayFromZero);
        customer.AccountOpenDate = NormalizeUtcDate(request.AccountOpenDate) ?? DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        customer.PhoneNumber = request.PhoneNumber;
        customer.EmailAddress = request.EmailAddress;
        customer.MeterNumber = request.MeterNumber;
        customer.Notes = request.Notes;
        customer.LastModifiedDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        if (isNew)
        {
            customer.CreatedDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        }
    }

    private static WileyWidget.Models.CustomerType MapCustomerType(WileyCoWeb.Contracts.CustomerType customerType)
        => customerType switch
        {
            WileyCoWeb.Contracts.CustomerType.Commercial => WileyWidget.Models.CustomerType.Commercial,
            WileyCoWeb.Contracts.CustomerType.Industrial => WileyWidget.Models.CustomerType.Industrial,
            WileyCoWeb.Contracts.CustomerType.Agricultural => WileyWidget.Models.CustomerType.Agricultural,
            WileyCoWeb.Contracts.CustomerType.Institutional => WileyWidget.Models.CustomerType.Institutional,
            WileyCoWeb.Contracts.CustomerType.Government => WileyWidget.Models.CustomerType.Government,
            WileyCoWeb.Contracts.CustomerType.MultiFamily => WileyWidget.Models.CustomerType.MultiFamily,
            _ => WileyWidget.Models.CustomerType.Residential
        };

    private static WileyWidget.Models.ServiceLocation MapServiceLocation(WileyCoWeb.Contracts.ServiceLocation serviceLocation)
        => serviceLocation switch
        {
            WileyCoWeb.Contracts.ServiceLocation.OutsideCityLimits => WileyWidget.Models.ServiceLocation.OutsideCityLimits,
            _ => WileyWidget.Models.ServiceLocation.InsideCityLimits
        };

    private static WileyWidget.Models.CustomerStatus MapCustomerStatus(WileyCoWeb.Contracts.CustomerStatus status)
        => status switch
        {
            WileyCoWeb.Contracts.CustomerStatus.Inactive => WileyWidget.Models.CustomerStatus.Inactive,
            WileyCoWeb.Contracts.CustomerStatus.Suspended => WileyWidget.Models.CustomerStatus.Suspended,
            WileyCoWeb.Contracts.CustomerStatus.Closed => WileyWidget.Models.CustomerStatus.Closed,
            _ => WileyWidget.Models.CustomerStatus.Active
        };

    private static DateTime? NormalizeUtcDate(DateTime? value)
    {
        if (value == null)
        {
            return null;
        }

        var normalized = value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };

        return normalized;
    }

    private static Dictionary<string, string[]>? ValidateModel(object model)
    {
        var validationContext = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();
        if (Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true))
        {
            return null;
        }

        return validationResults
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty).Select(member => new
            {
                Member = string.IsNullOrWhiteSpace(member) ? "model" : member,
                Error = result.ErrorMessage ?? "The supplied value is invalid."
            }))
            .GroupBy(item => item.Member, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Error).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static void MapWorkspaceSnapshotGetEndpoint(WebApplication app)
    {
        app.MapGet("/api/workspace/snapshot", async (
            string? enterprise,
            int? fiscalYear,
            WorkspaceSnapshotComposer composer,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await composer.BuildAsync(enterprise, fiscalYear, cancellationToken);
            return Results.Ok(snapshot);
        });
    }

    private static void MapWorkspaceSnapshotPostEndpoint(WebApplication app)
    {
        app.MapPost("/api/workspace/snapshot", async (
            WorkspaceBootstrapData request,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.SelectedEnterprise))
            {
                return Results.BadRequest("An enterprise name is required to save a snapshot.");
            }

            if (request.SelectedFiscalYear <= 0)
            {
                return Results.BadRequest("A valid fiscal year is required to save a snapshot.");
            }

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var savedAt = DateTimeOffset.UtcNow;
            var snapshot = new BudgetSnapshot
            {
                SnapshotName = $"{request.SelectedEnterprise} FY{request.SelectedFiscalYear} rate snapshot",
                SnapshotDate = DateOnly.FromDateTime(savedAt.UtcDateTime),
                CreatedAt = savedAt,
                Notes = $"{RateSnapshotRecordPrefix}; Enterprise: {request.SelectedEnterprise}; FY: {request.SelectedFiscalYear}; Current rate: {request.CurrentRate:0.##}; Total costs: {request.TotalCosts:0.##}; Projected volume: {request.ProjectedVolume:0.##}",
                Payload = JsonSerializer.Serialize(request)
            };

            context.BudgetSnapshots.Add(snapshot);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/workspace/snapshot/{snapshot.Id}", new WorkspaceSnapshotSaveResponse(
                snapshot.Id,
                snapshot.SnapshotName,
                snapshot.CreatedAt.ToString("O")));
        });
    }

    private static void MapWorkspaceBaselinePutEndpoint(WebApplication app)
    {
        app.MapPut("/api/workspace/baseline", async (
            WorkspaceBaselineUpdateRequest request,
            IDbContextFactory<AppDbContext> contextFactory,
            WorkspaceSnapshotComposer composer,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.SelectedEnterprise))
            {
                return Results.BadRequest("A workspace enterprise is required.");
            }

            if (request.SelectedFiscalYear <= 0)
            {
                return Results.BadRequest("A valid fiscal year is required.");
            }

            if (request.ProjectedVolume <= 0)
            {
                return Results.BadRequest("Projected volume must be greater than zero.");
            }

            if (request.CurrentRate < 0 || request.TotalCosts < 0)
            {
                return Results.BadRequest("Workspace baseline values cannot be negative.");
            }

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var enterprise = await context.Enterprises
                .FirstOrDefaultAsync(item => !item.IsDeleted && item.Name == request.SelectedEnterprise, cancellationToken);

            if (enterprise == null)
            {
                return Results.NotFound($"Enterprise '{request.SelectedEnterprise}' was not found.");
            }

            enterprise.CurrentRate = decimal.Round(request.CurrentRate, 2, MidpointRounding.AwayFromZero);
            enterprise.MonthlyExpenses = decimal.Round(request.TotalCosts, 2, MidpointRounding.AwayFromZero);
            enterprise.CitizenCount = Math.Max(1, decimal.ToInt32(decimal.Round(request.ProjectedVolume, 0, MidpointRounding.AwayFromZero)));
            enterprise.LastModified = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            var snapshot = await composer.BuildAsync(request.SelectedEnterprise, request.SelectedFiscalYear, cancellationToken);
            var savedAtUtc = DateTime.UtcNow.ToString("O");
            var response = new WorkspaceBaselineUpdateResponse(
                snapshot.SelectedEnterprise,
                snapshot.SelectedFiscalYear,
                savedAtUtc,
                $"Saved baseline values for {snapshot.SelectedEnterprise} FY {snapshot.SelectedFiscalYear}.",
                snapshot);

            return Results.Ok(response);
        });
    }

    private static void MapWorkspaceScenarioListEndpoint(WebApplication app)
    {
        app.MapGet("/api/workspace/scenarios", async (
            string? enterprise,
            int? fiscalYear,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var snapshots = await context.BudgetSnapshots
                .AsNoTracking()
                .Where(snapshot => snapshot.Notes != null && snapshot.Notes.Contains(ScenarioRecordPrefix))
                .OrderByDescending(snapshot => snapshot.CreatedAt)
                .ToListAsync(cancellationToken);

            var scenarios = new List<WorkspaceScenarioSummaryResponse>();
            foreach (var snapshot in snapshots)
            {
                var payload = TryDeserializeBootstrap(snapshot.Payload);
                if (payload == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(enterprise) &&
                    !string.Equals(payload.SelectedEnterprise, enterprise, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (fiscalYear is > 0 && payload.SelectedFiscalYear != fiscalYear.Value)
                {
                    continue;
                }

                scenarios.Add(BuildScenarioSummary(snapshot, payload));
            }

            return Results.Ok(new WorkspaceScenarioCollectionResponse(scenarios));
        });
    }

    private static void MapWorkspaceScenarioGetEndpoint(WebApplication app)
    {
        app.MapGet("/api/workspace/scenarios/{snapshotId:long}", async (
            long snapshotId,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var snapshot = await context.BudgetSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == snapshotId, cancellationToken);

            if (snapshot == null || snapshot.Notes?.Contains(ScenarioRecordPrefix, StringComparison.Ordinal) != true)
            {
                return Results.NotFound();
            }

            var payload = TryDeserializeBootstrap(snapshot.Payload);
            return payload == null ? Results.BadRequest("The selected scenario does not contain a valid workspace payload.") : Results.Ok(payload);
        });
    }

    private static void MapWorkspaceScenarioPostEndpoint(WebApplication app)
    {
        app.MapPost("/api/workspace/scenarios", async (
            WorkspaceScenarioSaveRequest request,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            if (request.Snapshot == null)
            {
                return Results.BadRequest("A workspace snapshot is required.");
            }

            if (string.IsNullOrWhiteSpace(request.ScenarioName))
            {
                return Results.BadRequest("A scenario name is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Snapshot.SelectedEnterprise) || request.Snapshot.SelectedFiscalYear <= 0)
            {
                return Results.BadRequest("Scenario persistence requires a valid enterprise and fiscal year.");
            }

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var savedAt = DateTimeOffset.UtcNow;
            var normalizedScenarioName = request.ScenarioName.Trim();
            var snapshot = new BudgetSnapshot
            {
                SnapshotName = $"{request.Snapshot.SelectedEnterprise} FY{request.Snapshot.SelectedFiscalYear} scenario {normalizedScenarioName}",
                SnapshotDate = DateOnly.FromDateTime(savedAt.UtcDateTime),
                CreatedAt = savedAt,
                Notes = BuildScenarioNotes(request, normalizedScenarioName),
                Payload = JsonSerializer.Serialize(request.Snapshot with { ActiveScenarioName = normalizedScenarioName, LastUpdatedUtc = savedAt.ToString("O") })
            };

            context.BudgetSnapshots.Add(snapshot);
            await context.SaveChangesAsync(cancellationToken);

            var payload = TryDeserializeBootstrap(snapshot.Payload) ?? request.Snapshot;
            return Results.Created($"/api/workspace/scenarios/{snapshot.Id}", BuildScenarioSummary(snapshot, payload, request.Description));
        });
    }

    private static void MapWorkspaceSnapshotExportsPostEndpoint(WebApplication app)
    {
        app.MapPost("/api/workspace/snapshot/{snapshotId:long}/exports", async (
            long snapshotId,
            WorkspaceSnapshotArtifactRequest? request,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var snapshot = await context.BudgetSnapshots
                .Include(item => item.ExportArtifacts)
                .FirstOrDefaultAsync(item => item.Id == snapshotId, cancellationToken);

            if (snapshot == null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(snapshot.Payload))
            {
                return Results.BadRequest("The selected snapshot does not contain a payload that can be used to generate exports.");
            }

            var documents = WorkspaceSnapshotExportArchiveService.CreateDocuments(snapshot.Payload, request?.DocumentKinds);
            var normalizedKinds = documents.Select(document => document.DocumentKind).ToHashSet(StringComparer.Ordinal);

            if (request?.ReplaceExisting == true)
            {
                var existingArtifacts = snapshot.ExportArtifacts
                    .Where(artifact => normalizedKinds.Contains(artifact.DocumentKind))
                    .ToList();

                if (existingArtifacts.Count > 0)
                {
                    context.BudgetSnapshotArtifacts.RemoveRange(existingArtifacts);
                }
            }

            var createdAt = DateTimeOffset.UtcNow;
            var artifacts = documents.Select(document => new BudgetSnapshotArtifact
            {
                BudgetSnapshotId = snapshot.Id,
                DocumentKind = document.DocumentKind,
                FileName = document.FileName,
                ContentType = document.ContentType,
                SizeBytes = document.Content.LongLength,
                CreatedAt = createdAt,
                Payload = document.Content
            }).ToList();

            context.BudgetSnapshotArtifacts.AddRange(artifacts);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(new WorkspaceSnapshotArtifactBatchResponse(
                snapshot.Id,
                snapshot.SnapshotName,
                artifacts.Select(BuildArtifactSummary).ToList()));
        });
    }

    private static void MapWorkspaceSnapshotExportsGetEndpoint(WebApplication app)
    {
        app.MapGet("/api/workspace/snapshot/{snapshotId:long}/exports", async (
            long snapshotId,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var snapshot = await context.BudgetSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == snapshotId, cancellationToken);

            if (snapshot == null)
            {
                return Results.NotFound();
            }

            var artifacts = await context.BudgetSnapshotArtifacts
                .AsNoTracking()
                .Where(item => item.BudgetSnapshotId == snapshotId)
                .OrderByDescending(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .ToListAsync(cancellationToken);

            return Results.Ok(new WorkspaceSnapshotArtifactBatchResponse(
                snapshot.Id,
                snapshot.SnapshotName,
                artifacts.Select(BuildArtifactSummary).ToList()));
        });
    }

    private static void MapWorkspaceExportDownloadEndpoint(WebApplication app)
    {
        app.MapGet("/api/workspace/exports/{artifactId:long}", async (
            long artifactId,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var artifact = await context.BudgetSnapshotArtifacts
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == artifactId, cancellationToken);

            if (artifact == null)
            {
                return Results.NotFound();
            }

            return Results.File(artifact.Payload, artifact.ContentType, artifact.FileName);
        });
    }

    internal static WorkspaceSnapshotArtifactSummary BuildArtifactSummary(BudgetSnapshotArtifact artifact)
    {
        return new WorkspaceSnapshotArtifactSummary(
            artifact.Id,
            artifact.BudgetSnapshotId,
            artifact.DocumentKind,
            artifact.FileName,
            artifact.ContentType,
            artifact.SizeBytes,
            artifact.CreatedAt.ToString("O"),
            $"/api/workspace/exports/{artifact.Id}");
    }

    private static WorkspaceBootstrapData? TryDeserializeBootstrap(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WorkspaceBootstrapData>(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildScenarioNotes(WorkspaceScenarioSaveRequest request, string normalizedScenarioName)
    {
        return $"{ScenarioRecordPrefix}; Enterprise: {request.Snapshot.SelectedEnterprise}; FY: {request.Snapshot.SelectedFiscalYear}; Scenario: {normalizedScenarioName}; Description: {request.Description}";
    }

    private static WorkspaceScenarioSummaryResponse BuildScenarioSummary(BudgetSnapshot snapshot, WorkspaceBootstrapData payload, string? descriptionOverride = null)
    {
        var scenarioName = string.IsNullOrWhiteSpace(payload.ActiveScenarioName) ? snapshot.SnapshotName : payload.ActiveScenarioName;
        var description = descriptionOverride;
        if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(snapshot.Notes))
        {
            description = ExtractDescription(snapshot.Notes);
        }

        return new WorkspaceScenarioSummaryResponse(
            snapshot.Id,
            scenarioName,
            payload.SelectedEnterprise,
            payload.SelectedFiscalYear,
            snapshot.CreatedAt.ToString("O"),
            payload.CurrentRate,
            payload.TotalCosts,
            payload.ProjectedVolume,
            payload.ScenarioItems?.Sum(item => item.Cost) ?? 0m,
            payload.ScenarioItems?.Count ?? 0,
            description);
    }

    private static WorkspaceKnowledgeResponse ToWorkspaceKnowledgeResponse(WorkspaceKnowledgeResult knowledge)
    {
        return new WorkspaceKnowledgeResponse(
            knowledge.SelectedEnterprise,
            knowledge.SelectedFiscalYear,
            knowledge.OperationalStatus,
            knowledge.ExecutiveSummary,
            knowledge.RateRationale,
            knowledge.CurrentRate,
            knowledge.TotalCosts,
            knowledge.ProjectedVolume,
            knowledge.ScenarioCostTotal,
            knowledge.BreakEvenRate,
            knowledge.AdjustedBreakEvenRate,
            knowledge.RateGap,
            knowledge.AdjustedRateGap,
            knowledge.MonthlyRevenue,
            knowledge.NetPosition,
            knowledge.CoverageRatio,
            knowledge.CurrentReserveBalance,
            knowledge.RecommendedReserveLevel,
            knowledge.ReserveRiskAssessment,
                knowledge.GeneratedAtUtc.ToString("O"),
            knowledge.Insights.Select(item => new WorkspaceKnowledgeInsightResponse(item.Label, item.Value, item.Description)).ToArray(),
            knowledge.RecommendedActions.Select(item => new WorkspaceKnowledgeActionResponse(item.Title, item.Description, item.Priority)).ToArray(),
            knowledge.TopVariances.Select(item => new WorkspaceKnowledgeVarianceResponse(item.AccountName, item.BudgetedAmount, item.ActualAmount, item.VarianceAmount, item.VariancePercentage)).ToArray());
    }

    private static string? ExtractDescription(string notes)
    {
        const string marker = "Description: ";
        var markerIndex = notes.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var value = notes[(markerIndex + marker.Length)..].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
