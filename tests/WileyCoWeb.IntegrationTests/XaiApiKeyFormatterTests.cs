using WileyCoWeb.Api.Configuration;

namespace WileyCoWeb.IntegrationTests;

public sealed class XaiApiKeyFormatterTests
{
    [Fact]
    public void ExtractUsableKey_PlainKey_ReturnsTrimmed()
    {
        Assert.Equal("xai-plain", XaiApiKeyFormatter.ExtractUsableKey("  xai-plain  "));
    }

    [Fact]
    public void ExtractUsableKey_JsonEnvelope_ReturnsProperty()
    {
        const string json = """{"XAI_API_KEY":"xai-from-json"}""";
        Assert.Equal("xai-from-json", XaiApiKeyFormatter.ExtractUsableKey(json));
    }

    [Fact]
    public void ExtractUsableKey_JsonUnknownProperties_ReturnsNull()
    {
        Assert.Null(XaiApiKeyFormatter.ExtractUsableKey("""{"other":"v"}"""));
    }
}
