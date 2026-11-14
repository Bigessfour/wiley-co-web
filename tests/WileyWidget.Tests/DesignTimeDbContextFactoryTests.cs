using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using Xunit;

namespace WileyWidget.Tests;

public sealed class DesignTimeDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_Throws_WhenNoConnectionConfigured()
    {
        using var env = new EnvOverride(
            ("ConnectionStrings__DefaultConnection", null),
            ("DATABASE_URL", null),
            ("WILEY_EF_DESIGN_SKIP_LOCAL_SETTINGS", "1"));

        var factory = new DesignTimeDbContextFactory();
        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext(Array.Empty<string>()));
        Assert.Contains("Design-time DefaultConnection is not configured", ex.Message, StringComparison.Ordinal);
        Assert.Contains("mr-storage", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateDbContext_Throws_WhenConnectionIsDevelopmentSentinel()
    {
        using var env = new EnvOverride(
            ("ConnectionStrings__DefaultConnection",
                "Host=mr-storage;Port=5433;Database=wileyco_dev;Username=wiley_app;Password=USE_DEVELOPMENT_LOCAL_OR_USER_SECRETS"),
            ("DATABASE_URL", null),
            ("WILEY_EF_DESIGN_SKIP_LOCAL_SETTINGS", "1"));

        var factory = new DesignTimeDbContextFactory();
        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext(Array.Empty<string>()));
        Assert.Contains("Design-time DefaultConnection is not configured", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateDbContext_UsesEnvironmentConnectionString()
    {
        // Unreachable port — factory should still build options from env without config errors.
        using var env = new EnvOverride(
            ("ConnectionStrings__DefaultConnection",
                "Host=127.0.0.1;Port=1;Database=wileyco_dev;Username=wiley_app;Password=test;Timeout=1"),
            ("DATABASE_URL", null),
            ("WILEY_EF_DESIGN_SKIP_LOCAL_SETTINGS", "1"));

        var factory = new DesignTimeDbContextFactory();
        using var context = factory.CreateDbContext(Array.Empty<string>());
        Assert.NotNull(context);
        Assert.Contains("wileyco_dev", context.Database.GetDbConnection().ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class EnvOverride : IDisposable
    {
        private readonly List<(string Key, string? Previous)> _previous = new();

        public EnvOverride(params (string Key, string? Value)[] values)
        {
            foreach (var (key, value) in values)
            {
                _previous.Add((key, Environment.GetEnvironmentVariable(key)));
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public void Dispose()
        {
            foreach (var (key, previous) in _previous)
            {
                Environment.SetEnvironmentVariable(key, previous);
            }
        }
    }
}
