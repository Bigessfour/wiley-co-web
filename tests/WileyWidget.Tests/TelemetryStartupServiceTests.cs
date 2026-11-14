using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;
using WileyWidget.Services.Telemetry;
using Xunit;

namespace WileyWidget.Tests;

public sealed class TelemetryStartupServiceTests
{
    [Fact]
    public async Task StartAsync_Throws_WhenConnectivityFailsAndDegradedDisabled()
    {
        AppDbStartupState.ResetForTests();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:AllowDegradedStartup"] = "false",
                ["Database:ApplyMigrations"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(new UnreachableDbContextFactory());
        await using var provider = services.BuildServiceProvider();

        var sut = new TelemetryStartupService(provider, configuration);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_DoesNotThrow_WhenConnectivityFailsAndDegradedAllowed()
    {
        AppDbStartupState.ResetForTests();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:AllowDegradedStartup"] = "true",
                ["Database:ApplyMigrations"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(new UnreachableDbContextFactory());
        await using var provider = services.BuildServiceProvider();

        var sut = new TelemetryStartupService(provider, configuration);
        await sut.StartAsync(CancellationToken.None);
    }

    private sealed class UnreachableDbContextFactory : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=wileyco_dev;Username=wiley_app;Password=x;Timeout=1;Command Timeout=1")
                .Options;
            return new AppDbContext(options);
        }
    }
}
