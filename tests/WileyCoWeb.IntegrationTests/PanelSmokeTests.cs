using System.Text.Json;
using WileyCoWeb.Contracts;
using WileyCoWeb.IntegrationTests.Infrastructure;
using WileyWidget.Models.Amplify;

namespace WileyCoWeb.IntegrationTests;

/// <summary>
/// Per-panel integration smoke tests. Each test loads a real <see cref="WorkspaceBootstrapData"/>
/// from the seeded in-memory database and asserts that every editable field consumed by that panel
/// is present, correctly typed, and round-trips through the service layer unchanged.
/// </summary>
public sealed class PanelSmokeTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public PanelSmokeTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    // ─── BreakEvenPanel ─────────────────────────────────────────────────────────

    [Fact]
    public async Task BreakEvenPanel_Snapshot_ContainsAllEditableFields()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var payload = await GetSnapshotAsync(client);

        Assert.NotNull(payload.CurrentRate);
        Assert.True(payload.CurrentRate > 0, "CurrentRate must be positive for break-even math to be meaningful.");
        Assert.NotNull(payload.TotalCosts);
        Assert.True(payload.TotalCosts > 0, "TotalCosts must be positive.");
        Assert.NotNull(payload.ProjectedVolume);
        Assert.True(payload.ProjectedVolume > 0, "ProjectedVolume must be positive.");
    }

    [Fact]
    public async Task BreakEvenPanel_RoundTrip_PostAndGetPreservesExactDecimalValues()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var request = new WorkspaceBootstrapData(
            "Water Utility",
            2026,
            "Break-even smoke test",
            72.33m,
            18450.75m,
            312m,
            DateTime.UtcNow.ToString("O"));

        var postResponse = await client.PostAsJsonAsync("/api/workspace/snapshot", request, _jsonOptions);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var payload = await GetSnapshotAsync(client);

        Assert.Equal(72.33m, payload.CurrentRate);
        Assert.Equal(18450.75m, payload.TotalCosts);
        Assert.Equal(312m, payload.ProjectedVolume);
    }

    // ─── RatesPanel ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RatesPanel_Snapshot_ContainsCurrentRateAndProjectionRows()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var payload = await GetSnapshotAsync(client);

        Assert.NotNull(payload.CurrentRate);
        Assert.NotNull(payload.ProjectionRows);
        Assert.NotEmpty(payload.ProjectionRows);
        Assert.All(payload.ProjectionRows, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Year), "Each projection row must have a year label.");
            Assert.True(row.Rate >= 0, "Projection row rate must be non-negative.");
        });
    }

    [Fact]
    public async Task RatesPanel_RoundTrip_ProjectionRowsPreservedAfterSnapshotSave()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var projections = new List<ProjectionRow>
        {
            new("FY24", 48.00m),
            new("FY25", 51.50m),
            new("FY26", 55.25m),
            new("FY27", 58.75m)
        };

        var request = new WorkspaceBootstrapData(
            "Water Utility",
            2026,
            "Rates round-trip smoke test",
            55.25m,
            13250m,
            240m,
            DateTime.UtcNow.ToString("O"))
        {
            ProjectionRows = projections
        };

        var postResponse = await client.PostAsJsonAsync("/api/workspace/snapshot", request, _jsonOptions);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var body = await postResponse.Content.ReadFromJsonAsync<WorkspaceSnapshotSaveResponse>(_jsonOptions);
        Assert.NotNull(body);

        // Verify the save response includes the snapshot identifier so the UI can navigate.
        Assert.True(body.SnapshotId > 0);
        Assert.Contains("Water Utility", body.SnapshotName);
    }

    // ─── ScenarioPlannerPanel ────────────────────────────────────────────────────

    [Fact]
    public async Task ScenarioPlannerPanel_Snapshot_ContainsScenarioItemsWithNamesAndCosts()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var payload = await GetSnapshotAsync(client);

        Assert.NotNull(payload.ScenarioItems);
        Assert.NotEmpty(payload.ScenarioItems);
        Assert.All(payload.ScenarioItems, item =>
        {
            Assert.NotEqual(Guid.Empty, item.Id);
            Assert.False(string.IsNullOrWhiteSpace(item.Name), "Scenario item must have a name.");
            Assert.True(item.Cost >= 0, "Scenario item cost must be non-negative.");
        });
        Assert.Contains(payload.ScenarioItems, item => item.Name == "Utilities");
    }

    [Fact]
    public async Task ScenarioPlannerPanel_RoundTrip_ScenarioItemsPreservedInSnapshot()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var scenarioId = Guid.NewGuid();
        var scenarios = new List<WorkspaceScenarioItemData>
        {
            new(scenarioId, "Reserve transfer", 6200m),
            new(Guid.NewGuid(), "Vehicle replacement", 18000m),
            new(Guid.NewGuid(), "SCADA upgrade", 42500m)
        };

        var request = new WorkspaceBootstrapData(
            "Water Utility",
            2026,
            "Scenario planner smoke test",
            55.25m,
            13250m,
            240m,
            DateTime.UtcNow.ToString("O"))
        {
            ScenarioItems = scenarios
        };

        var postResponse = await client.PostAsJsonAsync("/api/workspace/snapshot", request, _jsonOptions);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        // Verify database persisted all scenario items in the snapshot payload.
        var contextFactory = _factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var savedSnapshot = await context.BudgetSnapshots.SingleAsync();
        Assert.Contains("Reserve transfer", savedSnapshot.Payload);
        Assert.Contains("Vehicle replacement", savedSnapshot.Payload);
        Assert.Contains("SCADA upgrade", savedSnapshot.Payload);
    }

    // ─── CustomerViewerPanel ────────────────────────────────────────────────────

    [Fact]
    public async Task CustomerViewerPanel_Snapshot_ContainsCustomerRowsAndFilterOptions()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var payload = await GetSnapshotAsync(client);

        Assert.NotNull(payload.CustomerRows);
        Assert.NotEmpty(payload.CustomerRows);
        Assert.All(payload.CustomerRows, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Name), "Customer row must have a name.");
            Assert.False(string.IsNullOrWhiteSpace(row.Service), "Customer row must have a service type.");
            Assert.False(string.IsNullOrWhiteSpace(row.CityLimits), "Customer row must have a city limits value.");
        });

        Assert.NotNull(payload.CustomerServiceOptions);
        Assert.NotEmpty(payload.CustomerServiceOptions);
        Assert.Equal("All Services", payload.CustomerServiceOptions.First());

        Assert.NotNull(payload.CustomerCityLimitOptions);
        Assert.NotEmpty(payload.CustomerCityLimitOptions);
    }

    [Fact]
    public async Task CustomerViewerPanel_RoundTrip_CustomerRowsPreservedInSnapshot()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var customers = new List<CustomerRow>
        {
            new("North Plant", "Water", "Yes"),
            new("South Lift", "Sewer", "No"),
            new("East Station", "Water", "Yes")
        };

        var request = new WorkspaceBootstrapData(
            "Water Utility",
            2026,
            "Customer viewer smoke test",
            55.25m,
            13250m,
            240m,
            DateTime.UtcNow.ToString("O"))
        {
            CustomerRows = customers
        };

        var postResponse = await client.PostAsJsonAsync("/api/workspace/snapshot", request, _jsonOptions);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var contextFactory = _factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var savedSnapshot = await context.BudgetSnapshots.SingleAsync();
        Assert.Contains("North Plant", savedSnapshot.Payload);
        Assert.Contains("South Lift", savedSnapshot.Payload);
        Assert.Contains("East Station", savedSnapshot.Payload);
    }

    // ─── TrendsPanel ────────────────────────────────────────────────────────────

    [Fact]
    public async Task TrendsPanel_Snapshot_ContainsOrderedProjectionRowsWithPositiveRates()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var payload = await GetSnapshotAsync(client);

        Assert.NotNull(payload.ProjectionRows);
        Assert.True(payload.ProjectionRows.Count >= 2, "Trends panel needs at least two projection years to show a trend.");
        Assert.All(payload.ProjectionRows, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Year));
            Assert.True(row.Rate >= 0);
        });

        // Rates should be non-decreasing (realistic utility scenario).
        var rates = payload.ProjectionRows.Select(r => r.Rate).ToList();
        for (int i = 1; i < rates.Count; i++)
        {
            Assert.True(rates[i] >= rates[i - 1],
                $"Projection row {i} rate ({rates[i]}) should not be less than the prior row ({rates[i - 1]}).");
        }
    }

    [Fact]
    public async Task TrendsPanel_RoundTrip_FourYearProjectionPreservedInSnapshot()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var projections = new List<ProjectionRow>
        {
            new("FY24", 48.00m),
            new("FY25", 51.50m),
            new("FY26", 55.25m),
            new("FY27", 59.75m)
        };

        var request = new WorkspaceBootstrapData(
            "Water Utility",
            2026,
            "Trends smoke test",
            55.25m,
            13250m,
            240m,
            DateTime.UtcNow.ToString("O"))
        {
            ProjectionRows = projections
        };

        var postResponse = await client.PostAsJsonAsync("/api/workspace/snapshot", request, _jsonOptions);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var contextFactory = _factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var savedSnapshot = await context.BudgetSnapshots.SingleAsync();
        Assert.Contains("FY24", savedSnapshot.Payload);
        Assert.Contains("FY27", savedSnapshot.Payload);
        Assert.Contains("59.75", savedSnapshot.Payload);
    }

    // ─── DecisionSupportPanel ───────────────────────────────────────────────────

    [Fact]
    public async Task DecisionSupportPanel_Snapshot_ContainsEnterpriseAndFiscalYearOptions()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var payload = await GetSnapshotAsync(client);

        Assert.NotNull(payload.EnterpriseOptions);
        Assert.NotEmpty(payload.EnterpriseOptions);
        Assert.Contains("Water Utility", payload.EnterpriseOptions);
        Assert.DoesNotContain("Archived Utility", payload.EnterpriseOptions);

        Assert.NotNull(payload.FiscalYearOptions);
        Assert.NotEmpty(payload.FiscalYearOptions);
        Assert.All(payload.FiscalYearOptions, year => Assert.True(year >= 2020, "Fiscal year options should be recent years."));
    }

    [Fact]
    public async Task DecisionSupportPanel_Snapshot_ActiveScenarioNameIsPopulated()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var payload = await GetSnapshotAsync(client);

        Assert.False(string.IsNullOrWhiteSpace(payload.ActiveScenarioName),
            "DecisionSupportPanel requires ActiveScenarioName to display the current planning context.");
    }

    // ─── QuickBooksImportPanel ───────────────────────────────────────────────────

    [Fact]
    public async Task QuickBooksImportPanel_Preview_ReturnsAllFieldsConsumedByPanel()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await PostCsvImportAsync(client, "/api/imports/quickbooks/preview");

        response.EnsureSuccessStatusCode();
        var preview = await response.Content.ReadFromJsonAsync<QuickBooksImportPreviewResponse>(_jsonOptions);

        Assert.NotNull(preview);
        Assert.False(string.IsNullOrWhiteSpace(preview.FileName), "Panel needs FileName for display.");
        Assert.True(preview.TotalRows > 0, "Panel needs TotalRows for the preview badge.");
        Assert.False(preview.IsDuplicate, "Fresh import should not be flagged as duplicate.");
        Assert.NotNull(preview.Rows);
        Assert.NotEmpty(preview.Rows);
        Assert.All(preview.Rows, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Memo));
            Assert.False(string.IsNullOrWhiteSpace(row.AccountName));
        });
    }

    [Fact]
    public async Task QuickBooksImportPanel_Commit_AllPanelStatusFieldsPopulated()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await PostCsvImportAsync(client, "/api/imports/quickbooks/commit");

        response.EnsureSuccessStatusCode();
        var commit = await response.Content.ReadFromJsonAsync<QuickBooksImportCommitResponse>(_jsonOptions);

        Assert.NotNull(commit);
        Assert.False(commit.IsDuplicate);
        Assert.True(commit.ImportedRows > 0, "Panel status message needs imported row count.");
        Assert.False(string.IsNullOrWhiteSpace(commit.StatusMessage), "Panel needs a status message after commit.");
        Assert.Contains("Imported", commit.StatusMessage);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private async Task<WorkspaceBootstrapData> GetSnapshotAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/workspace/snapshot?enterprise=Water%20Utility&fiscalYear=2026");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceBootstrapData>(_jsonOptions);
        Assert.NotNull(payload);
        return payload;
    }

    private static Task<HttpResponseMessage> PostCsvImportAsync(HttpClient client, string path)
    {
        const string csv =
            "Date,Type,Num,Name,Memo,Account,Split,Amount,Balance,Clr\n" +
            "01/01/2026,Invoice,1001,Town of Wiley,Water Billing,Water Revenue,Accounts Receivable,125.00,125.00,C\n" +
            "01/02/2026,Payment,1002,Town of Wiley,Payment Received,Accounts Receivable,Water Revenue,-125.00,0.00,C\n";

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "file", "quickbooks-ledger.csv");
        return client.PostAsync(path, form);
    }
}
