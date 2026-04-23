using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class TelemetryLogServiceTests
{
    [Fact]
    public async Task LogMethods_PersistTelemetryRows_WithExpectedShapes()
    {
        var factory = CreateFactory();
        var loggerFactory = LoggerFactory.Create(builder => { });
        var service = new TelemetryLogService(factory, loggerFactory.CreateLogger<TelemetryLogService>());

        await service.LogErrorAsync("Something happened", "details", "stack", "corr-1", "user-1", "sess-1");
        await service.LogEventAsync("CustomEvent", "A custom event", "more", "corr-2", "user-2", "sess-2");
        await service.LogExceptionAsync(new InvalidOperationException("outer", new ArgumentException("inner")), "Exception message", "corr-3", "user-3", "sess-3");
        await service.LogUserActionAsync("Opened dashboard", "details", "user-4", "sess-4");

        await using var context = await factory.CreateDbContextAsync();
        var rows = await context.TelemetryLogs.OrderBy(row => row.Id).ToListAsync();

        Assert.Equal(4, rows.Count);
        Assert.Equal("Error", rows[0].EventType);
        Assert.Equal("Something happened", rows[0].Message);
        Assert.Equal("CustomEvent", rows[1].EventType);
        Assert.Equal("A custom event", rows[1].Message);
        Assert.Equal("Exception message", rows[2].Message);
        Assert.Contains("ArgumentException", rows[2].Details ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("UserAction", rows[3].EventType);
        Assert.Equal("Opened dashboard", rows[3].Message);
    }

    private static IDbContextFactory<AppDbContext> CreateFactory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TelemetryLogServiceTests-{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;

        return new AppDbContextFactory(options);
    }
}