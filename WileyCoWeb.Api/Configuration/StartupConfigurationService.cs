using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using WileyWidget.Services.Logging;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WileyWidget.Data;

namespace WileyCoWeb.Api.Configuration;

public static class StartupConfigurationService
{
    public static void ConfigureLogging(WebApplicationBuilder builder)
    {
        ConfigureApiLogging(builder.Logging, !builder.Environment.IsEnvironment("IntegrationTest"));
    }

    public static Task TryActivateDegradedModeForUnavailableDevelopmentDatabaseAsync(
        string? configuredConnectionString,
        bool allowDegradedStartup,
        IWebHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
        => TryActivateDevelopmentDatabaseCoreAsync(configuredConnectionString, allowDegradedStartup, environment, logger, cancellationToken);

    public static void ConfigureApiLogging(ILoggingBuilder logging, bool includeWorkspaceFileLogger)
        => ConfigureApiLoggingCore(logging, includeWorkspaceFileLogger);

    private static async Task TryActivateDevelopmentDatabaseCoreAsync(
        string? configuredConnectionString,
        bool allowDegradedStartup,
        IWebHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!CanProbeDevelopmentDatabase(allowDegradedStartup, configuredConnectionString, environment))
        {
            return;
        }

        const string fallbackReason = "Configured development database was unreachable during startup.";

        if (await TryValidateDevelopmentDatabaseConnectionAsync(configuredConnectionString!, logger, fallbackReason, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        ActivateDegradedMode(logger, fallbackReason);
    }

    private static Task<bool> TryValidateDevelopmentDatabaseConnectionAsync(
        string configuredConnectionString,
        ILogger logger,
        string fallbackReason,
        CancellationToken cancellationToken)
        => TryValidateDevelopmentDatabaseConnectionCoreAsync(configuredConnectionString, logger, fallbackReason, cancellationToken);

    private static async Task<bool> TryValidateDevelopmentDatabaseConnectionCoreAsync(
        string configuredConnectionString,
        ILogger logger,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        try
        {
            return await TryConnectToDevelopmentDatabaseAsync(configuredConnectionString, logger, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ActivateDegradedMode(logger, fallbackReason, ex);
            return true;
        }
    }

    private static async Task<bool> TryConnectToDevelopmentDatabaseAsync(
        string configuredConnectionString,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!await CanConnectToDevelopmentDatabaseAsync(configuredConnectionString, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        logger.LogInformation("Workspace API validated the configured development database before service registration.");
        return true;
    }

    private static void ConfigureApiLoggingCore(ILoggingBuilder logging, bool includeWorkspaceFileLogger)
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Debug);
        ConfigureConsoleLogging(logging);
        ConfigureLoggingFilters(logging);
        ConfigureWorkspaceFileLogger(logging, includeWorkspaceFileLogger);
    }

    private static string BuildConnectivityProbeConnectionString(string configuredConnectionString)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(Environment.ExpandEnvironmentVariables(configuredConnectionString))
        {
            Pooling = false
        };

        connectionStringBuilder.Timeout = NormalizeProbeTimeout(connectionStringBuilder.Timeout);
        connectionStringBuilder.CommandTimeout = NormalizeProbeCommandTimeout(connectionStringBuilder.CommandTimeout);

        return connectionStringBuilder.ConnectionString;
    }

    private static bool CanProbeDevelopmentDatabase(bool allowDegradedStartup, string? configuredConnectionString, IWebHostEnvironment environment)
    {
        if (!IsDevelopmentDatabaseProbeEnabled(allowDegradedStartup, environment))
        {
            return false;
        }

        if (AppDbStartupState.IsDegradedMode)
        {
            return false;
        }

        return HasConfiguredConnectionString(configuredConnectionString);
    }

    private static bool IsDevelopmentDatabaseProbeEnabled(bool allowDegradedStartup, IWebHostEnvironment environment)
        => allowDegradedStartup && environment.IsDevelopment();

    private static bool HasConfiguredConnectionString(string? configuredConnectionString)
        => !string.IsNullOrWhiteSpace(configuredConnectionString);

    private static async Task<bool> CanConnectToDevelopmentDatabaseAsync(string configuredConnectionString, CancellationToken cancellationToken)
    {
        var options = CreateDevelopmentDatabaseProbeOptions(configuredConnectionString);
        await using var context = new AppDbContext(options);
        return await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DbContextOptions<AppDbContext> CreateDevelopmentDatabaseProbeOptions(string configuredConnectionString)
    {
        var probeConnectionString = BuildConnectivityProbeConnectionString(configuredConnectionString);
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(probeConnectionString)
            .Options;
    }

    private static void ActivateDegradedMode(ILogger logger, string fallbackReason, Exception? exception = null)
    {
        if (exception is null)
        {
            logger.LogWarning("Workspace API could not connect to the configured development database. Activating degraded mode.");
        }
        else
        {
            logger.LogWarning(exception, "Workspace API could not connect to the configured development database. Activating degraded mode.");
        }

        AppDbStartupState.ActivateFallback(fallbackReason);
    }

    private static void ConfigureConsoleLogging(ILoggingBuilder logging)
    {
        logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "HH:mm:ss.fff ";
            options.SingleLine = true;
            options.IncludeScopes = false;
        });
    }

    private static void ConfigureLoggingFilters(ILoggingBuilder logging)
    {
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
    }

    private static void ConfigureWorkspaceFileLogger(ILoggingBuilder logging, bool includeWorkspaceFileLogger)
    {
        if (includeWorkspaceFileLogger)
        {
            logging.AddProvider(new WorkspaceFileLoggerProvider("wiley-widget.log"));
        }
    }

    private static int NormalizeProbeTimeout(int timeout)
        => timeout <= 0 || timeout > 5 ? 5 : timeout;

    private static int NormalizeProbeCommandTimeout(int timeout)
        => timeout <= 0 || timeout > 5 ? 5 : timeout;
}