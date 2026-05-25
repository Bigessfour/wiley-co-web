using System.Net;
using System.Net.Http.Json;
using WileyCoWeb.Contracts;
using WileyCoWeb.IntegrationTests.Infrastructure;

namespace WileyCoWeb.IntegrationTests;

[Trait("Category", "HighRisk")]
public sealed class WorkspacePanelFinancialEndpointsTests
{
    [Fact]
    public async Task PostCapitalGap_Returns503_WhenDatabaseIsEmptyAndSyntheticFallbackDisabled()
    {
        using var factory = new ApiApplicationFactory();
        await factory.InitializeAsync();
        await factory.ResetDatabaseAsync(seedData: false);

        var client = factory.CreateClient();
        const int fiscalYear = 2037;
        var response = await client.PostAsJsonAsync("/api/workspace/capital-gap", new CapitalGapRequest("Water Utility", fiscalYear));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task PostDebtCoverage_Returns503OrNotFound_WhenDatabaseIsEmptyAndSyntheticFallbackDisabled()
    {
        using var factory = new ApiApplicationFactory();
        await factory.InitializeAsync();
        await factory.ResetDatabaseAsync(seedData: false);

        var client = factory.CreateClient();
        const int fiscalYear = 2037;
        var response = await client.PostAsJsonAsync("/api/workspace/debt-coverage", new DebtCoverageRequest("Water Utility", fiscalYear));

        Assert.True(
            response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound,
            $"Expected 503 or 404 when budget data is missing, got {response.StatusCode}");
    }
}
