using Bunit;
using WileyCoWeb.Components.Pages;
using Xunit;

namespace WileyCoWeb.ComponentTests;

public sealed class SupportPageTests : TestContext
{
    [Fact]
    [Trait("Category", "HighRisk")]
    public void SupportPage_NamesTheTownClerk()
    {
        var cut = RenderComponent<Support>();

        Assert.Contains("Who to call", cut.Markup);
        Assert.Contains("(719) 829-4974", cut.Markup);
        Assert.Contains("mailto:clerk@townofwiley.gov", cut.Markup);
        Assert.Contains("304 Main Street", cut.Markup);
    }
}
