using System.Text.Json;
using WileyCoWeb.IntegrationTests.Infrastructure;
using WileyWidget.Models.Amplify;

namespace WileyCoWeb.IntegrationTests;

public sealed class WorkspaceSnapshotApiTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public WorkspaceSnapshotApiTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSnapshot_ReturnsSeededWorkspaceData()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/workspace/snapshot?enterprise=Water%20Utility&fiscalYear=2026");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceSnapshotContract>(_jsonOptions);

        Assert.NotNull(payload);
        Assert.Equal("Water Utility", payload.SelectedEnterprise);
        Assert.Equal(2026, payload.SelectedFiscalYear);
        Assert.Equal(55.25m, payload.CurrentRate);
        Assert.Equal(13250m, payload.TotalCosts);
        Assert.Equal(240m, payload.ProjectedVolume);
        Assert.Contains("Water Utility", payload.EnterpriseOptions);
        Assert.DoesNotContain("Archived Utility", payload.EnterpriseOptions);
        Assert.Equal(new[] { 2025, 2026 }, payload.FiscalYearOptions.OrderBy(year => year));
        Assert.Equal("All Services", payload.CustomerServiceOptions.First());
        Assert.Equal(2, payload.CustomerRows.Count);
        Assert.Equal(4, payload.ProjectionRows.Count);
        Assert.NotEmpty(payload.ScenarioItems);
        Assert.Contains(payload.ScenarioItems, item => item.Name == "Utilities");
    }

    [Fact]
    public async Task GetSnapshot_UsesLatestFiscalYearAndFirstActiveEnterprise_WhenFiltersAreMissing()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/workspace/snapshot");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceSnapshotContract>(_jsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(2026, payload.SelectedFiscalYear);
        Assert.Equal("Sanitation Utility", payload.SelectedEnterprise);
    }

    [Fact]
    public async Task PostSnapshot_PersistsSnapshotAndReturnsCreated()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var request = new WorkspaceBootstrapData(
            "Water Utility",
            2026,
            "Water Utility planning snapshot",
            55.25m,
            13250m,
            240m,
            DateTime.UtcNow.ToString("O"));

        var response = await client.PostAsJsonAsync("/api/workspace/snapshot", request, _jsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<WorkspaceSnapshotSaveResponseContract>(_jsonOptions);
        Assert.NotNull(body);
        Assert.True(body.SnapshotId > 0);
        Assert.Contains("Water Utility FY2026 rate snapshot", body.SnapshotName);

        var contextFactory = _factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var snapshot = await context.BudgetSnapshots.SingleAsync();

        Assert.Equal(body.SnapshotId, snapshot.Id);
        Assert.Contains("Water Utility", snapshot.SnapshotName);
        Assert.Contains("Water Utility", snapshot.Payload);
    }

    [Fact]
    public async Task PostSnapshot_ReturnsBadRequest_WhenEnterpriseNameIsMissing()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var request = new WorkspaceBootstrapData(
            string.Empty,
            2026,
            "Invalid snapshot",
            55.25m,
            13250m,
            240m,
            DateTime.UtcNow.ToString("O"));

        var response = await client.PostAsJsonAsync("/api/workspace/snapshot", request, _jsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record WorkspaceSnapshotContract(
        string SelectedEnterprise,
        int SelectedFiscalYear,
        decimal? CurrentRate,
        decimal? TotalCosts,
        decimal? ProjectedVolume,
        List<string> EnterpriseOptions,
        List<int> FiscalYearOptions,
        List<string> CustomerServiceOptions,
        List<CustomerRowContract> CustomerRows,
        List<ProjectionRowContract> ProjectionRows,
        List<ScenarioItemContract> ScenarioItems);

    private sealed record CustomerRowContract(string Name, string Service, string CityLimits);

    private sealed record ProjectionRowContract(string Year, decimal Rate);

    private sealed record ScenarioItemContract(Guid Id, string Name, decimal Cost);

    private sealed record WorkspaceSnapshotSaveResponseContract(long SnapshotId, string SnapshotName, string SavedAtUtc);
}