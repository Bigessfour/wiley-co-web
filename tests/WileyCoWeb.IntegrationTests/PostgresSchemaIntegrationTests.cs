using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using WileyWidget.Data;

namespace WileyCoWeb.IntegrationTests;

/// <summary>
/// Optional PostgreSQL smoke tests — run when RUN_POSTGRES_TESTS=true.
/// </summary>
[Trait("Category", "Postgres")]
public sealed class PostgresSchemaIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    [Fact]
    public async Task Migrations_ApplySuccessfully_OnPostgreSql()
    {
        if (!ShouldRunPostgresTests())
        {
            return;
        }

        var connectionString = _container!.GetConnectionString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.MigrateAsync();

        Assert.True(await context.Database.CanConnectAsync());

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await AssertColumnAbsentAsync(connection, "Charges", "BillId");
        await AssertColumnPresentAsync(connection, "Charges", "UtilityBillId");
        await AssertColumnAbsentAsync(connection, "BudgetInteraction", "EnterpriseId");
        await AssertColumnPresentAsync(connection, "UtilityCustomers", "EnterpriseId");
        await AssertIndexPresentAsync(connection, "IX_AuditEntries_Timestamp");
        await AssertIndexPresentAsync(connection, "IX_ledger_entries_entry_date");
        await AssertIndexPresentAsync(connection, "IX_ledger_entries_entry_scope");
        await AssertIndexPresentAsync(connection, "IX_BudgetEntries_SourceFilePath");
        await AssertTablePresentAsync(connection, "ApartmentUnitTypes");
        await AssertTablePresentAsync(connection, "quickbooks_routing_rules");
    }

    public async Task InitializeAsync()
    {
        if (!ShouldRunPostgresTests())
        {
            return;
        }

        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private static bool ShouldRunPostgresTests()
        => string.Equals(Environment.GetEnvironmentVariable("RUN_POSTGRES_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    private static async Task AssertColumnAbsentAsync(NpgsqlConnection connection, string tableName, string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = @table
              AND column_name = @column;
            """;
        command.Parameters.AddWithValue("table", tableName);
        command.Parameters.AddWithValue("column", columnName);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        Assert.Equal(0, count);
    }

    private static async Task AssertColumnPresentAsync(NpgsqlConnection connection, string tableName, string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = @table
              AND column_name = @column;
            """;
        command.Parameters.AddWithValue("table", tableName);
        command.Parameters.AddWithValue("column", columnName);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        Assert.Equal(1, count);
    }

    private static async Task AssertIndexPresentAsync(NpgsqlConnection connection, string indexName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname = @indexName;
            """;
        command.Parameters.AddWithValue("indexName", indexName);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        Assert.Equal(1, count);
    }

    private static async Task AssertTablePresentAsync(NpgsqlConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name = @tableName;
            """;
        command.Parameters.AddWithValue("tableName", tableName);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        Assert.Equal(1, count);
    }
}
