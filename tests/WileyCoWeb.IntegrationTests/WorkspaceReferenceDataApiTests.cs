using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WileyCoWeb.Contracts;
using WileyCoWeb.IntegrationTests.Infrastructure;
using WileyWidget.Models;

namespace WileyCoWeb.IntegrationTests;

public sealed class WorkspaceReferenceDataApiTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory factory;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public WorkspaceReferenceDataApiTests(ApiApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task PostReferenceDataImport_SeedsEnterprises_FromImportFolder()
    {
        await factory.ResetDatabaseAsync();
        await ClearWorkspaceReferenceDataAsync();

        using var client = factory.CreateClient();
        var request = new WorkspaceReferenceDataImportRequest(ResolveImportDataPath());

        var response = await client.PostAsJsonAsync("/api/workspace/reference-data/import", request, jsonOptions);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceReferenceDataImportResponse>(jsonOptions);

        Assert.NotNull(payload);
        Assert.True(payload.ImportedEnterpriseCount >= 2);
        Assert.True(payload.ImportedUtilityCustomerCount > 0);
        Assert.Contains("Water Utility", payload.EnterpriseNames);
        Assert.Contains("Wiley Sanitation District", payload.EnterpriseNames);
        Assert.Contains("Imported", payload.UtilityCustomerImportStatus, StringComparison.OrdinalIgnoreCase);

        var snapshotResponse = await client.GetAsync("/api/workspace/snapshot");
        snapshotResponse.EnsureSuccessStatusCode();
        var snapshot = await snapshotResponse.Content.ReadFromJsonAsync<WorkspaceBootstrapData>(jsonOptions);

        Assert.NotNull(snapshot);
        Assert.Contains("Water Utility", snapshot.EnterpriseOptions ?? []);
        Assert.Contains("Wiley Sanitation District", snapshot.EnterpriseOptions ?? []);
        Assert.NotEmpty(snapshot.CustomerRows ?? []);
        Assert.Contains(snapshot.CustomerRows ?? [], row => row.Name.Contains("COBANK", StringComparison.OrdinalIgnoreCase));

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal(2, await context.Enterprises.CountAsync(item => !item.IsDeleted));
        Assert.Equal(payload.ImportedUtilityCustomerCount, await context.UtilityCustomers.CountAsync());
    }

    [Fact]
    public async Task PostReferenceDataImport_ReturnsBadRequest_WhenExplicitPathIsRequired_AndNoPathWasProvided()
    {
        await factory.ResetDatabaseAsync();
        await ClearWorkspaceReferenceDataAsync();

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/workspace/reference-data/import", new WorkspaceReferenceDataImportRequest(), jsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("requires an explicit importDataPath", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostReferenceDataImport_BackfillsBaselines_AndImportsSampleLedgers_WhenRequested()
    {
        await factory.ResetDatabaseAsync();
        await ClearWorkspaceReferenceDataAsync();

        using var client = factory.CreateClient();
        var request = new WorkspaceReferenceDataImportRequest(
            ResolveImportDataPath(),
            IncludeSampleLedgerData: true,
            ApplyDefaultEnterpriseBaselines: true);

        var response = await client.PostAsJsonAsync("/api/workspace/reference-data/import", request, jsonOptions);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceReferenceDataImportResponse>(jsonOptions);

        Assert.NotNull(payload);
        Assert.True(payload.ImportedLedgerFileCount >= 2);
        Assert.True(payload.ImportedLedgerRowCount > 0);
        Assert.True(payload.SeededEnterpriseBaselineCount >= 2);

        var snapshotResponse = await client.GetAsync("/api/workspace/snapshot?enterprise=Water%20Utility&fiscalYear=2026");
        snapshotResponse.EnsureSuccessStatusCode();
        var snapshot = await snapshotResponse.Content.ReadFromJsonAsync<WorkspaceBootstrapData>(jsonOptions);

        Assert.NotNull(snapshot);
        Assert.Equal("Water Utility", snapshot.SelectedEnterprise);
        Assert.Equal(31.25m, snapshot.CurrentRate);
        Assert.Equal(98000m, snapshot.TotalCosts);
        Assert.Equal(4500m, snapshot.ProjectedVolume);

        var knowledgeResponse = await client.PostAsJsonAsync(
            "/api/workspace/knowledge",
            new WorkspaceKnowledgeRequest(snapshot, 5, 3),
            jsonOptions);

        var knowledgeBody = await knowledgeResponse.Content.ReadAsStringAsync();
        Assert.True(knowledgeResponse.IsSuccessStatusCode, knowledgeBody);
        var knowledge = await knowledgeResponse.Content.ReadFromJsonAsync<WorkspaceKnowledgeResponse>(jsonOptions);

        Assert.NotNull(knowledge);
        Assert.Equal("Water Utility", knowledge.SelectedEnterprise);
        Assert.False(string.IsNullOrWhiteSpace(knowledge.ExecutiveSummary));
        Assert.False(string.IsNullOrWhiteSpace(knowledge.ReserveRiskAssessment));
        Assert.NotEmpty(knowledge.TopVariances);
        Assert.All(knowledge.TopVariances, variance => Assert.NotEqual(0m, variance.ActualAmount));

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.True(await context.LedgerEntries.AnyAsync());
        Assert.True(await context.ImportBatches.AnyAsync());
        Assert.True(await context.SourceFiles.AnyAsync());

        var wsdCollectionFeeBudget = await context.BudgetEntries.SingleAsync(item => item.FiscalYear == 2026 && item.AccountNumber == "310.00");
        Assert.Equal(1500m, wsdCollectionFeeBudget.ActualAmount);
    }

    [Fact]
    public async Task PostReferenceDataImport_BuildsEnterpriseScopedReserveKnowledge()
    {
        await factory.ResetDatabaseAsync();
        await ClearWorkspaceReferenceDataAsync();

        using var client = factory.CreateClient();
        var request = new WorkspaceReferenceDataImportRequest(
            ResolveImportDataPath(),
            IncludeSampleLedgerData: true,
            ApplyDefaultEnterpriseBaselines: true);

        var importResponse = await client.PostAsJsonAsync("/api/workspace/reference-data/import", request, jsonOptions);
        importResponse.EnsureSuccessStatusCode();

        var waterSnapshotResponse = await client.GetAsync("/api/workspace/snapshot?enterprise=Water%20Utility&fiscalYear=2026");
        waterSnapshotResponse.EnsureSuccessStatusCode();
        var waterSnapshot = await waterSnapshotResponse.Content.ReadFromJsonAsync<WorkspaceBootstrapData>(jsonOptions);

        var wsdSnapshotResponse = await client.GetAsync("/api/workspace/snapshot?enterprise=Wiley%20Sanitation%20District&fiscalYear=2026");
        wsdSnapshotResponse.EnsureSuccessStatusCode();
        var wsdSnapshot = await wsdSnapshotResponse.Content.ReadFromJsonAsync<WorkspaceBootstrapData>(jsonOptions);

        Assert.NotNull(waterSnapshot);
        Assert.NotNull(wsdSnapshot);

        var waterKnowledgeResponse = await client.PostAsJsonAsync(
            "/api/workspace/knowledge",
            new WorkspaceKnowledgeRequest(waterSnapshot, 5, 3),
            jsonOptions);
        var waterKnowledgeBody = await waterKnowledgeResponse.Content.ReadAsStringAsync();
        Assert.True(waterKnowledgeResponse.IsSuccessStatusCode, waterKnowledgeBody);
        var waterKnowledge = await waterKnowledgeResponse.Content.ReadFromJsonAsync<WorkspaceKnowledgeResponse>(jsonOptions);

        var wsdKnowledgeResponse = await client.PostAsJsonAsync(
            "/api/workspace/knowledge",
            new WorkspaceKnowledgeRequest(wsdSnapshot, 5, 3),
            jsonOptions);
        var wsdKnowledgeBody = await wsdKnowledgeResponse.Content.ReadAsStringAsync();
        Assert.True(wsdKnowledgeResponse.IsSuccessStatusCode, wsdKnowledgeBody);
        var wsdKnowledge = await wsdKnowledgeResponse.Content.ReadFromJsonAsync<WorkspaceKnowledgeResponse>(jsonOptions);

        Assert.NotNull(waterKnowledge);
        Assert.NotNull(wsdKnowledge);
        Assert.NotEqual(waterKnowledge.CurrentReserveBalance, wsdKnowledge.CurrentReserveBalance);
    }

    [Fact]
    public async Task UtilityCustomerCrud_ManagesCustomers_EndToEnd()
    {
        await factory.ResetDatabaseAsync(seedData: false);
        using var client = factory.CreateClient();

        var createRequest = new UtilityCustomerUpsertRequest(
            "2001",
            "Alex",
            "Morgan",
            "Wiley Feed & Supply",
            CustomerType.Commercial,
            "12 Main St",
            "Wiley",
            "CO",
            "81092",
            ServiceLocation.InsideCityLimits,
            CustomerStatus.Active,
            125.50m,
            new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            "555-0100",
            "alex@example.com",
            "M-2001",
            "Initial onboarding");

        var createResponse = await client.PostAsJsonAsync("/api/utility-customers", createRequest, jsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<UtilityCustomerRecord>(jsonOptions);
        Assert.NotNull(created);
        Assert.Equal("2001", created.AccountNumber);
        Assert.Equal("Wiley Feed & Supply", created.DisplayName);
        Assert.Equal("Commercial", created.CustomerType);

        var listResponse = await client.GetAsync("/api/utility-customers");
        listResponse.EnsureSuccessStatusCode();
        var listedCustomers = await listResponse.Content.ReadFromJsonAsync<List<UtilityCustomerRecord>>(jsonOptions);

        Assert.NotNull(listedCustomers);
        var listedCustomer = Assert.Single(listedCustomers);
        Assert.Equal(created.Id, listedCustomer.Id);

        var updateRequest = createRequest with
        {
            CurrentBalance = 0m,
            Status = CustomerStatus.Inactive,
            Notes = "Balance cleared"
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/utility-customers/{created.Id}", updateRequest, jsonOptions);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<UtilityCustomerRecord>(jsonOptions);

        Assert.NotNull(updated);
        Assert.Equal("Inactive", updated.Status);
        Assert.Equal(0m, updated.CurrentBalance);
        Assert.Equal("Balance cleared", updated.Notes);

        var getResponse = await client.GetAsync($"/api/utility-customers/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<UtilityCustomerRecord>(jsonOptions);

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Inactive", fetched.Status);

        var deleteResponse = await client.DeleteAsync($"/api/utility-customers/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var missingResponse = await client.GetAsync($"/api/utility-customers/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    private async Task ClearWorkspaceReferenceDataAsync()
    {
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        context.UtilityCustomers.RemoveRange(context.UtilityCustomers);
        context.Enterprises.RemoveRange(context.Enterprises);
        await context.SaveChangesAsync();
    }

    private static string ResolveImportDataPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Import Data"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "Import Data"),
            Path.Combine(Directory.GetCurrentDirectory(), "Import Data"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Import Data")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        var attemptedPaths = string.Join(Environment.NewLine, candidates.Select(Path.GetFullPath));
        Assert.Fail($"Import Data folder was not found. Attempted:{Environment.NewLine}{attemptedPaths}");
        return string.Empty;
    }
}