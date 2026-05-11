using WileyCoWeb.Api.Configuration;

namespace WileyCoWeb.IntegrationTests;

public sealed class XaiApiKeyGuardsTests
{
    [Fact]
    public void TryGetRuntimeValidationIssue_ValidBareKey_ReturnsNull()
    {
        Assert.Null(XaiApiKeyGuards.TryGetRuntimeValidationIssue("xai-" + new string('a', 20)));
    }

    [Fact]
    public void TryGetRuntimeValidationIssue_JsonEnvelope_ReturnsIssue()
    {
        Assert.Contains("JSON", XaiApiKeyGuards.TryGetRuntimeValidationIssue("""{"XAI_API_KEY":"x"}""")!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetRuntimeValidationIssue_TooShort_ReturnsIssue()
    {
        Assert.NotNull(XaiApiKeyGuards.TryGetRuntimeValidationIssue("short"));
    }

    [Fact]
    public void TryGetRuntimeValidationIssue_Placeholder_ReturnsIssue()
    {
        Assert.NotNull(XaiApiKeyGuards.TryGetRuntimeValidationIssue("changeme-please-use-real-key-12345"));
    }

    [Fact]
    public void ComputeFingerprint_StableForSameKey()
    {
        var k = "xai-test-key-material-123456789012";
        Assert.Equal(XaiApiKeyGuards.ComputeFingerprint(k), XaiApiKeyGuards.ComputeFingerprint(k));
    }
}
