using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Amplify;
using Xunit;

namespace WileyWidget.Tests.Database;

/// <summary>
/// Minimal unit tests for provider selection in AppDbContextFactory (Slice A).
/// Defaults to PostgreSQL for backward compat. SQLite support added for local machine / no-Docker scenarios.
/// These are light (no full DB roundtrip yet — see Slice E HighRisk for that).
/// </summary>
public class AppDbContextFactoryProviderTests
{
    [Fact]
    public void Constructor_WithConfig_DefaultsToPostgreSql_WhenNoExplicitProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=postgres;Password=postgres"
            })
            .Build();

        // Should not throw; builds Npgsql options under the hood (default)
        var factory = new AppDbContextFactory(config);
        // Lazy build happens on first Create; force it
        using var ctx = factory.CreateDbContext();
        // Provider name will be "Npgsql" for default path
        Assert.Contains("Npgsql", ctx.Database.ProviderName ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_WithConfig_UsesSqlite_WhenProviderSetOrDataSourceConn()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SQLite",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:"
            })
            .Build();

        var factory = new AppDbContextFactory(config);
        using var ctx = factory.CreateDbContext();
        Assert.Contains("Sqlite", ctx.Database.ProviderName ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_WithConfig_UsesSqlite_WhenConnStringInfersSqlite_EvenWithoutProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // No explicit Provider — infer from conn (Data Source=)
                ["ConnectionStrings:DefaultConnection"] = "Data Source=%LOCALAPPDATA%\\WileyWidget\\test-infer.db"
            })
            .Build();

        var factory = new AppDbContextFactory(config);
        using var ctx = factory.CreateDbContext();
        Assert.Contains("Sqlite", ctx.Database.ProviderName ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "HighRisk")]
    public void SQLiteProvider_EnsureCreatedAndRoundtrip_SupportsSpecialColumns_JsonbByteaTimestampRowVersion()
    {
        // Arrange: SQLite :memory: to prove model conditionals (no bytea/jsonb/timestamptz errors)
        // and roundtrip for key payload entities used in snapshots/exports/Jarvis etc.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SQLite",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:"
            })
            .Build();

        var factory = new AppDbContextFactory(config);
        using var ctx = factory.CreateDbContext();

        // For SQLite :memory:, must OpenConnection() to keep the DB alive across Ensure/ops (classic EF gotcha; each conn is isolated DB otherwise)
        ctx.Database.OpenConnection();

        // Act: EnsureCreated builds schema using current (conditional) model for SQLite
        ctx.Database.EnsureCreated();

        // Diagnostic: list created tables to debug why budget_snapshots missing (EnsureCreated succeeded but table not?)
        var createdTables = new List<string>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read()) createdTables.Add(rdr.GetString(0));
        }
        var modelTables = ctx.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .OrderBy(n => n)
            .ToList();
        Console.WriteLine($"[DEBUG SQLITE MODEL TABLES count={modelTables.Count}]: {string.Join(", ", modelTables.Take(20))}{(modelTables.Count > 20 ? "..." : "")}");

        if (!createdTables.Contains("budget_snapshots"))
        {
            throw new Exception($"budget_snapshots NOT in SQLite schema after EnsureCreated. Tables from sqlite_master: [{string.Join(", ", createdTables)}]. Model tables sample: [{string.Join(", ", modelTables.Take(10))}]");
        }

        // Stronger proof for original Verification checklist item: "EnsureCreatedAsync creates all QB + Amplify + workspace tables"
        // (critical for local SQLite machine mode). Core tables exercised by snapshots, QB import/dedup/routing, Jarvis history, customers, etc.
        // Table names taken from actual sqlite_master + model output in this test (mixed casing / EF conventions).
        var expectedCoreTables = new[]
        {
            "Enterprises", "enterprises",
            "UtilityCustomers", "customers",
            "budget_snapshots", "budget_snapshot_artifacts",
            "ConversationHistories",
            "import_batches", "source_files", "source_file_variants",
            "quickbooks_allocation_profiles", "quickbooks_routing_rules",
            "ActivityLog", "FiscalYearSettings"  // representative sample; full model has ~30+ (see [DEBUG SQLITE MODEL TABLES] log)
        };
        var missing = expectedCoreTables.Where(t => !createdTables.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Any())
        {
            throw new Exception($"EnsureCreated missing core tables for checklist: [{string.Join(", ", missing)}]. sqlite_master: [{string.Join(", ", createdTables)}]");
        }

        // Insert minimal BudgetSnapshot (uses CreatedAt timestamp + Payload json string)
        var snapshot = new BudgetSnapshot
        {
            SnapshotName = "Test Snapshot for SQLite",
            CreatedAt = DateTimeOffset.UtcNow,
            Payload = "{\"test\":\"json payload for sqlite roundtrip\",\"value\":42}"
        };
        ctx.BudgetSnapshots.Add(snapshot);
        ctx.SaveChanges();

        // Insert Artifact with byte[] Payload (binary export artifact)
        var artifact = new BudgetSnapshotArtifact
        {
            BudgetSnapshotId = snapshot.Id,
            DocumentKind = "test-export",
            FileName = "test.xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            CreatedAt = DateTimeOffset.UtcNow,
            Payload = Encoding.UTF8.GetBytes("fake binary payload data for SQLite test")
        };
        ctx.BudgetSnapshotArtifacts.Add(artifact);
        ctx.SaveChanges();

        // Also test a RowVersion entity (Enterprise)
        var enterprise = new Enterprise
        {
            Name = "Test Enterprise SQLite",
            Type = "Water Utility",
            CurrentRate = 10.5m,
            MonthlyExpenses = 1000m,
            CitizenCount = 50
            // RowVersion will be set by interceptor or default
        };
        ctx.Enterprises.Add(enterprise);
        ctx.SaveChanges();

        // Assert: roundtrip succeeds, data matches (proves column mappings work for SQLite)
        var loadedSnap = ctx.BudgetSnapshots.AsNoTracking().First(s => s.Id == snapshot.Id);
        Assert.Equal(snapshot.Payload, loadedSnap.Payload);
        Assert.True(loadedSnap.CreatedAt > DateTimeOffset.MinValue);

        var loadedArt = ctx.BudgetSnapshotArtifacts.AsNoTracking().First(a => a.Id == artifact.Id);
        Assert.Equal(artifact.Payload, loadedArt.Payload);

        var loadedEnt = ctx.Enterprises.AsNoTracking().First(e => e.Id == enterprise.Id);
        Assert.Equal(enterprise.Name, loadedEnt.Name);
        Assert.NotNull(loadedEnt.RowVersion); // concurrency token present

        // Also basic ensure no crash on other timestamp entities
        ctx.ActivityLogs.Add(new ActivityLog { Activity = "sqlite-test", Timestamp = DateTime.UtcNow });
        ctx.SaveChanges();

        // Note: QB routing/dedup tables + CRUD covered conceptually by model (DbSets + OnModelCreating ensure indexes/FKs) + guard skip for !Npgsql (C) + agnostic helper (D).
        // Core B roundtrips for special columns (Payloads, RowVersion, timestamps) + EnsureCreated proven above. Full service dedup in other HighRisk.
    }

    [Fact]
    [Trait("Category", "HighRisk")]
    public void SQLiteProvider_UtilityCustomer_CRUD_Roundtrip()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SQLite",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:"
            })
            .Build();

        var factory = new AppDbContextFactory(config);
        using var ctx = factory.CreateDbContext();
        ctx.Database.OpenConnection();
        ctx.Database.EnsureCreated();

        // Need Enterprise first (FK)
        var ent = new Enterprise { Name = "Test Util Cust Ent", Type = "Water Utility", CurrentRate = 5m, MonthlyExpenses = 100m, CitizenCount = 10 };
        ctx.Enterprises.Add(ent);
        ctx.SaveChanges();

        var cust = new UtilityCustomer
        {
            EnterpriseId = ent.Id,
            AccountNumber = "SQL-001",
            FirstName = "SQLite",
            LastName = "Test Customer"
        };
        ctx.UtilityCustomers.Add(cust);
        ctx.SaveChanges();

        var loaded = ctx.UtilityCustomers.AsNoTracking().First(c => c.AccountNumber == "SQL-001");
        Assert.Equal("SQL-001", loaded.AccountNumber);
        Assert.Contains("SQLite", loaded.FullName);
    }

    [Fact]
    [Trait("Category", "HighRisk")]
    public void SQLiteProvider_ConversationHistory_Persist_AcrossRestart()
    {
        string dbFile = Path.Combine(Path.GetTempPath(), $"test-convo-{Guid.NewGuid()}.db");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "SQLite",
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={dbFile}"
                })
                .Build();

            // First "session"
            var factory1 = new AppDbContextFactory(config);
            using (var ctx1 = factory1.CreateDbContext())
            {
                ctx1.Database.OpenConnection();
                ctx1.Database.EnsureCreated();

                var convo = new global::WileyWidget.Services.Abstractions.ConversationHistory
                {
                    ConversationId = "conv-sqlite-123",
                    Title = "Test Persist",
                    MessagesJson = "[{\"role\":\"user\",\"content\":\"hello sqlite\"}]",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                ctx1.ConversationHistories.Add(convo);
                ctx1.SaveChanges();
            }

            // New "session" / restart - new context on same file
            var factory2 = new AppDbContextFactory(config);
            using var ctx2 = factory2.CreateDbContext();
            ctx2.Database.OpenConnection();
            var loaded = ctx2.ConversationHistories.AsNoTracking().FirstOrDefault(c => c.ConversationId == "conv-sqlite-123");
            Assert.NotNull(loaded);
            Assert.Equal("Test Persist", loaded.Title);
            Assert.Contains("hello sqlite", loaded.MessagesJson);
        }
        finally
        {
            try
            {
                if (File.Exists(dbFile)) File.Delete(dbFile);
            }
            catch (IOException) { /* file may be briefly locked by SQLite on Windows; ignore for test */ }
        }
    }

    [Fact]
    [Trait("Category", "HighRisk")]
    public void SQLiteProvider_QB_RoutingAndAllocation_SaveLoad()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SQLite",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:"
            })
            .Build();

        var factory = new AppDbContextFactory(config);
        using var ctx = factory.CreateDbContext();
        ctx.Database.OpenConnection();
        ctx.Database.EnsureCreated();

        var profile = new QuickBooksAllocationProfile { Name = "Test Profile SQLite", IsActive = true };
        ctx.QuickBooksAllocationProfiles.Add(profile);
        ctx.SaveChanges();

        var rule = new QuickBooksRoutingRule
        {
            Name = "Test Route SQLite",
            Priority = 5,
            IsActive = true,
            AllocationProfileId = profile.Id,
            TargetEnterprise = "Water Utility"
        };
        ctx.QuickBooksRoutingRules.Add(rule);
        ctx.SaveChanges();

        var loadedProfile = ctx.QuickBooksAllocationProfiles.AsNoTracking().First(p => p.Name == "Test Profile SQLite");
        Assert.True(loadedProfile.IsActive);

        var loadedRule = ctx.QuickBooksRoutingRules.AsNoTracking().First(r => r.Name == "Test Route SQLite");
        Assert.Equal(5, loadedRule.Priority);
        Assert.Equal(loadedProfile.Id, loadedRule.AllocationProfileId);
    }

    [Fact]
    [Trait("Category", "HighRisk")]
    public void SQLiteProvider_QB_ImportDedup_RejectsDuplicateFileHash()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SQLite",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:"
            })
            .Build();

        var factory = new AppDbContextFactory(config);
        using var ctx = factory.CreateDbContext();
        ctx.Database.OpenConnection();
        ctx.Database.EnsureCreated();

        // Minimal parents for SourceFile
        var batch = new ImportBatch { BatchName = "DedupBatch", SourceSystem = "test", Status = "done", StartedAt = DateTimeOffset.UtcNow };
        ctx.ImportBatches.Add(batch);
        ctx.SaveChanges();

        var variant = new SourceFileVariant { VariantCode = "v1", Description = "test" };
        ctx.SourceFileVariants.Add(variant);
        ctx.SaveChanges();

        var src1 = new SourceFile
        {
            BatchId = batch.Id,
            SourceFileVariantId = variant.Id,
            CanonicalEntity = "ledger",
            OriginalFileName = "gl.xlsx",
            FileHash = "deduphashsqlite999",
            RowCount = 10,
            ColumnCount = 5,
            ImportedAt = DateTimeOffset.UtcNow
        };
        ctx.SourceFiles.Add(src1);
        ctx.SaveChanges();

        // Duplicate hash + canonical should violate unique index (from model)
        var src2 = new SourceFile
        {
            BatchId = batch.Id,
            SourceFileVariantId = variant.Id,
            CanonicalEntity = "ledger",
            OriginalFileName = "gl2.xlsx",
            FileHash = "deduphashsqlite999",
            RowCount = 10,
            ColumnCount = 5,
            ImportedAt = DateTimeOffset.UtcNow
        };
        ctx.SourceFiles.Add(src2);

        var ex = Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
        // The exception should be unique constraint (SQLite or general)
        Assert.Contains("UNIQUE", ex.InnerException?.Message ?? ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
