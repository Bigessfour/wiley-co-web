using System.Text.Json;
using WileyCoWeb.Contracts;
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
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceBootstrapData>(_jsonOptions);

        Assert.NotNull(payload);
        Assert.Equal("Water Utility", payload.SelectedEnterprise);
        Assert.Equal(2026, payload.SelectedFiscalYear);
        Assert.Equal(55.25m, payload.CurrentRate);
        Assert.Equal(13250m, payload.TotalCosts);
        Assert.Equal(240m, payload.ProjectedVolume);
        Assert.NotNull(payload.EnterpriseOptions);
        Assert.NotNull(payload.FiscalYearOptions);
        Assert.NotNull(payload.CustomerServiceOptions);
        Assert.NotNull(payload.CustomerRows);
        Assert.NotNull(payload.ProjectionRows);
        Assert.NotNull(payload.ScenarioItems);
        Assert.Contains("Water Utility", payload.EnterpriseOptions);
        Assert.Contains("Wiley Sanitation District", payload.EnterpriseOptions);
        Assert.Contains("Trash", payload.EnterpriseOptions);
        Assert.Contains("Apartments", payload.EnterpriseOptions);
        Assert.DoesNotContain("Archived Utility", payload.EnterpriseOptions);
        Assert.Equal(new[] { 2025, 2026 }, payload.FiscalYearOptions.OrderBy(year => year));
        Assert.Equal("All Services", payload.CustomerServiceOptions.First());
        Assert.Equal(2, payload.CustomerRows.Count);
        Assert.Equal(4, payload.ProjectionRows.Count);
        Assert.NotEmpty(payload.ScenarioItems);
        Assert.Contains(payload.ScenarioItems, item => item.Name == "Water Utility reserve target");
    }

    [Fact]
    public async Task GetSnapshot_UsesLatestFiscalYearAndFirstActiveEnterprise_WhenFiltersAreMissing()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/workspace/snapshot");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceBootstrapData>(_jsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(2026, payload.SelectedFiscalYear);
        Assert.Equal("Water Utility", payload.SelectedEnterprise);
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

        var body = await response.Content.ReadFromJsonAsync<WorkspaceSnapshotSaveResponse>(_jsonOptions);
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

    [Fact]
    public async Task PostSnapshotExports_PersistsArtifacts_AndDownloadEndpointReturnsBinaryContent()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var snapshotRequest = new WorkspaceBootstrapData(
            "Water Utility",
            2026,
            "Water Utility planning snapshot",
            55.25m,
            13250m,
            240m,
            DateTime.UtcNow.ToString("O"))
        {
            CustomerRows =
            [
                new CustomerRow("North Plant", "Water", "Yes")
            ],
            ProjectionRows =
            [
                new ProjectionRow("FY26", 55.25m)
            ],
            ScenarioItems =
            [
                new WorkspaceScenarioItemData(Guid.NewGuid(), "Reserve transfer", 6200m)
            ]
        };

        var snapshotResponse = await client.PostAsJsonAsync("/api/workspace/snapshot", snapshotRequest, _jsonOptions);
        snapshotResponse.EnsureSuccessStatusCode();

        var savedSnapshot = await snapshotResponse.Content.ReadFromJsonAsync<WorkspaceSnapshotSaveResponse>(_jsonOptions);
        Assert.NotNull(savedSnapshot);

        var exportRequest = new WorkspaceSnapshotArtifactRequest(
            ["customer-workbook", "workspace-pdf"],
            false);

        var exportResponse = await client.PostAsJsonAsync($"/api/workspace/snapshot/{savedSnapshot.SnapshotId}/exports", exportRequest, _jsonOptions);

        exportResponse.EnsureSuccessStatusCode();
        var batch = await exportResponse.Content.ReadFromJsonAsync<WorkspaceSnapshotArtifactBatchResponse>(_jsonOptions);

        Assert.NotNull(batch);
        Assert.Equal(savedSnapshot.SnapshotId, batch.SnapshotId);
        Assert.Equal(2, batch.Artifacts.Count);
        Assert.Contains(batch.Artifacts, artifact => artifact.DocumentKind == "customer-workbook" && artifact.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(batch.Artifacts, artifact => artifact.DocumentKind == "workspace-pdf" && artifact.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

        var listResponse = await client.GetAsync($"/api/workspace/snapshot/{savedSnapshot.SnapshotId}/exports");
        listResponse.EnsureSuccessStatusCode();
        var listedBatch = await listResponse.Content.ReadFromJsonAsync<WorkspaceSnapshotArtifactBatchResponse>(_jsonOptions);

        Assert.NotNull(listedBatch);
        Assert.Equal(2, listedBatch.Artifacts.Count);

        var downloadableArtifact = listedBatch.Artifacts.First(artifact => artifact.DocumentKind == "workspace-pdf");
        var downloadResponse = await client.GetAsync(downloadableArtifact.DownloadUrl);
        downloadResponse.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", downloadResponse.Content.Headers.ContentType?.MediaType);
        var downloadBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.True(downloadBytes.Length > 4);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(downloadBytes, 0, 4));

        var contextFactory = _factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal(2, await context.BudgetSnapshotArtifacts.CountAsync());
    }

    [Fact]
    public async Task PostScenario_PersistsScenario_AndScenarioEndpointsReturnSavedWorkspacePayload()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var scenarioRequest = new WorkspaceScenarioSaveRequest(
            "Council Review Scenario",
            "Scenario persisted from the workspace shell.",
            new WorkspaceBootstrapData(
                "Water Utility",
                2026,
                "Council Review Scenario",
                55.25m,
                13250m,
                240m,
                DateTime.UtcNow.ToString("O"))
            {
                ScenarioItems =
                [
                    new WorkspaceScenarioItemData(Guid.NewGuid(), "Reserve transfer", 6200m),
                    new WorkspaceScenarioItemData(Guid.NewGuid(), "Vehicle replacement", 18000m)
                ],
                CustomerRows =
                [
                    new CustomerRow("North Plant", "Water", "Yes")
                ],
                ProjectionRows =
                [
                    new ProjectionRow("FY26", 55.25m)
                ]
            });

        var saveResponse = await client.PostAsJsonAsync("/api/workspace/scenarios", scenarioRequest, _jsonOptions);

        Assert.Equal(HttpStatusCode.Created, saveResponse.StatusCode);
        var savedScenario = await saveResponse.Content.ReadFromJsonAsync<WorkspaceScenarioSummaryResponse>(_jsonOptions);
        Assert.NotNull(savedScenario);
        Assert.Equal("Council Review Scenario", savedScenario.ScenarioName);
        Assert.Equal(2, savedScenario.ScenarioItemCount);
        Assert.Equal(24200m, savedScenario.ScenarioCostTotal);

        var listResponse = await client.GetAsync("/api/workspace/scenarios?enterprise=Water%20Utility&fiscalYear=2026");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await listResponse.Content.ReadFromJsonAsync<WorkspaceScenarioCollectionResponse>(_jsonOptions);

        Assert.NotNull(listPayload);
        Assert.Single(listPayload.Scenarios);
        Assert.Equal(savedScenario.SnapshotId, listPayload.Scenarios[0].SnapshotId);

        var loadResponse = await client.GetAsync($"/api/workspace/scenarios/{savedScenario.SnapshotId}");
        loadResponse.EnsureSuccessStatusCode();
        var loadedScenario = await loadResponse.Content.ReadFromJsonAsync<WorkspaceBootstrapData>(_jsonOptions);

        Assert.NotNull(loadedScenario);
        Assert.Equal("Council Review Scenario", loadedScenario.ActiveScenarioName);
        Assert.Equal("Water Utility", loadedScenario.SelectedEnterprise);
        Assert.Equal(2, loadedScenario.ScenarioItems?.Count);
    }

    [Fact]
    public async Task PutBaseline_UpdatesEnterprise_AndFutureSnapshotReadsReturnPersistedValues()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var request = new WorkspaceBaselineUpdateRequest(
            "Water Utility",
            2026,
            61.75m,
            15500m,
            275m);

        var saveResponse = await client.PutAsJsonAsync("/api/workspace/baseline", request, _jsonOptions);

        saveResponse.EnsureSuccessStatusCode();
        var payload = await saveResponse.Content.ReadFromJsonAsync<WorkspaceBaselineUpdateResponse>(_jsonOptions);

        Assert.NotNull(payload);
        Assert.Equal("Water Utility", payload.SelectedEnterprise);
        Assert.Equal(2026, payload.SelectedFiscalYear);
        Assert.Equal(61.75m, payload.Snapshot.CurrentRate);
        Assert.Equal(15500m, payload.Snapshot.TotalCosts);
        Assert.Equal(275m, payload.Snapshot.ProjectedVolume);

        var snapshotResponse = await client.GetAsync("/api/workspace/snapshot?enterprise=Water%20Utility&fiscalYear=2026");
        snapshotResponse.EnsureSuccessStatusCode();
        var snapshot = await snapshotResponse.Content.ReadFromJsonAsync<WorkspaceBootstrapData>(_jsonOptions);

        Assert.NotNull(snapshot);
        Assert.Equal(61.75m, snapshot.CurrentRate);
        Assert.Equal(15500m, snapshot.TotalCosts);
        Assert.Equal(275m, snapshot.ProjectedVolume);

        var contextFactory = _factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var enterprise = await context.Enterprises.SingleAsync(item => item.Name == "Water Utility");

        Assert.Equal(61.75m, enterprise.CurrentRate);
        Assert.Equal(15500m, enterprise.MonthlyExpenses);
        Assert.Equal(275, enterprise.CitizenCount);
        Assert.NotNull(enterprise.LastModified);
    }

    [Fact]
    public async Task GetScenario_ReturnsBadRequest_WhenPersistedPayloadCannotBeDeserialized()
    {
        await _factory.ResetDatabaseAsync();

        var contextFactory = _factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        var snapshot = new BudgetSnapshot
        {
            SnapshotName = "Broken scenario snapshot",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTimeOffset.UtcNow,
            Notes = "RecordType:Scenario; Enterprise: Water Utility; FY: 2026; Scenario: Broken; Description: Corrupted payload",
            Payload = "{ not valid json"
        };

        context.BudgetSnapshots.Add(snapshot);
        await context.SaveChangesAsync();

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/workspace/scenarios/{snapshot.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetScenarios_ReturnsCorsHeaders_ForAmplifyPreviewOrigin()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/workspace/scenarios?enterprise=Water%20Utility&fiscalYear=2026");
        request.Headers.TryAddWithoutValidation("Origin", "https://preview-branch.d2ellat1y3ljd9.amplifyapp.com");

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins));
        Assert.Contains("https://preview-branch.d2ellat1y3ljd9.amplifyapp.com", allowedOrigins);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credentials));
        Assert.Contains("true", credentials, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetScenarios_DoesNotReturnCorsHeaders_ForUnknownOrigin()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/workspace/scenarios?enterprise=Water%20Utility&fiscalYear=2026");
        request.Headers.TryAddWithoutValidation("Origin", "https://example.com");

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task PostSnapshotExports_ReturnsBadRequest_WhenSnapshotPayloadIsMissing()
    {
        await _factory.ResetDatabaseAsync();

        var contextFactory = _factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        var snapshot = new BudgetSnapshot
        {
            SnapshotName = "Missing payload snapshot",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTimeOffset.UtcNow,
            Notes = "RecordType:RateSnapshot; Enterprise: Water Utility; FY: 2026",
            Payload = string.Empty
        };

        context.BudgetSnapshots.Add(snapshot);
        await context.SaveChangesAsync();

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/workspace/snapshot/{snapshot.Id}/exports",
            new WorkspaceSnapshotArtifactRequest(["customer-workbook"], false),
            _jsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutBaseline_ReturnsNotFound_WhenEnterpriseDoesNotExist()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var request = new WorkspaceBaselineUpdateRequest(
            "Missing Utility",
            2026,
            61.75m,
            15500m,
            275m);

        var response = await client.PutAsJsonAsync("/api/workspace/baseline", request, _jsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetScenario_ReturnsNotFound_WhenSnapshotDoesNotExist()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/workspace/scenarios/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetExportDownload_ReturnsNotFound_WhenArtifactDoesNotExist()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/workspace/exports/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostSnapshotExports_ReturnsNotFound_WhenSnapshotDoesNotExist()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/workspace/snapshot/999999/exports",
            new WorkspaceSnapshotArtifactRequest(["workspace-pdf"], false),
            _jsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

}