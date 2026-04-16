using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using WileyCoWeb.Api;

namespace WileyCoWeb.IntegrationTests.Infrastructure;

public sealed class ApiApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"WileyCoWebIntegrationTests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextFactory<AppDbContext>>();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .EnableSensitiveDataLogging()
                .Options;

            services.AddSingleton(options);
            services.AddScoped(_ => new AppDbContext(options));
            services.AddSingleton<IDbContextFactory<AppDbContext>>(_ => new AppDbContextFactory(options));
        });
    }

    public async Task InitializeAsync()
    {
        await ResetDatabaseAsync();
    }

    public async Task ResetDatabaseAsync(bool seedData = true)
    {
        var contextFactory = Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        if (seedData)
        {
            await TestDataSeeder.SeedAsync(context);
        }
    }
}