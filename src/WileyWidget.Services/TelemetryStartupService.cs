using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using Serilog;

namespace WileyWidget.Services.Telemetry;

/// <summary>
/// Startup service for telemetry initialization and database health checks.
/// Performs DB connectivity validation during startup.
/// </summary>
public sealed class TelemetryStartupService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public TelemetryStartupService(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("Telemetry startup service initializing...");

            using var scope = _serviceProvider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var context = factory.CreateDbContext();
            var metrics = scope.ServiceProvider.GetService<WileyWidget.Services.ApplicationMetricsService>();

            // Apply EF Core migrations only when explicitly enabled. The production database is already provisioned.
            var migrationSw = Stopwatch.StartNew();
            var migrationSuccess = false;
            try
            {
                if (_configuration.GetValue<bool>("Database:ApplyMigrations"))
                {
                    await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                    migrationSuccess = true;
                    Log.Information("Database migrations applied successfully");
                }
                else
                {
                    Log.Information("Database migrations skipped because Database:ApplyMigrations is disabled");
                }
            }
            catch (Exception migEx)
            {
                Log.Warning(migEx, "Database migration step encountered an issue - database may already be current");
            }
            finally
            {
                migrationSw.Stop();
                metrics?.RecordMigration(migrationSw.Elapsed.TotalMilliseconds, migrationSuccess);
                metrics?.RecordSeeding(migrationSuccess);
            }

            // Confirm connectivity after migration
            await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            Log.Information("Database connectivity validated during telemetry startup");
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Telemetry startup cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during telemetry startup");
            // Don't throw to avoid crashing startup
        }

        Log.Information("Telemetry pipeline initialized successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.Information("Telemetry pipeline shutdown initiated.");
        return Task.CompletedTask;
    }
}
