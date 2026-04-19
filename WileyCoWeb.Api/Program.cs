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
using WileyCoWeb.Api.Configuration;

namespace WileyCoWeb.Api;

public partial class Program
{
    private const string ScenarioRecordPrefix = "RecordType:Scenario";
    private const string RateSnapshotRecordPrefix = "RecordType:RateSnapshot";
    private static readonly Func<WorkspaceBaselineUpdateRequest, string?>[] WorkspaceBaselineValidationRules = new Func<WorkspaceBaselineUpdateRequest, string?>[]
    {
        request => string.IsNullOrWhiteSpace(request.SelectedEnterprise) ? "A workspace enterprise is required." : null,
        request => request.SelectedFiscalYear <= 0 ? "A valid fiscal year is required." : null,
        request => request.ProjectedVolume <= 0 ? "Projected volume must be greater than zero." : null,
        request => request.CurrentRate < 0 || request.TotalCosts < 0 ? "Workspace baseline values cannot be negative." : null
    };
    private static readonly Func<WorkspaceScenarioSaveRequest, string?>[] WorkspaceScenarioSaveValidationRules = new Func<WorkspaceScenarioSaveRequest, string?>[]
    {
        request => request.Snapshot == null ? "A workspace snapshot is required." : null,
        request => string.IsNullOrWhiteSpace(request.ScenarioName) ? "A scenario name is required." : null,
        request => request.Snapshot == null
            ? null
            : string.IsNullOrWhiteSpace(request.Snapshot.SelectedEnterprise) || request.Snapshot.SelectedFiscalYear <= 0
                ? "Scenario persistence requires a valid enterprise and fiscal year."
                : null
    };

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

            TracingBootstrapper.InitializeTracing(builder);

            var startupOptions = await PrepareStartupRuntimeOptionsAsync(builder, bootstrapLogger).ConfigureAwait(false);

            ConfigureServices(builder, startupOptions.ConfiguredConnectionString, startupOptions.AllowedWorkspaceClientOrigins);

            var app = builder.Build();
            await RunStartupHostAsync(app, builder, startupOptions, startupStopwatch).ConfigureAwait(false);
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
        return LoggerFactory.Create(logging => StartupConfigurationService.ConfigureApiLogging(logging, includeWorkspaceFileLogger: true));
    }

    private static async Task<(SyncfusionLicenseResult SyncfusionResult, XaiSecretResolutionResult XaiResult)> ResolveSecretsAsync(WebApplicationBuilder builder)
    {
        var secretResolver = new SecretResolver(builder.Configuration);
        var xaiSecretResolution = await secretResolver.ResolveXaiSecretAsync().ConfigureAwait(false);
        var syncfusionLicenseResult = await LicenseBootstrapper.RegisterSyncfusionLicenseAsync(builder).ConfigureAwait(false);
        return (syncfusionLicenseResult, xaiSecretResolution);
    }

    private static async Task<StartupRuntimeOptions> PrepareStartupRuntimeOptionsAsync(WebApplicationBuilder builder, ILogger bootstrapLogger)
    {
        var (syncfusionLicenseResult, xaiSecretResolution) = await ResolveSecretsAsync(builder).ConfigureAwait(false);

        var xaiEnvironmentApiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
        var xaiConfigDirectApiKey = builder.Configuration["XAI_API_KEY"];
        var xaiConfigNamedApiKey = builder.Configuration["XAI:ApiKey"];
        var xaiKeyResolved = !string.IsNullOrWhiteSpace(
            xaiEnvironmentApiKey
            ?? xaiConfigDirectApiKey
            ?? xaiConfigNamedApiKey);
        var xaiKeySource = DetermineXaiKeySource(xaiEnvironmentApiKey, xaiSecretResolution, xaiConfigDirectApiKey, xaiConfigNamedApiKey);

        var configuredConnectionString = GetConfiguredConnectionString(builder.Configuration);
        var allowDegradedStartup = builder.Environment.IsEnvironment("IntegrationTest")
            || builder.Configuration.GetValue<bool>("Database:AllowDegradedStartup");
        var seedDevelopmentData = builder.Configuration.GetValue<bool>("Database:SeedDevelopmentData");

        await StartupConfigurationService.TryActivateDegradedModeForUnavailableDevelopmentDatabaseAsync(
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

        EnsureStartupConnectionString(builder, configuredConnectionString, allowDegradedStartup, bootstrapLogger);

        var allowedWorkspaceClientOrigins = BuildAllowedWorkspaceClientOrigins(builder.Configuration);

        return new StartupRuntimeOptions(
            syncfusionLicenseResult,
            xaiSecretResolution,
            xaiKeySource,
            xaiKeyResolved,
            configuredConnectionString,
            allowDegradedStartup,
            seedDevelopmentData,
            allowedWorkspaceClientOrigins);
    }

    private static async Task RunStartupHostAsync(
        WebApplication app,
        WebApplicationBuilder builder,
        StartupRuntimeOptions startupOptions,
        Stopwatch startupStopwatch)
    {
        var logger = app.Logger;

        // Emit startup key-resolution event as a structured log entry.
        // Amplify ships stdout to CloudWatch Logs automatically; query with CloudWatch Logs Insights.
        LogStartupKeyResolution(
            logger,
            startupOptions.SyncfusionLicenseResult.KeySource,
            startupOptions.SyncfusionLicenseResult.LicenseKey,
            startupOptions.XaiKeySource,
            startupOptions.XaiKeyResolved,
            startupOptions.XaiSecretResolution,
            builder.Environment.EnvironmentName);

        LogRuntimeBaseline(
            logger,
            builder,
            startupOptions.ConfiguredConnectionString,
            startupOptions.AllowDegradedStartup,
            startupOptions.SeedDevelopmentData,
            startupOptions.AllowedWorkspaceClientOrigins,
            startupStopwatch.ElapsedMilliseconds);

        if (AppDbStartupState.IsDegradedMode)
        {
            if (startupOptions.SeedDevelopmentData)
            {
                await SeedDevelopmentDataAsync(app.Services).ConfigureAwait(false);
                logger.LogWarning("Workspace API is running in degraded mode with explicitly seeded development data.");
            }
            else
            {
                logger.LogWarning("Workspace API is running in degraded mode without seeded sample data. Configure a real database or disable degraded startup.");
            }
        }

        logger.LogInformation("Workspace API host initialized in {ElapsedMs}ms.", startupStopwatch.ElapsedMilliseconds);

        ConfigureMiddleware(app, logger);

        await app.RunAsync().ConfigureAwait(false);
    }

    private static void EnsureStartupConnectionString(WebApplicationBuilder builder, string? configuredConnectionString, bool allowDegradedStartup, ILogger bootstrapLogger)
    {
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return;
        }

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

            return;
        }

        throw new InvalidOperationException($"{missingConnectionStringMessage} Configure ConnectionStrings:DefaultConnection or DATABASE_URL before starting the workspace API.");
    }

    private sealed record StartupRuntimeOptions(
        SyncfusionLicenseResult SyncfusionLicenseResult,
        XaiSecretResolutionResult XaiSecretResolution,
        string XaiKeySource,
        bool XaiKeyResolved,
        string? ConfiguredConnectionString,
        bool AllowDegradedStartup,
        bool SeedDevelopmentData,
        IReadOnlySet<string> AllowedWorkspaceClientOrigins);

    private static string DetermineXaiKeySource(string? xaiEnvironmentApiKey, XaiSecretResolutionResult xaiSecretResolution, string? xaiConfigDirectApiKey, string? xaiConfigNamedApiKey)
    {
        return !string.IsNullOrWhiteSpace(xaiEnvironmentApiKey)
            ? "env:XAI_API_KEY"
            : !string.IsNullOrWhiteSpace(xaiSecretResolution.ResolvedKeySource)
                && !string.Equals(xaiSecretResolution.ResolvedKeySource, "not-found", StringComparison.OrdinalIgnoreCase)
                ? xaiSecretResolution.ResolvedKeySource
                : !string.IsNullOrWhiteSpace(xaiConfigDirectApiKey)
                    ? "config:XAI_API_KEY"
                    : !string.IsNullOrWhiteSpace(xaiConfigNamedApiKey)
                        ? "config:XAI:ApiKey"
                        : "not-found";
    }

    private static string? GetConfiguredConnectionString(IConfiguration configuration)
    {
        return configuration.GetConnectionString("DefaultConnection")
            ?? configuration["ConnectionStrings:DefaultConnection"]
            ?? configuration["DATABASE_URL"]
            ?? Environment.GetEnvironmentVariable("DATABASE_URL");
    }

    private static void ConfigureServices(WebApplicationBuilder builder, string? configuredConnectionString, IReadOnlySet<string> allowedWorkspaceClientOrigins)
    {
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
        builder.Services.AddSingleton<QuickBooksCsvParser>();
        builder.Services.AddSingleton<QuickBooksExcelParser>();
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
    }

    private static void ConfigureMiddleware(WebApplication app, ILogger logger)
    {
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

    private static void LogStartupKeyResolution(
        ILogger logger,
        string syncfusionKeySource,
        string? syncfusionLicenseKey,
        string xaiKeySource,
        bool xaiKeyResolved,
        XaiSecretResolutionResult xaiSecretResolution,
        string environmentName)
    {
        var logData = BuildStartupKeyResolutionLogData(
            syncfusionKeySource,
            syncfusionLicenseKey,
            xaiKeySource,
            xaiKeyResolved,
            xaiSecretResolution);

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
            logData.SyncfusionKeySource,
            logData.SyncfusionKeyPresent,
            logData.SyncfusionKeyLength,
            logData.SyncfusionKeyFingerprint,
            logData.XaiKeySource,
            logData.XaiKeyPresent,
            logData.XaiEnvironmentKeyPresent,
            logData.XaiConfigDirectKeyPresent,
            logData.XaiConfigNamedKeyPresent,
            logData.XaiSecretFetchAttempted,
            logData.XaiSecretName,
            logData.XaiAwsRegion,
            logData.XaiSecretFetchStatus,
            logData.XaiSecretFetchErrorCode,
            logData.XaiSecretFetchErrorMessage,
            logData.XaiConfigurationInjected);
    }

    private static StartupKeyResolutionLogData BuildStartupKeyResolutionLogData(
        string syncfusionKeySource,
        string? syncfusionLicenseKey,
        string xaiKeySource,
        bool xaiKeyResolved,
        XaiSecretResolutionResult xaiSecretResolution)
    {
        return new StartupKeyResolutionLogData(
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

    private static string KeyFingerprint(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "(empty)";
        }

        var trimmed = key.Trim();
        return trimmed.Length > 8 ? trimmed[..8] + "..." : "(too-short)";
    }

    private static string? TruncateForLog(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length > 200 ? normalized[..200] : normalized;
    }

    private sealed record StartupKeyResolutionLogData(
        string SyncfusionKeySource,
        bool SyncfusionKeyPresent,
        int SyncfusionKeyLength,
        string SyncfusionKeyFingerprint,
        string XaiKeySource,
        bool XaiKeyPresent,
        bool XaiEnvironmentKeyPresent,
        bool XaiConfigDirectKeyPresent,
        bool XaiConfigNamedKeyPresent,
        bool XaiSecretFetchAttempted,
        string XaiSecretName,
        string XaiAwsRegion,
        string XaiSecretFetchStatus,
        string? XaiSecretFetchErrorCode,
        string? XaiSecretFetchErrorMessage,
        bool XaiConfigurationInjected);

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
        var headers = context.Request.Headers;

        userContext.SetCurrentUser(
            ResolveUserId(principal, headers),
            ResolveUserDisplayName(principal, headers),
            ResolveUserEmail(principal, headers));
    }

    private static string ResolveUserId(ClaimsPrincipal principal, IHeaderDictionary headers)
    {
        return ResolveClaim(principal, "sub")
            ?? ResolveClaim(principal, ClaimTypes.NameIdentifier)
            ?? ResolveHeaderValue(headers, "X-Wiley-User-Id")
            ?? "anonymous";
    }

    private static string ResolveUserDisplayName(ClaimsPrincipal principal, IHeaderDictionary headers)
    {
        var emailLocalPart = ResolveEmailLocalPart(ResolveClaim(principal, ClaimTypes.Email));

        return ResolveClaim(principal, "name")
            ?? ResolveClaim(principal, "preferred_username")
            ?? ResolveClaim(principal, ClaimTypes.Name)
            ?? emailLocalPart
            ?? ResolveHeaderValue(headers, "X-Wiley-User-Name")
            ?? "Guest";
    }

    private static string? ResolveUserEmail(ClaimsPrincipal principal, IHeaderDictionary headers)
    {
        return ResolveClaim(principal, ClaimTypes.Email)
            ?? ResolveClaim(principal, "email")
            ?? ResolveHeaderValue(headers, "X-Wiley-User-Email");
    }

    private static string? ResolveClaim(ClaimsPrincipal principal, string claimType)
        => principal.FindFirst(claimType)?.Value;

    private static string? ResolveHeaderValue(IHeaderDictionary headers, string headerName)
        => headers[headerName].FirstOrDefault();

    private static string? ResolveEmailLocalPart(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return email.Split('@', 2)[0];
    }



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

        MapUtilityCustomerReadEndpoints(customers);
        MapUtilityCustomerWriteEndpoints(customers);
    }

    private static void MapUtilityCustomerReadEndpoints(RouteGroupBuilder customers)
    {
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
    }

    private static void MapUtilityCustomerWriteEndpoints(RouteGroupBuilder customers)
    {
        customers.MapPost(string.Empty, MapUtilityCustomerCreateEndpoint);
        customers.MapPut("/{id:int}", MapUtilityCustomerUpdateEndpoint);
        customers.MapDelete("/{id:int}", MapUtilityCustomerDeleteEndpoint);
    }

    private static async Task<IResult> MapUtilityCustomerCreateEndpoint(
        UtilityCustomerUpsertRequest request,
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
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
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Created($"/api/utility-customers/{customer.Id}", ToUtilityCustomerRecord(customer));
    }

    private static async Task<IResult> MapUtilityCustomerUpdateEndpoint(
        int id,
        UtilityCustomerUpsertRequest request,
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
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

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(ToUtilityCustomerRecord(customer));
    }

    private static async Task<IResult> MapUtilityCustomerDeleteEndpoint(
        int id,
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var customer = await context.UtilityCustomers.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (customer == null)
        {
            return Results.NotFound();
        }

        context.UtilityCustomers.Remove(customer);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static void MapWorkspaceKnowledgeEndpoint(WebApplication app)
    {
        var logger = app.Logger;

        app.MapPost("/api/workspace/knowledge", async (
            WorkspaceKnowledgeRequest request,
            IWorkspaceKnowledgeService knowledgeService,
            CancellationToken cancellationToken) =>
        {
            if (!TryValidateWorkspaceKnowledgeRequest(request, out var validationError))
            {
                return validationError;
            }

            var snapshot = request.Snapshot!;
            var knowledgeInput = CreateWorkspaceKnowledgeInput(request);

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

    private static bool TryValidateWorkspaceKnowledgeRequest(
        WorkspaceKnowledgeRequest request,
        out IResult? validationError)
    {
        validationError = null;

        if (request.Snapshot is null)
        {
            validationError = Results.BadRequest("A workspace snapshot is required to calculate live knowledge.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Snapshot.SelectedEnterprise))
        {
            validationError = Results.BadRequest("A workspace enterprise is required to calculate live knowledge.");
            return false;
        }

        if (request.Snapshot.SelectedFiscalYear <= 0)
        {
            validationError = Results.BadRequest("A valid fiscal year is required to calculate live knowledge.");
            return false;
        }

        return true;
    }

    private static WorkspaceKnowledgeInput CreateWorkspaceKnowledgeInput(WorkspaceKnowledgeRequest request)
    {
        var snapshot = request.Snapshot!;
        return new WorkspaceKnowledgeInput(
            snapshot.SelectedEnterprise,
            snapshot.SelectedFiscalYear,
            snapshot.CurrentRate ?? 0m,
            snapshot.TotalCosts ?? 0m,
            snapshot.ProjectedVolume ?? 0m,
            snapshot.ScenarioItems?.Sum(item => item.Cost) ?? 0m,
            Math.Clamp(request.TopVarianceCount, 1, 20),
            Math.Clamp(request.ForecastYears, 1, 10));
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
        app.MapPost("/api/workspace/snapshot", MapWorkspaceSnapshotSaveAsync);
    }

    private static async Task<IResult> MapWorkspaceSnapshotSaveAsync(
        WorkspaceBootstrapData request,
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        if (!TryValidateWorkspaceSnapshotSaveRequest(request, out var validationError))
        {
            return validationError!;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var savedAt = DateTimeOffset.UtcNow;
        var snapshot = CreateWorkspaceSnapshotSaveRecord(request, savedAt);

        context.BudgetSnapshots.Add(snapshot);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Created($"/api/workspace/snapshot/{snapshot.Id}", BuildWorkspaceSnapshotSaveResponse(snapshot));
    }

    private static bool TryValidateWorkspaceSnapshotSaveRequest(WorkspaceBootstrapData request, out IResult? validationError)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedEnterprise))
        {
            validationError = Results.BadRequest("An enterprise name is required to save a snapshot.");
            return false;
        }

        if (request.SelectedFiscalYear <= 0)
        {
            validationError = Results.BadRequest("A valid fiscal year is required to save a snapshot.");
            return false;
        }

        validationError = null;
        return true;
    }

    private static BudgetSnapshot CreateWorkspaceSnapshotSaveRecord(WorkspaceBootstrapData request, DateTimeOffset savedAt)
    {
        return new BudgetSnapshot
        {
            SnapshotName = $"{request.SelectedEnterprise} FY{request.SelectedFiscalYear} rate snapshot",
            SnapshotDate = DateOnly.FromDateTime(savedAt.UtcDateTime),
            CreatedAt = savedAt,
            Notes = $"{RateSnapshotRecordPrefix}; Enterprise: {request.SelectedEnterprise}; FY: {request.SelectedFiscalYear}; Current rate: {request.CurrentRate:0.##}; Total costs: {request.TotalCosts:0.##}; Projected volume: {request.ProjectedVolume:0.##}",
            Payload = JsonSerializer.Serialize(request)
        };
    }

    private static WorkspaceSnapshotSaveResponse BuildWorkspaceSnapshotSaveResponse(BudgetSnapshot snapshot)
    {
        return new WorkspaceSnapshotSaveResponse(
            snapshot.Id,
            snapshot.SnapshotName,
            snapshot.CreatedAt.ToString("O"));
    }

    private static void MapWorkspaceBaselinePutEndpoint(WebApplication app)
    {
        app.MapPut("/api/workspace/baseline", MapWorkspaceBaselineUpdateAsync);
    }

    private static async Task<IResult> MapWorkspaceBaselineUpdateAsync(
        WorkspaceBaselineUpdateRequest request,
        IDbContextFactory<AppDbContext> contextFactory,
        WorkspaceSnapshotComposer composer,
        CancellationToken cancellationToken)
    {
        if (!TryValidateWorkspaceBaselineUpdateRequest(request, out var validationError))
        {
            return validationError!;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var enterprise = await LoadWorkspaceBaselineEnterpriseAsync(context, request.SelectedEnterprise, cancellationToken).ConfigureAwait(false);
        if (enterprise == null)
        {
            return Results.NotFound($"Enterprise '{request.SelectedEnterprise}' was not found.");
        }

        ApplyWorkspaceBaselineUpdate(enterprise, request);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var snapshot = await composer.BuildAsync(request.SelectedEnterprise, request.SelectedFiscalYear, cancellationToken).ConfigureAwait(false);
        return Results.Ok(BuildWorkspaceBaselineUpdateResponse(snapshot));
    }

    private static bool TryValidateWorkspaceBaselineUpdateRequest(WorkspaceBaselineUpdateRequest request, out IResult? validationError)
    {
        var validationMessage = TryGetWorkspaceBaselineValidationMessage(request);
        if (validationMessage is not null)
        {
            validationError = Results.BadRequest(validationMessage);
            return false;
        }

        validationError = null;
        return true;
    }

    private static string? TryGetWorkspaceBaselineValidationMessage(WorkspaceBaselineUpdateRequest request)
    {
        foreach (var validationRule in WorkspaceBaselineValidationRules)
        {
            var validationMessage = validationRule(request);
            if (validationMessage is not null)
            {
                return validationMessage;
            }
        }

        return null;
    }

    private static async Task<Enterprise?> LoadWorkspaceBaselineEnterpriseAsync(
        AppDbContext context,
        string selectedEnterprise,
        CancellationToken cancellationToken)
    {
        return await context.Enterprises
            .FirstOrDefaultAsync(item => !item.IsDeleted && item.Name == selectedEnterprise, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ApplyWorkspaceBaselineUpdate(Enterprise enterprise, WorkspaceBaselineUpdateRequest request)
    {
        enterprise.CurrentRate = decimal.Round(request.CurrentRate, 2, MidpointRounding.AwayFromZero);
        enterprise.MonthlyExpenses = decimal.Round(request.TotalCosts, 2, MidpointRounding.AwayFromZero);
        enterprise.CitizenCount = Math.Max(1, decimal.ToInt32(decimal.Round(request.ProjectedVolume, 0, MidpointRounding.AwayFromZero)));
        enterprise.LastModified = DateTime.UtcNow;
    }

    private static WorkspaceBaselineUpdateResponse BuildWorkspaceBaselineUpdateResponse(WorkspaceBootstrapData snapshot)
    {
        var savedAtUtc = DateTime.UtcNow.ToString("O");
        return new WorkspaceBaselineUpdateResponse(
            snapshot.SelectedEnterprise,
            snapshot.SelectedFiscalYear,
            savedAtUtc,
            $"Saved baseline values for {snapshot.SelectedEnterprise} FY {snapshot.SelectedFiscalYear}.",
            snapshot);
    }

    private static void MapWorkspaceScenarioListEndpoint(WebApplication app)
    {
        app.MapGet("/api/workspace/scenarios", MapWorkspaceScenarioListAsync);
    }

    private static async Task<IResult> MapWorkspaceScenarioListAsync(
        string? enterprise,
        int? fiscalYear,
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var snapshots = await LoadWorkspaceScenarioSnapshotsAsync(context, cancellationToken).ConfigureAwait(false);
        var scenarios = await BuildWorkspaceScenarioSummariesAsync(snapshots, enterprise, fiscalYear).ConfigureAwait(false);

        return Results.Ok(new WorkspaceScenarioCollectionResponse(scenarios));
    }

    private static async Task<List<BudgetSnapshot>> LoadWorkspaceScenarioSnapshotsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        return await context.BudgetSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.Notes != null && snapshot.Notes.Contains(ScenarioRecordPrefix))
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static Task<List<WorkspaceScenarioSummaryResponse>> BuildWorkspaceScenarioSummariesAsync(
        IEnumerable<BudgetSnapshot> snapshots,
        string? enterprise,
        int? fiscalYear)
    {
        var scenarios = new List<WorkspaceScenarioSummaryResponse>();
        foreach (var snapshot in snapshots)
        {
            if (TryBuildWorkspaceScenarioSummary(snapshot, enterprise, fiscalYear, out var scenario))
            {
                scenarios.Add(scenario);
            }
        }

        return Task.FromResult(scenarios);
    }

    private static bool TryBuildWorkspaceScenarioSummary(
        BudgetSnapshot snapshot,
        string? enterprise,
        int? fiscalYear,
        out WorkspaceScenarioSummaryResponse summary)
    {
        summary = default!;

        var payload = TryDeserializeBootstrap(snapshot.Payload);
        if (payload == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(enterprise)
            && !string.Equals(payload.SelectedEnterprise, enterprise, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (fiscalYear is > 0 && payload.SelectedFiscalYear != fiscalYear.Value)
        {
            return false;
        }

        summary = BuildScenarioSummary(snapshot, payload);
        return true;
    }

    private static void MapWorkspaceScenarioGetEndpoint(WebApplication app)
    {
        app.MapGet("/api/workspace/scenarios/{snapshotId:long}", MapWorkspaceScenarioGetAsync);
    }

    private static async Task<IResult> MapWorkspaceScenarioGetAsync(
        long snapshotId,
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var snapshot = await LoadWorkspaceScenarioSnapshotAsync(context, snapshotId, cancellationToken).ConfigureAwait(false);
        return BuildWorkspaceScenarioResponse(snapshot);
    }

    private static async Task<BudgetSnapshot?> LoadWorkspaceScenarioSnapshotAsync(
        AppDbContext context,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        return await context.BudgetSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == snapshotId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static IResult BuildWorkspaceScenarioResponse(BudgetSnapshot? snapshot)
    {
        if (snapshot is null || !IsWorkspaceScenarioSnapshot(snapshot))
        {
            return Results.NotFound();
        }

        return BuildWorkspaceScenarioPayloadResponse(snapshot.Payload);
    }

    private static bool IsWorkspaceScenarioSnapshot(BudgetSnapshot snapshot)
    {
        return snapshot.Notes?.Contains(ScenarioRecordPrefix, StringComparison.Ordinal) == true;
    }

    private static IResult BuildWorkspaceScenarioPayloadResponse(string? payload)
    {
        var bootstrap = TryDeserializeBootstrap(payload);
        return bootstrap == null ? Results.BadRequest("The selected scenario does not contain a valid workspace payload.") : Results.Ok(bootstrap);
    }

    private static void MapWorkspaceScenarioPostEndpoint(WebApplication app)
    {
        app.MapPost("/api/workspace/scenarios", MapWorkspaceScenarioSaveAsync);
    }

    private static async Task<IResult> MapWorkspaceScenarioSaveAsync(
        WorkspaceScenarioSaveRequest request,
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        if (!TryValidateWorkspaceScenarioSaveRequest(request, out var validationError))
        {
            return validationError!;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var savedAt = DateTimeOffset.UtcNow;
        var normalizedScenarioName = request.ScenarioName.Trim();
        var snapshot = CreateWorkspaceScenarioSaveRecord(request, normalizedScenarioName, savedAt);

        context.BudgetSnapshots.Add(snapshot);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var payload = TryDeserializeBootstrap(snapshot.Payload) ?? request.Snapshot!;
        return Results.Created($"/api/workspace/scenarios/{snapshot.Id}", BuildScenarioSummary(snapshot, payload, request.Description));
    }

    private static bool TryValidateWorkspaceScenarioSaveRequest(WorkspaceScenarioSaveRequest request, out IResult? validationError)
    {
        var validationMessage = TryGetWorkspaceScenarioSaveValidationMessage(request);
        if (validationMessage is not null)
        {
            validationError = Results.BadRequest(validationMessage);
            return false;
        }

        validationError = null;
        return true;
    }

    private static string? TryGetWorkspaceScenarioSaveValidationMessage(WorkspaceScenarioSaveRequest request)
    {
        foreach (var validationRule in WorkspaceScenarioSaveValidationRules)
        {
            var validationMessage = validationRule(request);
            if (validationMessage is not null)
            {
                return validationMessage;
            }
        }

        return null;
    }

    private static BudgetSnapshot CreateWorkspaceScenarioSaveRecord(
        WorkspaceScenarioSaveRequest request,
        string normalizedScenarioName,
        DateTimeOffset savedAt)
    {
        return new BudgetSnapshot
        {
            SnapshotName = $"{request.Snapshot!.SelectedEnterprise} FY{request.Snapshot.SelectedFiscalYear} scenario {normalizedScenarioName}",
            SnapshotDate = DateOnly.FromDateTime(savedAt.UtcDateTime),
            CreatedAt = savedAt,
            Notes = BuildScenarioNotes(request, normalizedScenarioName),
            Payload = JsonSerializer.Serialize(request.Snapshot with { ActiveScenarioName = normalizedScenarioName, LastUpdatedUtc = savedAt.ToString("O") })
        };
    }

    private static void MapWorkspaceSnapshotExportsPostEndpoint(WebApplication app)
    {
        app.MapPost("/api/workspace/snapshot/{snapshotId:long}/exports", MapWorkspaceSnapshotExportsPostAsync);
    }

    private static async Task<IResult> MapWorkspaceSnapshotExportsPostAsync(
        long snapshotId,
        WorkspaceSnapshotArtifactRequest? request,
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var snapshot = await LoadWorkspaceSnapshotForExportAsync(context, snapshotId, cancellationToken).ConfigureAwait(false);
        if (!TryValidateWorkspaceSnapshotExport(snapshot, out var validationError))
        {
            return validationError!;
        }

        return await SaveWorkspaceSnapshotExportArtifactsAsync(context, snapshot!, request, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryValidateWorkspaceSnapshotExport(BudgetSnapshot? snapshot, out IResult? validationError)
    {
        if (snapshot is null)
        {
            validationError = Results.NotFound();
            return false;
        }

        if (string.IsNullOrWhiteSpace(snapshot.Payload))
        {
            validationError = Results.BadRequest("The selected snapshot does not contain a payload that can be used to generate exports.");
            return false;
        }

        validationError = null;
        return true;
    }

    private static async Task<IResult> SaveWorkspaceSnapshotExportArtifactsAsync(
        AppDbContext context,
        BudgetSnapshot snapshot,
        WorkspaceSnapshotArtifactRequest? request,
        CancellationToken cancellationToken)
    {
        var documents = CreateWorkspaceSnapshotExportDocuments(snapshot, request);
        return await PersistWorkspaceSnapshotExportArtifactsAsync(context, snapshot, documents, request, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyCollection<WorkspaceSnapshotExportArtifactDocument> CreateWorkspaceSnapshotExportDocuments(
        BudgetSnapshot snapshot,
        WorkspaceSnapshotArtifactRequest? request)
    {
        return WorkspaceSnapshotExportArchiveService.CreateDocuments(snapshot.Payload!, request?.DocumentKinds);
    }

    private static async Task<IResult> PersistWorkspaceSnapshotExportArtifactsAsync(
        AppDbContext context,
        BudgetSnapshot snapshot,
        IReadOnlyCollection<WorkspaceSnapshotExportArtifactDocument> documents,
        WorkspaceSnapshotArtifactRequest? request,
        CancellationToken cancellationToken)
    {
        var normalizedKinds = documents.Select(document => document.DocumentKind).ToHashSet(StringComparer.Ordinal);

        if (request?.ReplaceExisting == true)
        {
            RemoveExistingWorkspaceSnapshotArtifacts(context, snapshot, normalizedKinds);
        }

        var artifacts = CreateWorkspaceSnapshotArtifacts(snapshot, documents);
        context.BudgetSnapshotArtifacts.AddRange(artifacts);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(BuildWorkspaceSnapshotArtifactBatchResponse(snapshot, artifacts));
    }

    private static async Task<BudgetSnapshot?> LoadWorkspaceSnapshotForExportAsync(
        AppDbContext context,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        return await context.BudgetSnapshots
            .Include(item => item.ExportArtifacts)
            .FirstOrDefaultAsync(item => item.Id == snapshotId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void RemoveExistingWorkspaceSnapshotArtifacts(
        AppDbContext context,
        BudgetSnapshot snapshot,
        ISet<string> normalizedKinds)
    {
        var existingArtifacts = snapshot.ExportArtifacts
            .Where(artifact => normalizedKinds.Contains(artifact.DocumentKind))
            .ToList();

        if (existingArtifacts.Count > 0)
        {
            context.BudgetSnapshotArtifacts.RemoveRange(existingArtifacts);
        }
    }

    private static List<BudgetSnapshotArtifact> CreateWorkspaceSnapshotArtifacts(
        BudgetSnapshot snapshot,
        IEnumerable<WorkspaceSnapshotExportArtifactDocument> documents)
    {
        var createdAt = DateTimeOffset.UtcNow;
        return documents.Select(document => new BudgetSnapshotArtifact
        {
            BudgetSnapshotId = snapshot.Id,
            DocumentKind = document.DocumentKind,
            FileName = document.FileName,
            ContentType = document.ContentType,
            SizeBytes = document.Content.LongLength,
            CreatedAt = createdAt,
            Payload = document.Content
        }).ToList();
    }

    private static WorkspaceSnapshotArtifactBatchResponse BuildWorkspaceSnapshotArtifactBatchResponse(
        BudgetSnapshot snapshot,
        IEnumerable<BudgetSnapshotArtifact> artifacts)
    {
        return new WorkspaceSnapshotArtifactBatchResponse(
            snapshot.Id,
            snapshot.SnapshotName,
            artifacts.Select(BuildArtifactSummary).ToList());
    }

    private static void MapWorkspaceSnapshotExportsGetEndpoint(WebApplication app)
    {
        app.MapGet("/api/workspace/snapshot/{snapshotId:long}/exports", MapWorkspaceSnapshotExportsGetAsync);
    }

    private static async Task<IResult> MapWorkspaceSnapshotExportsGetAsync(
        long snapshotId,
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var snapshot = await LoadWorkspaceSnapshotExportSummaryAsync(context, snapshotId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return Results.NotFound();
        }

        var response = await BuildWorkspaceSnapshotArtifactBatchResponseAsync(context, snapshot, cancellationToken).ConfigureAwait(false);
        return Results.Ok(response);
    }

    private static async Task<BudgetSnapshot?> LoadWorkspaceSnapshotExportSummaryAsync(
        AppDbContext context,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        return await context.BudgetSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == snapshotId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<WorkspaceSnapshotArtifactBatchResponse> BuildWorkspaceSnapshotArtifactBatchResponseAsync(
        AppDbContext context,
        BudgetSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var artifacts = await LoadWorkspaceSnapshotArtifactsAsync(context, snapshot.Id, cancellationToken).ConfigureAwait(false);
        return BuildWorkspaceSnapshotArtifactBatchResponse(snapshot, artifacts);
    }

    private static async Task<List<BudgetSnapshotArtifact>> LoadWorkspaceSnapshotArtifactsAsync(
        AppDbContext context,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        return await context.BudgetSnapshotArtifacts
            .AsNoTracking()
            .Where(item => item.BudgetSnapshotId == snapshotId)
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
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
        var scenarioName = ResolveScenarioName(snapshot, payload);
        var description = ResolveScenarioDescription(snapshot, descriptionOverride);

        return new WorkspaceScenarioSummaryResponse(
            snapshot.Id,
            scenarioName,
            payload.SelectedEnterprise,
            payload.SelectedFiscalYear,
            snapshot.CreatedAt.ToString("O"),
            payload.CurrentRate,
            payload.TotalCosts,
            payload.ProjectedVolume,
            GetScenarioItemTotalCost(payload),
            GetScenarioItemCount(payload),
            description);
    }

    private static string ResolveScenarioName(BudgetSnapshot snapshot, WorkspaceBootstrapData payload)
    {
        return string.IsNullOrWhiteSpace(payload.ActiveScenarioName)
            ? snapshot.SnapshotName
            : payload.ActiveScenarioName;
    }

    private static string? ResolveScenarioDescription(BudgetSnapshot snapshot, string? descriptionOverride)
    {
        if (!string.IsNullOrWhiteSpace(descriptionOverride))
        {
            return descriptionOverride;
        }

        return string.IsNullOrWhiteSpace(snapshot.Notes)
            ? null
            : ExtractDescription(snapshot.Notes);
    }

    private static decimal GetScenarioItemTotalCost(WorkspaceBootstrapData payload)
    {
        return payload.ScenarioItems?.Sum(item => item.Cost) ?? 0m;
    }

    private static int GetScenarioItemCount(WorkspaceBootstrapData payload)
    {
        return payload.ScenarioItems?.Count ?? 0;
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
