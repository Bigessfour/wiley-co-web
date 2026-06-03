#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace WileyWidget.Data
{
    /// <summary>
    /// Application DbContext factory. Provides IDbContextFactory<AppDbContext> using
    /// configured DbContextOptions for PostgreSQL (default), SQLite, or InMemory (degraded) with fallback.
    /// Provider selected via Database:Provider config (or inferred from connection string).
    /// Npgsql path remains default/unchanged for full backward compatibility.
    /// </summary>
    public sealed class AppDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext>? _options;
        private readonly IConfiguration? _configuration;
        private readonly Lazy<DbContextOptions<AppDbContext>> _lazyOptions;

        /// <summary>
        /// Constructor for pre-configured options (preferred path).
        /// </summary>
        public AppDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _lazyOptions = new Lazy<DbContextOptions<AppDbContext>>(() => _options);
        }

        /// <summary>
        /// Constructor with IConfiguration fallback for early registration scenarios.
        /// </summary>
        public AppDbContextFactory(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _lazyOptions = new Lazy<DbContextOptions<AppDbContext>>(BuildOptionsFromConfiguration);
        }

        /// <summary>
        /// Constructor with both options and configuration for maximum resilience.
        /// Prefers options, falls back to configuration if options are not provided.
        /// </summary>
        public AppDbContextFactory(DbContextOptions<AppDbContext>? options, IConfiguration? configuration)
        {
            if (options == null && configuration == null)
            {
                throw new ArgumentException("At least one of options or configuration must be provided.");
            }

            _options = options;
            _configuration = configuration;
            _lazyOptions = new Lazy<DbContextOptions<AppDbContext>>(() => _options ?? BuildOptionsFromConfiguration());
        }

        private string GetConfiguredProvider()
        {
            if (_configuration == null)
            {
                return "PostgreSQL";
            }
            var provider = _configuration.GetValue<string>("Database:Provider")
                ?? _configuration.GetValue<string>("Database__Provider");
            if (string.IsNullOrWhiteSpace(provider))
            {
                return "PostgreSQL"; // default preserves all existing Npgsql/Aurora/local dev behavior
            }
            return provider.Trim();
        }

        private DbContextOptions<AppDbContext> BuildOptionsFromConfiguration()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("Cannot build DbContextOptions without IConfiguration.");
            }

            try
            {
                // Global degraded-mode enforcement
                if (AppDbStartupState.IsDegradedMode)
                {
                    var degradedDbName = _configuration.GetValue<string>("Database:DegradedModeName") ?? "WileyWidget_Degraded";
                    Log.Warning("[DB_FACTORY] Degraded mode active - using InMemory provider (db='{DbName}')", degradedDbName);
                    return new DbContextOptionsBuilder<AppDbContext>()
                        .UseInMemoryDatabase(degradedDbName)
                        .Options;
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = _configuration["ConnectionStrings:DefaultConnection"]
                        ?? _configuration["DATABASE_URL"]
                        ?? Environment.GetEnvironmentVariable("DATABASE_URL");
                }

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException("DefaultConnection is not configured for the selected database provider (see Database:Provider).");
                }

                // Expand environment variables (supports %LOCALAPPDATA% etc for SQLite paths)
                connectionString = Environment.ExpandEnvironmentVariables(connectionString);

                var provider = GetConfiguredProvider();
                var isSqliteByConn = connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
                                     || connectionString.StartsWith("Filename=", StringComparison.OrdinalIgnoreCase);
                var builder = new DbContextOptionsBuilder<AppDbContext>();

                if (provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase) || isSqliteByConn)
                {
                    builder.UseSqlite(connectionString, sqlite =>
                    {
                        // SQLite has no direct CommandTimeout equivalent on the extension in same way; rely on EF defaults or connection
                    });
                    builder.EnableDetailedErrors();
                    builder.EnableSensitiveDataLogging(_configuration.GetValue<bool>("Database:EnableSensitiveDataLogging", false));
                    Log.Information("Built DbContextOptions for SQLite (provider={Provider}, conn inferred or explicit)", provider);
                }
                else
                {
                    // Default / explicit PostgreSQL path (unchanged behavior for Aurora, local postgres, existing configs)
                    builder.UseNpgsql(connectionString, npgsql =>
                    {
                        npgsql.CommandTimeout(_configuration.GetValue<int>("Database:CommandTimeoutSeconds", 30));
                    });
                    builder.EnableDetailedErrors();
                    builder.EnableSensitiveDataLogging(_configuration.GetValue<bool>("Database:EnableSensitiveDataLogging", false));
                    Log.Information("Built DbContextOptions for PostgreSQL (provider={Provider})", provider);
                }

                return builder.Options;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to build DbContextOptions from configuration");

                // Optional in-memory fallback on configuration failure
                if (_configuration.GetValue<bool>("Database:EnableInMemoryFallback", false))
                {
                    AppDbStartupState.ActivateFallback("Factory configuration failure");
                    var degradedDbName = _configuration.GetValue<string>("Database:DegradedModeName") ?? "WileyWidget_Degraded";
                    Log.Warning("[DB_FACTORY] Activating InMemory fallback after configuration failure (db='{DbName}')", degradedDbName);
                    return new DbContextOptionsBuilder<AppDbContext>()
                        .UseInMemoryDatabase(degradedDbName)
                        .Options;
                }

                throw;
            }
        }

        public AppDbContext CreateDbContext()
        {
#pragma warning disable CA2000
            return new AppDbContext(_lazyOptions.Value);
#pragma warning restore CA2000
        }

        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000
            return new ValueTask<AppDbContext>(new AppDbContext(_lazyOptions.Value));
#pragma warning restore CA2000
        }
    }
}
