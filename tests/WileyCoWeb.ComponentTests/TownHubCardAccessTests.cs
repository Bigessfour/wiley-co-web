using System.Security.Claims;
using WileyCoWeb.Services.Auth;
using Xunit;

namespace WileyCoWeb.ComponentTests;

[Trait("Category", "HighRisk")]
public sealed class TownHubCardAccessTests
{
    [Theory]
    [InlineData("01", new[] { "Driver" }, false)]
    [InlineData("02", new[] { "Driver" }, false)]
    [InlineData("03", new[] { "Driver" }, false)]
    [InlineData("05", new[] { "Driver" }, false)]
    [InlineData("04", new[] { "Driver" }, true)]
    [InlineData("01", new[] { "Clerk" }, true)]
    [InlineData("04", new[] { "Clerk" }, true)]
    [InlineData("02", new[] { "admin" }, true)]
    [InlineData("01", new string[] { }, true)]
    public void IsVisible_UsesCognitoGroupsWhenAuthIsEnabled(string appId, string[] groups, bool expected)
    {
        var visible = TownHubCardAccess.IsVisible(appId, allowedGroups: null, groups, authEnabled: true);
        Assert.Equal(expected, visible);
    }

    [Fact]
    public void IsVisible_ShowsEveryCardWhenAuthIsDisabled()
    {
        Assert.True(TownHubCardAccess.IsVisible("01", null, ["Driver"], authEnabled: false));
    }

    [Theory]
    [InlineData("http://mr-storage:8082", true)]
    [InlineData("http://mr-storage:8080", true)]
    [InlineData("http://localhost:8082", false)]
    [InlineData("https://main.d2dke4s7zyafra.amplifyapp.com", false)]
    [InlineData("/wiley-workspace", false)]
    [InlineData("", false)]
    public void IsOfficeNetworkOnly_MatchesTailnetHttpLinks(string url, bool expected)
    {
        Assert.Equal(expected, TownHubCardAccess.IsOfficeNetworkOnly(url));
    }

    [Fact]
    public void GetGroups_ReadsHostedUiRoleClaims()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Clerk")],
            authenticationType: "test");
        var groups = CognitoGroupsHelper.GetGroups(new ClaimsPrincipal(identity));
        Assert.Contains("Clerk", groups);
    }

    [Fact]
    public void GetGroups_ParsesJsonArrayClaim()
    {
        var identity = new ClaimsIdentity(
            [new Claim("cognito:groups", "[\"Driver\",\"Clerk\"]")],
            authenticationType: "test");
        var groups = CognitoGroupsHelper.GetGroups(new ClaimsPrincipal(identity));
        Assert.Contains("Driver", groups);
        Assert.Contains("Clerk", groups);
    }
}
