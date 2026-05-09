using System.Net;
using System.Net.Http.Json;
using WileyCoWeb.Contracts;
using WileyCoWeb.IntegrationTests.Infrastructure;

namespace WileyCoWeb.IntegrationTests;

public sealed class WorkspacePanelFinancialEndpointsTests
{
    [Fact]
    public async Task PostCapitalGap_ReturnsOkWithSampleMarker_WhenDatabaseIsEmpty()
    {
        using var factory = new ApiApplicationFactory();
        await factory.InitializeAsync();
        await factory.ResetDatabaseAsync(seedData: false);

        var client = factory.CreateClient();
        // Use a FY unlikely to be cached by other tests (BudgetRepository caches by fiscal year).
        const int fiscalYear = 2037;
        var response = await client.PostAsJsonAsync("/api/workspace/capital-gap", new CapitalGapRequest("Water Utility", fiscalYear));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CapitalGapResponse>();
        Assert.NotNull(body);
        Assert.Contains("sample data", body!.ExecutiveSummary, StringComparison.OrdinalIgnoreCase);
        Assert.True(body.CapitalItems.Count > 0);
    }

    [Fact]
    public async Task PostDebtCoverage_ReturnsOkWithSampleMarker_WhenDatabaseIsEmpty()
    {
        using var factory = new ApiApplicationFactory();
        await factory.InitializeAsync();
        await factory.ResetDatabaseAsync(seedData: false);

        var client = factory.CreateClient();
        const int fiscalYear = 2037;
        var response = await client.PostAsJsonAsync("/api/workspace/debt-coverage", new DebtCoverageRequest("Water Utility", fiscalYear));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DebtCoverageResponse>();
        Assert.NotNull(body);
        Assert.Contains("sample data", body!.ExecutiveSummary, StringComparison.OrdinalIgnoreCase);
    }
}
