using Microsoft.Extensions.Configuration;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Tests;

public sealed class WorkspaceAiKernelFactoryTests
{
    [Fact]
    public void ResolveConfiguration_PrefersCanonicalChatEndpointOverLegacyAlias()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XaiApiEndpoint"] = "https://api.x.ai/v1",
            ["XAI:Endpoint"] = "https://proxy-secondary.example/prod/v1",
            ["XAI:ChatEndpoint"] = "https://proxy-primary.example/prod/v1"
        }).Build();

        var resolved = WorkspaceAiKernelFactory.ResolveConfiguration(configuration);

        Assert.Equal("https://proxy-primary.example/prod/v1", resolved.ChatCompletionEndpoint.ToString());
    }
}