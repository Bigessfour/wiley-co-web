using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WileyCoWeb.Contracts;
using WileyCoWeb.IntegrationTests.Infrastructure;
using WileyWidget.Data;

namespace WileyCoWeb.IntegrationTests;

public sealed class SharedContractReferenceDataApiTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory factory;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public SharedContractReferenceDataApiTests(ApiApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task PostAndGetActuals_UsesSharedContractsExample_Returns202()
    {
        await factory.ResetDatabaseAsync();
        await ClearSharedContractDocumentsAsync();

        using var client = factory.CreateClient();
        var actuals = LoadExample<ActualsSummaryContract>("actuals-summary-utility-oct-2026.json");

        var post = await client.PostAsJsonAsync("/api/reference-data/actuals", actuals, jsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, post.StatusCode);
        Assert.NotNull(post.Headers.Location);

        var accepted = await post.Content.ReadFromJsonAsync<ReferenceDataIngestAcceptedResponse>(jsonOptions);
        Assert.NotNull(accepted);
        Assert.Equal("actuals-summary", accepted.Kind);
        Assert.NotEqual(Guid.Empty, accepted.Id);

        var byId = await client.GetAsync($"/api/reference-data/actuals/{accepted.Id}");
        byId.EnsureSuccessStatusCode();
        var single = await byId.Content.ReadFromJsonAsync<ReferenceDataActualsDocument>(jsonOptions);
        Assert.NotNull(single);
        Assert.Equal(accepted.Id, single.Id);
        Assert.Equal(["UTILITY", "UTILITY SAVINGS"], single.Actuals.CashBalanceAccounts);

        var get = await client.GetAsync("/api/reference-data/actuals");
        get.EnsureSuccessStatusCode();
        var list = await get.Content.ReadFromJsonAsync<List<ReferenceDataActualsDocument>>(jsonOptions);

        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(accepted.Id, list[0].Id);
        Assert.Equal("tow-water-utility", list[0].Actuals.EnterpriseId);
        Assert.Equal("2026-10", list[0].Actuals.Period);
        Assert.Equal("fy-ytd", list[0].Actuals.AmountsBasis);
        Assert.Equal(259177.18m, list[0].Actuals.Income);
        Assert.Equal(188843.30m, list[0].Actuals.Expenses);
        Assert.Equal(152007.37m, list[0].Actuals.CashBalance);
    }

    [Fact]
    public async Task PostAndGetEvents_UsesSharedContractsExampleItems_Returns202()
    {
        await factory.ResetDatabaseAsync();
        await ClearSharedContractDocumentsAsync();

        using var client = factory.CreateClient();
        var events = await LoadOperationalEventsExampleAsync();
        Assert.True(events.Count >= 2);

        var post = await client.PostAsJsonAsync("/api/reference-data/events", events, jsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, post.StatusCode);
        Assert.NotNull(post.Headers.Location);

        var accepted = await post.Content.ReadFromJsonAsync<ReferenceDataIngestAcceptedResponse>(jsonOptions);
        Assert.NotNull(accepted);
        Assert.Equal("operational-events", accepted.Kind);

        var byId = await client.GetAsync($"/api/reference-data/events/{accepted.Id}");
        byId.EnsureSuccessStatusCode();

        var get = await client.GetAsync("/api/reference-data/events");
        get.EnsureSuccessStatusCode();
        var list = await get.Content.ReadFromJsonAsync<List<ReferenceDataEventsDocument>>(jsonOptions);

        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(accepted.Id, list[0].Id);
        Assert.Equal(events.Count, list[0].Events.Count);
        Assert.Equal("check", list[0].Events[0].Type);
        Assert.Equal(344.84m, list[0].Events[0].Amount);

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var row = await context.SharedContractDocuments.SingleAsync(d => d.Kind == "operational-events");
        Assert.Null(row.EnterpriseId);
    }

    [Fact]
    public async Task PostEvents_ReturnsBadRequest_WhenBodyIsEmpty()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/reference-data/events",
            Array.Empty<OperationalEventContract>(),
            jsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostActuals_ReturnsBadRequest_WhenRequiredFieldsMissing()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/reference-data/actuals",
            new ActualsSummaryContract("", "2026-10", "fy-ytd", 1m, 1m, 1m),
            jsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostActuals_ReturnsBadRequest_WhenAmountsBasisInvalid()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/reference-data/actuals",
            new ActualsSummaryContract("tow-water-utility", "2026-10", "weekly", 1m, 1m, 1m),
            jsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetActualsById_ReturnsNotFound_ForUnknownId()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/reference-data/actuals/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WorkspaceReferenceDataImport_DoesNotCreateSharedContractDocuments()
    {
        await factory.ResetDatabaseAsync();
        await ClearSharedContractDocumentsAsync();

        using var client = factory.CreateClient();
        var importPath = ResolveImportDataPath();
        if (importPath is null)
        {
            return;
        }

        var response = await client.PostAsJsonAsync(
            "/api/workspace/reference-data/import",
            new WorkspaceReferenceDataImportRequest(importPath),
            jsonOptions);
        response.EnsureSuccessStatusCode();

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal(0, await context.SharedContractDocuments.CountAsync());
    }

    private async Task ClearSharedContractDocumentsAsync()
    {
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        context.SharedContractDocuments.RemoveRange(context.SharedContractDocuments);
        await context.SaveChangesAsync();
    }

    private async Task<List<OperationalEventContract>> LoadOperationalEventsExampleAsync()
    {
        var batchJson = await File.ReadAllTextAsync(ResolveSharedContractsExamplePath("operational-events-oct-2026-checks.json"));
        using var batchDoc = JsonDocument.Parse(batchJson);
        var items = batchDoc.RootElement.GetProperty("items");
        var events = JsonSerializer.Deserialize<List<OperationalEventContract>>(items.GetRawText(), jsonOptions);
        Assert.NotNull(events);
        return events;
    }

    private static T LoadExample<T>(string fileName)
    {
        var path = ResolveSharedContractsExamplePath(fileName);
        var json = File.ReadAllText(path);
        var value = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(value);
        return value;
    }

    private static string ResolveSharedContractsExamplePath(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "shared-contracts", "examples", fileName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "shared-contracts", "examples", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "shared-contracts", "examples", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "shared-contracts", "examples", fileName)
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        var attempted = string.Join(Environment.NewLine, candidates.Select(Path.GetFullPath));
        Assert.Fail($"shared-contracts example '{fileName}' was not found. Attempted:{Environment.NewLine}{attempted}");
        return string.Empty;
    }

    private static string? ResolveImportDataPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Import Data"),
            Path.Combine(Directory.GetCurrentDirectory(), "Import Data")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}
