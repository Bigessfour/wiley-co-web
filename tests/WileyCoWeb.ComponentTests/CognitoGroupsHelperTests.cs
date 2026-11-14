using System.Security.Claims;
using WileyCoWeb.Services.Auth;
using Xunit;

namespace WileyCoWeb.ComponentTests;

public sealed class CognitoGroupsHelperTests
{
    [Theory]
    [InlineData(new[] { "Driver" }, "driver")]
    [InlineData(new[] { "Clerk" }, "clerk")]
    [InlineData(new[] { "admin" }, "clerk")]
    [InlineData(new[] { "Driver", "Clerk" }, "clerk")]
    [InlineData(new string[] { }, "clerk")]
    public void GetTrashTruckPathSegment_UsesCognitoGroups(string[] groups, string expectedSegment)
    {
        var segment = CognitoGroupsHelper.GetTrashTruckPathSegment(groups);
        Assert.Equal(expectedSegment, segment);
    }

    [Fact]
    public void BuildTrashTruckHubUrl_IncludesFromTownHubWithoutRoleQuery()
    {
        var url = CognitoGroupsHelper.BuildTrashTruckHubUrl("https://trash.example", ["Clerk"]);
        Assert.Equal("https://trash.example/clerk?from=town-hub", url);
        Assert.DoesNotContain("role=", url, StringComparison.Ordinal);
    }

    [Fact]
    public void GetGroups_ReadsMultipleClaims()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("cognito:groups", "Clerk"),
            new Claim("cognito:groups", "Driver"),
        ], authenticationType: "test");
        var groups = CognitoGroupsHelper.GetGroups(new ClaimsPrincipal(identity));
        Assert.Equal(2, groups.Count);
        Assert.Contains("Clerk", groups);
        Assert.Contains("Driver", groups);
    }
}
