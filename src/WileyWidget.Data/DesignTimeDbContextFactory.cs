#nullable enable

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WileyWidget.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Host=localhost;Database=wileywidget_design;Username=postgres;Password=postgres";
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(Environment.ExpandEnvironmentVariables(connectionString), npgsql =>
        {
            npgsql.CommandTimeout(30);
        });

        return new AppDbContext(optionsBuilder.Options);
    }
}
