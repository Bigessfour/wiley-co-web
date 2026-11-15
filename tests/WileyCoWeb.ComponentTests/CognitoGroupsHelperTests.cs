using System.Security.Claims;
using WileyCoWeb.Services.Auth;
using Xunit;

namespace WileyCoWeb.ComponentTests;

public sealed class CognitoGroupsHelperTests
{
    [Theory]
    [InlineData(new[] { "Driver" }, "driver")]
    [InlineData(new[] { "Staff" }, "clerk")]
    [InlineData(new[] { "admin" }, "clerk")]
    [InlineData(new[] { "Driver", "Staff" }, "clerk")]
    [InlineData(new string[] { }, "clerk")]
    public void GetTrashTruckPathSegment_UsesCognitoGroups(string[] groups, string expectedSegment)
    {
        var segment = CognitoGroupsHelper.GetTrashTruckPathSegment(groups);
        Assert.Equal(expectedSegment, segment);
    }

    [Fact]
    public void BuildTrashTruckHubUrl_IncludesFromTownHubWithoutRoleQuery()
    {
        var url = CognitoGroupsHelper.BuildTrashTruckHubUrl("https://trash.example", ["Staff"]);
        Assert.Equal("https://trash.example/clerk?from=town-hub", url);
        Assert.DoesNotContain("role=", url, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTrashTruckHubUrl_DriverOnPhoneUsesIndependentLogin()
    {
        var url = CognitoGroupsHelper.BuildTrashTruckHubUrl("https://trash.example/", ["Driver"], mobileDevice: true);
        Assert.Equal("https://trash.example/login?for=driver", url);
        Assert.DoesNotContain("from=town-hub", url, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTrashTruckHubUrl_DriverAtADeskUsesTownHubSignIn()
    {
        var url = CognitoGroupsHelper.BuildTrashTruckHubUrl("https://trash.example", ["Driver"], mobileDevice: false);
        Assert.Equal("https://trash.example/driver?from=town-hub", url);
    }

    [Fact]
    public void UsesIndependentTrashLogin_StaffOnAPhoneStillUsesTownHub()
    {
        Assert.False(CognitoGroupsHelper.UsesIndependentTrashLogin(["Staff", "Driver"], mobileDevice: true));
        Assert.True(CognitoGroupsHelper.UsesIndependentTrashLogin(["Driver"], mobileDevice: true));
    }

    [Fact]
    public void GetGroups_ReadsMultipleClaims()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("cognito:groups", "Staff"),
            new Claim("cognito:groups", "Driver"),
        ], authenticationType: "test");
        var groups = CognitoGroupsHelper.GetGroups(new ClaimsPrincipal(identity));
        Assert.Equal(2, groups.Count);
        Assert.Contains("Staff", groups);
        Assert.Contains("Driver", groups);
    }
}
