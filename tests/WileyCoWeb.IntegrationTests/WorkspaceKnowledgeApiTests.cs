using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WileyCoWeb.Contracts;
using WileyCoWeb.IntegrationTests.Infrastructure;
using WileyWidget.Services.Abstractions;

namespace WileyCoWeb.IntegrationTests;

public sealed class WorkspaceKnowledgeApiTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory factory;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public WorkspaceKnowledgeApiTests(ApiApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task PostKnowledge_ReturnsNotFound_WhenServiceReportsMissingWorkspaceScope()
    {
        await factory.ResetDatabaseAsync();
        using var client = CreateClientWithKnowledgeService(new NotFoundWorkspaceKnowledgeService());

        var response = await client.PostAsJsonAsync("/api/workspace/knowledge", CreateRequest(), jsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Water Utility", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostKnowledge_ReturnsServiceUnavailable_WhenLiveDataAccessFails()
    {
        await factory.ResetDatabaseAsync();
        using var client = CreateClientWithKnowledgeService(new UnavailableWorkspaceKnowledgeService());

        var response = await client.PostAsJsonAsync("/api/workspace/knowledge", CreateRequest(), jsonOptions);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProblemDetails>(jsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("Workspace knowledge unavailable", payload.Title);
        Assert.Contains("analytics data could not be loaded", payload.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private HttpClient CreateClientWithKnowledgeService(IWorkspaceKnowledgeService knowledgeService)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IWorkspaceKnowledgeService>();
                services.AddSingleton(knowledgeService);
            });
        }).CreateClient();
    }

    private static WorkspaceKnowledgeRequest CreateRequest()
    {
        return new WorkspaceKnowledgeRequest(new WorkspaceBootstrapData(
            "Water Utility",
            2026,
            "Water Utility planning snapshot",
            55.25m,
            13250m,
            240m,
            DateTime.UtcNow.ToString("O")));
    }

    private sealed class NotFoundWorkspaceKnowledgeService : IWorkspaceKnowledgeService
    {
        public Task<WorkspaceKnowledgeResult> BuildAsync(string enterpriseName, int fiscalYear, CancellationToken cancellationToken = default)
            => throw new WorkspaceKnowledgeNotFoundException($"Enterprise '{enterpriseName}' was not found in the live data store.");

        public Task<WorkspaceKnowledgeResult> BuildAsync(WorkspaceKnowledgeInput input, CancellationToken cancellationToken = default)
            => throw new WorkspaceKnowledgeNotFoundException($"Enterprise '{input.SelectedEnterprise}' was not found in the live data store.");
    }

    private sealed class UnavailableWorkspaceKnowledgeService : IWorkspaceKnowledgeService
    {
        public Task<WorkspaceKnowledgeResult> BuildAsync(string enterpriseName, int fiscalYear, CancellationToken cancellationToken = default)
            => throw new WorkspaceKnowledgeUnavailableException("Live workspace knowledge is unavailable because analytics data could not be loaded.");

        public Task<WorkspaceKnowledgeResult> BuildAsync(WorkspaceKnowledgeInput input, CancellationToken cancellationToken = default)
            => throw new WorkspaceKnowledgeUnavailableException("Live workspace knowledge is unavailable because analytics data could not be loaded.");
    }
}