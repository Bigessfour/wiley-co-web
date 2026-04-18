using System.Net;
using WileyCoWeb.IntegrationTests.Infrastructure;

namespace WileyCoWeb.IntegrationTests;

public sealed class HealthApiTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public HealthApiTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsOk_WithDeterministicStatusText()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = (await response.Content.ReadAsStringAsync()).Trim();

        Assert.False(string.IsNullOrWhiteSpace(payload));
        Assert.True(
            payload.Contains("Healthy", StringComparison.OrdinalIgnoreCase)
            || payload.Contains("Degraded", StringComparison.OrdinalIgnoreCase),
            $"Unexpected /health payload: '{payload}'.");
    }
}