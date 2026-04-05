using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WileyCoWeb.IntegrationTests.Infrastructure;

public sealed class ApiApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"WileyCoWebIntegrationTests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextFactory<AppDbContext>>();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .EnableSensitiveDataLogging()
                .Options;

            services.AddSingleton<IDbContextFactory<AppDbContext>>(_ => new AppDbContextFactory(options));
        });
    }

    public async Task InitializeAsync()
    {
        await ResetDatabaseAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public async Task ResetDatabaseAsync()
    {
        var contextFactory = Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        await TestDataSeeder.SeedAsync(context);
    }
}