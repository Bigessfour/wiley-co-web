using WileyCoWeb.Services.Auth;
using Xunit;

namespace WileyCoWeb.ComponentTests;

[Trait("Category", "HighRisk")]
public sealed class CognitoHostedUiTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://REPLACE_WITH_COGNITO_HOSTED_UI_PREFIX.auth.us-east-2.amazoncognito.com")]
    [InlineData("http://tow-staff.auth.us-east-2.amazoncognito.com")]
    [InlineData("https://example.com")]
    public void IsConfiguredDomain_RejectsPlaceholderAndNonCognitoHosts(string? domain)
    {
        Assert.False(CognitoHostedUi.IsConfiguredDomain(domain));
    }

    [Fact]
    public void IsConfiguredDomain_AcceptsHttpsCognitoHost()
    {
        Assert.True(CognitoHostedUi.IsConfiguredDomain("https://tow-staff.auth.us-east-2.amazoncognito.com"));
    }

    [Fact]
    public void BuildGlobalLogoutUrl_IncludesClientAndLogoutUri()
    {
        var url = CognitoHostedUi.BuildGlobalLogoutUrl(
            "https://tow-staff.auth.us-east-2.amazoncognito.com/",
            "client-id",
            "https://wileywidget.townofwiley.gov/");

        Assert.Equal(
            "https://tow-staff.auth.us-east-2.amazoncognito.com/logout?client_id=client-id&logout_uri=https%3A%2F%2Fwileywidget.townofwiley.gov%2F",
            url);
    }

    [Fact]
    public void BuildGlobalLogoutUrl_DoesNotTargetThePlaceholderHost()
    {
        var url = CognitoHostedUi.BuildGlobalLogoutUrl(
            "https://REPLACE_WITH_COGNITO_HOSTED_UI_PREFIX.auth.us-east-2.amazoncognito.com",
            "2m6vp91m9938jpbg2efivr2p8k",
            "https://wileywidget.townofwiley.gov/");

        Assert.Null(url);
    }
}
