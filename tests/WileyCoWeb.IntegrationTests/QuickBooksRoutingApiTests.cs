using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WileyCoWeb.Contracts;
using WileyCoWeb.IntegrationTests.Infrastructure;
using WileyWidget.Data;

namespace WileyCoWeb.IntegrationTests;

public sealed class QuickBooksRoutingApiTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory factory;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public QuickBooksRoutingApiTests(ApiApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task RoutingConfiguration_SaveAndGet_RoundTripsRulesAndAllocationProfiles()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var request = CreateBrooksideRoutingConfiguration();

        var saveResponse = await client.PutAsJsonAsync("/api/imports/quickbooks/routing", request);
        saveResponse.EnsureSuccessStatusCode();

        var saved = await saveResponse.Content.ReadFromJsonAsync<QuickBooksRoutingConfigurationResponse>(jsonOptions);
        Assert.NotNull(saved);
        Assert.Single(saved.Rules);
        Assert.Single(saved.AllocationProfiles);
        Assert.Equal("Saved QuickBooks routing configuration.", saved.StatusMessage);

        var getResponse = await client.GetAsync("/api/imports/quickbooks/routing");
        getResponse.EnsureSuccessStatusCode();

        var loaded = await getResponse.Content.ReadFromJsonAsync<QuickBooksRoutingConfigurationResponse>(jsonOptions);
        Assert.NotNull(loaded);
        Assert.Single(loaded.Rules);
        Assert.Single(loaded.AllocationProfiles);
        Assert.Equal("Brookside to Apartments", loaded.Rules[0].Name);
        Assert.Equal("Brookside overhead split", loaded.AllocationProfiles[0].Name);
    }

    [Fact]
    public async Task Preview_AppliesConfiguredRoutingRule_ThroughRealApi()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var saveResponse = await client.PutAsJsonAsync("/api/imports/quickbooks/routing", new QuickBooksRoutingConfigurationRequest
        {
            Rules =
            [
                new QuickBooksRoutingRuleDefinition
                {
                    Name = "Brookside to Apartments",
                    Priority = 10,
                    MemoPattern = "BROOKSIDE",
                    TargetEnterprise = "Apartments",
                    IsActive = true
                }
            ]
        });
        saveResponse.EnsureSuccessStatusCode();

        var response = await PostImportAsync(client, "/api/imports/quickbooks/preview", CreateBrooksideCsv(), "brookside-journal.csv");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<QuickBooksImportPreviewResponse>(jsonOptions);
        Assert.NotNull(payload);
        var row = Assert.Single(payload.Rows);
        Assert.Equal("Apartments", row.RoutedEnterprise);
        Assert.Equal("Brookside to Apartments", row.RoutingRuleName);
        Assert.Contains("Matched rule", row.RoutingReason);
        Assert.Equal(-1000.00m, row.SourceAmount);
    }

    [Fact]
    public async Task Commit_PersistsAllocatedRows_AndHistoryReflectsSplitScopes()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var configuration = new QuickBooksRoutingConfigurationRequest
        {
            AllocationProfiles =
            [
                new QuickBooksAllocationProfileDefinition
                {
                    Id = 1,
                    Name = "Administrative split",
                    Description = "Distribute shared overhead between Trash and Apartments.",
                    IsActive = true,
                    Targets =
                    [
                        new QuickBooksAllocationTargetDefinition { EnterpriseName = "Trash", AllocationPercent = 60m },
                        new QuickBooksAllocationTargetDefinition { EnterpriseName = "Apartments", AllocationPercent = 40m }
                    ]
                }
            ],
            Rules =
            [
                new QuickBooksRoutingRuleDefinition
                {
                    Name = "Administrative overhead split",
                    Priority = 10,
                    AccountPattern = "OTHER ADMINISTRATIVE EXPENSES",
                    AllocationProfileId = 1,
                    TargetEnterprise = "Water Utility",
                    IsActive = true
                }
            ]
        };

        var saveResponse = await client.PutAsJsonAsync("/api/imports/quickbooks/routing", configuration);
        saveResponse.EnsureSuccessStatusCode();

        var commitResponse = await PostImportAsync(client, "/api/imports/quickbooks/commit", CreateAdministrativeOverheadCsv(), "admin-overhead.csv");
        commitResponse.EnsureSuccessStatusCode();

        var commit = await commitResponse.Content.ReadFromJsonAsync<QuickBooksImportCommitResponse>(jsonOptions);
        Assert.NotNull(commit);
        Assert.Equal(2, commit.ImportedRows);

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var entries = await context.LedgerEntries
                .OrderBy(item => item.Id)
                .ToListAsync();

            Assert.Equal(2, entries.Count);
            Assert.Equal(new[] { "Apartments", "Trash" }, entries.Select(item => item.EntryScope).OrderBy(item => item).ToArray());
            Assert.All(entries, entry => Assert.Equal("Water Utility", entry.OriginalEntryScope));
            Assert.All(entries, entry => Assert.Equal(100.00m, entry.SourceAmount));
            Assert.All(entries, entry => Assert.Equal("Administrative overhead split", entry.AppliedRoutingRuleName));
            Assert.All(entries, entry => Assert.Equal("Administrative split", entry.AppliedAllocationProfileName));
            Assert.Contains(entries, entry => entry.EntryScope == "Trash" && entry.Amount == 60.00m && entry.RoutingAllocationPercent == 60m);
            Assert.Contains(entries, entry => entry.EntryScope == "Apartments" && entry.Amount == 40.00m && entry.RoutingAllocationPercent == 40m);
        }

        var historyResponse = await client.GetAsync("/api/imports/quickbooks/history");
        historyResponse.EnsureSuccessStatusCode();

        var history = await historyResponse.Content.ReadFromJsonAsync<QuickBooksImportHistoryResponse>(jsonOptions);
        Assert.NotNull(history);
        var item = Assert.Single(history.Items);
        Assert.Contains("Apartments", item.ScopeSummary);
        Assert.Contains("Trash", item.ScopeSummary);
    }

    [Fact]
    public async Task HistoricalReroute_RewritesExistingLedgerScopes_UsingUpdatedRules()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var initialCommitResponse = await PostImportAsync(client, "/api/imports/quickbooks/commit", CreateBrooksideCsv(), "brookside-journal.csv");
        initialCommitResponse.EnsureSuccessStatusCode();

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        long sourceFileId;

        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var originalEntry = await context.LedgerEntries.SingleAsync();
            Assert.Equal("Water Utility", originalEntry.EntryScope);
            Assert.Equal("Water Utility", originalEntry.OriginalEntryScope);

            sourceFileId = await context.SourceFiles.Select(item => item.Id).SingleAsync();
        }

        var saveResponse = await client.PutAsJsonAsync("/api/imports/quickbooks/routing", new QuickBooksRoutingConfigurationRequest
        {
            Rules =
            [
                new QuickBooksRoutingRuleDefinition
                {
                    Name = "Brookside to Apartments",
                    Priority = 10,
                    MemoPattern = "BROOKSIDE",
                    TargetEnterprise = "Apartments",
                    IsActive = true
                }
            ]
        });
        saveResponse.EnsureSuccessStatusCode();

        var rerouteResponse = await client.PostAsJsonAsync("/api/imports/quickbooks/reroute", new QuickBooksHistoricalRerouteRequest
        {
            SourceFileId = sourceFileId
        });
        rerouteResponse.EnsureSuccessStatusCode();

        var reroute = await rerouteResponse.Content.ReadFromJsonAsync<QuickBooksHistoricalRerouteResponse>(jsonOptions);
        Assert.NotNull(reroute);
        Assert.Equal(1, reroute.SourceRowCount);
        Assert.Equal(1, reroute.RoutedRowCount);

        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var reroutedEntry = await context.LedgerEntries.SingleAsync();
            Assert.Equal("Apartments", reroutedEntry.EntryScope);
            Assert.Equal("Water Utility", reroutedEntry.OriginalEntryScope);
            Assert.Equal("Brookside to Apartments", reroutedEntry.AppliedRoutingRuleName);
            Assert.Equal(-1000.00m, reroutedEntry.SourceAmount);
        }
    }

    [Fact]
    public async Task HistoricalReroute_UpdatesWorkspaceKnowledgeReserveAnalytics_ForAffectedEnterprises()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var initialCommitResponse = await PostImportAsync(client, "/api/imports/quickbooks/commit", CreateReserveTransferCsv(), "reserve-transfer.csv");
        initialCommitResponse.EnsureSuccessStatusCode();

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        long sourceFileId;

        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var importedEntry = await context.LedgerEntries.SingleAsync();
            Assert.Equal("Water Utility", importedEntry.EntryScope);
            Assert.Equal("101 · CASH IN BANK - UTILITY", importedEntry.AccountName);

            sourceFileId = await context.SourceFiles.Select(item => item.Id).SingleAsync();
        }

        var waterKnowledgeBefore = await GetKnowledgeAsync(client, "Water Utility", 2026);
        var apartmentsKnowledgeBefore = await GetKnowledgeAsync(client, "Apartments", 2026);

        Assert.Equal(7500.00m, waterKnowledgeBefore.CurrentReserveBalance);
        Assert.Equal(0m, apartmentsKnowledgeBefore.CurrentReserveBalance);

        var saveResponse = await client.PutAsJsonAsync("/api/imports/quickbooks/routing", new QuickBooksRoutingConfigurationRequest
        {
            Rules =
            [
                new QuickBooksRoutingRuleDefinition
                {
                    Name = "Reserve transfer to Apartments",
                    Priority = 10,
                    MemoPattern = "APARTMENT RESERVE",
                    TargetEnterprise = "Apartments",
                    IsActive = true
                }
            ]
        });
        saveResponse.EnsureSuccessStatusCode();

        var rerouteResponse = await client.PostAsJsonAsync("/api/imports/quickbooks/reroute", new QuickBooksHistoricalRerouteRequest
        {
            SourceFileId = sourceFileId
        });
        rerouteResponse.EnsureSuccessStatusCode();

        var waterKnowledgeAfter = await GetKnowledgeAsync(client, "Water Utility", 2026);
        var apartmentsKnowledgeAfter = await GetKnowledgeAsync(client, "Apartments", 2026);

        Assert.Equal(0m, waterKnowledgeAfter.CurrentReserveBalance);
        Assert.Equal(7500.00m, apartmentsKnowledgeAfter.CurrentReserveBalance);
        Assert.NotEqual(waterKnowledgeBefore.CurrentReserveBalance, waterKnowledgeAfter.CurrentReserveBalance);
        Assert.NotEqual(apartmentsKnowledgeBefore.CurrentReserveBalance, apartmentsKnowledgeAfter.CurrentReserveBalance);
    }

    private static QuickBooksRoutingConfigurationRequest CreateBrooksideRoutingConfiguration()
    {
        return new QuickBooksRoutingConfigurationRequest
        {
            AllocationProfiles =
            [
                new QuickBooksAllocationProfileDefinition
                {
                    Id = 1,
                    Name = "Brookside overhead split",
                    Description = "Reserve placeholder for Brookside overhead.",
                    IsActive = true,
                    Targets =
                    [
                        new QuickBooksAllocationTargetDefinition { EnterpriseName = "Apartments", AllocationPercent = 100m }
                    ]
                }
            ],
            Rules =
            [
                new QuickBooksRoutingRuleDefinition
                {
                    Name = "Brookside to Apartments",
                    Priority = 10,
                    MemoPattern = "BROOKSIDE",
                    TargetEnterprise = "Apartments",
                    AllocationProfileId = 1,
                    IsActive = true
                }
            ]
        };
    }

    private static async Task<HttpResponseMessage> PostImportAsync(HttpClient client, string requestUri, string csvContent, string fileName)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent("Water Utility"), "selectedEnterprise");
        form.Add(new StringContent("2026"), "selectedFiscalYear");

        return await client.PostAsync(requestUri, form);
    }

    private async Task<WorkspaceKnowledgeResponse> GetKnowledgeAsync(HttpClient client, string enterpriseName, int fiscalYear)
    {
        var snapshotResponse = await client.GetAsync($"/api/workspace/snapshot?enterprise={Uri.EscapeDataString(enterpriseName)}&fiscalYear={fiscalYear}");
        snapshotResponse.EnsureSuccessStatusCode();

        var snapshot = await snapshotResponse.Content.ReadFromJsonAsync<WorkspaceBootstrapData>(jsonOptions);
        Assert.NotNull(snapshot);

        var knowledgeResponse = await client.PostAsJsonAsync(
            "/api/workspace/knowledge",
            new WorkspaceKnowledgeRequest(snapshot, 5, 2),
            jsonOptions);
        knowledgeResponse.EnsureSuccessStatusCode();

        var knowledge = await knowledgeResponse.Content.ReadFromJsonAsync<WorkspaceKnowledgeResponse>(jsonOptions);
        Assert.NotNull(knowledge);
        return knowledge;
    }

    private static string CreateBrooksideCsv()
    {
        return "Date,Type,Num,Name,Memo,Account,Split,Amount,Balance,Clr\n"
            + "01/30/2026,General Journal,364,Town of Wiley,BROOKSIDE MANAGEMENT FEE,366 · BCL MANAGEMENT FEE,222 · DUE TO/FROM WSD,-1000.00,-1000.00,C\n";
    }

    private static string CreateAdministrativeOverheadCsv()
    {
        return "Date,Type,Num,Name,Memo,Account,Split,Amount,Balance,Clr\n"
            + "02/28/2026,Check,9999,Town of Wiley,Service Charge,467 · OTHER ADMINISTRATIVE EXPENSES,101 · CASH IN BANK - UTILITY,100.00,100.00,C\n";
    }

    private static string CreateReserveTransferCsv()
    {
        return "Date,Type,Num,Name,Memo,Account,Split,Amount,Balance,Clr\n"
            + "03/15/2026,General Journal,4100,Town of Wiley,APARTMENT RESERVE TRANSFER,101 · CASH IN BANK - UTILITY,300 · OPERATING RESERVES,7500.00,7500.00,C\n";
    }
}