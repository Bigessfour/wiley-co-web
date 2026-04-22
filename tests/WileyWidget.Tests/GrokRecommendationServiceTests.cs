using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Business.Services;

namespace WileyWidget.Tests;

public sealed class GrokRecommendationServiceTests
{
    [Fact]
    public void Constructor_PrefersCanonicalChatEndpointOverLegacyAlias()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XaiApiEndpoint"] = "https://api.x.ai/v1",
            ["XAI:Endpoint"] = "https://proxy-secondary.example/prod/v1",
            ["XAI:ChatEndpoint"] = "https://proxy-primary.example/prod/v1"
        }).Build();

        var service = new GrokRecommendationService(
            apiKeyProvider: null,
            logger: NullLogger<GrokRecommendationService>.Instance,
            configuration: configuration);

        var endpointField = typeof(GrokRecommendationService).GetField("_endpoint", BindingFlags.Instance | BindingFlags.NonPublic);
        var endpoint = Assert.IsType<Uri>(endpointField?.GetValue(service));

        Assert.Equal(new Uri("https://proxy-primary.example/prod/v1/chat/completions"), endpoint);
    }
}
