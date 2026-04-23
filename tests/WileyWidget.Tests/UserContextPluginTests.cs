using Moq;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Plugins;

namespace WileyWidget.Tests;

public sealed class UserContextPluginTests
{
    [Fact]
    public void GetWorkspaceKnowledgeSummaryAsync_UsesWorkspaceKnowledgeService()
    {
        var knowledgeService = new Mock<IWorkspaceKnowledgeService>();
        knowledgeService
            .Setup(service => service.BuildAsync("Water Utility", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKnowledgeResult());

        var plugin = CreatePlugin(knowledgeService: knowledgeService);

        var summary = plugin.GetWorkspaceKnowledgeSummary("Water Utility", 2026);

        Assert.Contains("Workspace summary for Water Utility FY 2026", summary, StringComparison.Ordinal);
        Assert.Contains("Primary action", summary, StringComparison.Ordinal);
        knowledgeService.Verify(service => service.BuildAsync("Water Utility", 2026, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ExplainFinancialIssueAsync_UsesAsyncSystemContext_WhenKnowledgeIsUnavailable()
    {
        var contextService = new Mock<IWileyWidgetContextService>();
        contextService
            .Setup(service => service.BuildCurrentSystemContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Current system context");

        var knowledgeService = new Mock<IWorkspaceKnowledgeService>();
        knowledgeService
            .Setup(service => service.BuildAsync("Water Utility", 2026, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WorkspaceKnowledgeUnavailableException("analytics unavailable"));

        var plugin = CreatePlugin(contextService: contextService, knowledgeService: knowledgeService);

        var explanation = plugin.ExplainFinancialIssue("why is water subsidizing sewer", "Water Utility", 2026);

        Assert.Contains("Grounded Analysis (Context Fallback)", explanation, StringComparison.Ordinal);
        contextService.Verify(service => service.BuildCurrentSystemContextAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GenerateRateRationaleAsync_UsesAsyncEnterpriseContext_WhenKnowledgeIsUnavailable()
    {
        var contextService = new Mock<IWileyWidgetContextService>();
        contextService
            .Setup(service => service.GetEnterpriseContextAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Enterprise context fallback message");

        var knowledgeService = new Mock<IWorkspaceKnowledgeService>();
        knowledgeService
            .Setup(service => service.BuildAsync("Water Utility", 2026, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WorkspaceKnowledgeUnavailableException("analytics unavailable"));

        var plugin = CreatePlugin(contextService: contextService, knowledgeService: knowledgeService);

        var rationale = plugin.GenerateRateRationale("8.5% water rate adjustment", "Water Utility FY2026", "Water Utility", 2026);

        Assert.Contains("Live workspace knowledge was unavailable", rationale, StringComparison.Ordinal);
        contextService.Verify(service => service.GetEnterpriseContextAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static UserContextPlugin CreatePlugin(
        Mock<IUserContext>? userContext = null,
        Mock<IConversationRepository>? conversationRepository = null,
        Mock<IWileyWidgetContextService>? contextService = null,
        Mock<IWorkspaceKnowledgeService>? knowledgeService = null)
    {
        var useDefaultContextService = contextService is null;
        userContext ??= new Mock<IUserContext>();
        conversationRepository ??= new Mock<IConversationRepository>();
        contextService ??= new Mock<IWileyWidgetContextService>();
        knowledgeService ??= new Mock<IWorkspaceKnowledgeService>();

        if (useDefaultContextService)
        {
            contextService
                .Setup(service => service.BuildCurrentSystemContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("Current system context");
            contextService
                .Setup(service => service.GetEnterpriseContextAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Enterprise context");
        }

        return new UserContextPlugin(userContext.Object, conversationRepository.Object, contextService.Object, knowledgeService.Object);
    }

    private static WorkspaceKnowledgeResult CreateKnowledgeResult()
    {
        return new WorkspaceKnowledgeResult(
            "Water Utility",
            2026,
            "Stable",
            "Executive summary",
            "Rate rationale",
            21.5m,
            50000m,
            3000m,
            0m,
            16.67m,
            18.10m,
            -4.83m,
            -3.40m,
            5375m,
            4200m,
            1.12m,
            120000m,
            150000m,
            "Watch",
            DateTime.UtcNow,
            Array.Empty<WorkspaceKnowledgeInsight>(),
            new[] { new WorkspaceKnowledgeAction("Raise rates", "Increase rates gradually.", "High") },
            new[] { new WorkspaceKnowledgeVariance("Chemicals", 1000m, 1200m, 200m, 20m) });
    }
}