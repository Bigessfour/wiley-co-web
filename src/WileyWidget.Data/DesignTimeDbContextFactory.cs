#nullable enable

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using Microsoft.EntityFrameworkCore.Design;

namespace WileyWidget.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Support multi-provider for design-time (e.g. `dotnet ef ...` against SQLite for local machine dev)
        // Set DATABASE_PROVIDER=sqlite and DATABASE_URL="Data Source=..." (or use default below)
        var provider = Environment.GetEnvironmentVariable("DATABASE_PROVIDER") ?? "PostgreSQL";
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
            {
                connectionString = "Data Source=wileywidget_design.db";
            }
            else
            {
                connectionString = "Host=localhost;Database=wileywidget_design;Username=postgres;Password=postgres";
            }
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var expanded = Environment.ExpandEnvironmentVariables(connectionString);

        if (provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase) ||
            expanded.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) ||
            expanded.StartsWith("Filename=", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlite(expanded);
        }
        else
        {
            optionsBuilder.UseNpgsql(expanded, npgsql =>
            {
                npgsql.CommandTimeout(30);
            });
        }

        return new AppDbContext(optionsBuilder.Options);
    }
}
