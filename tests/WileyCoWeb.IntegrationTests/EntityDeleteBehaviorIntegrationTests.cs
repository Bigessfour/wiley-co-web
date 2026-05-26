using Microsoft.EntityFrameworkCore;
using WileyCoWeb.IntegrationTests.Infrastructure;
using WileyWidget.Data;

namespace WileyCoWeb.IntegrationTests;

[Trait("Category", "HighRisk")]
public sealed class EntityDeleteBehaviorIntegrationTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public EntityDeleteBehaviorIntegrationTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SoftDeletedEnterprise_ApartmentUnitTypesRemainInDatabase()
    {
        await _factory.ResetDatabaseAsync();
        var contextFactory = _factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        var enterprise = await context.Enterprises
            .Include(e => e.ApartmentUnitTypes)
            .FirstOrDefaultAsync(e => e.ApartmentUnitTypes.Any());

        if (enterprise is null)
        {
            return;
        }

        var unitTypeId = enterprise.ApartmentUnitTypes.First().Id;
        enterprise.IsDeleted = true;
        await context.SaveChangesAsync();

        var remaining = await context.ApartmentUnitTypes
            .IgnoreQueryFilters()
            .Where(u => u.Id == unitTypeId)
            .ToListAsync();

        Assert.NotEmpty(remaining);
    }
}
