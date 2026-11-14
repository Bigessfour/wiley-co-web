using Bunit;
using WileyCoWeb.Components.Auth;
using Xunit;

namespace WileyCoWeb.ComponentTests;

[Trait("Category", "HighRisk")]
public sealed class SignInFailureTests : TestContext
{
    [Fact]
    public void LoginFailed_TellsTheClerkWhoToCall()
    {
        var cut = RenderComponent<SignInFailure>(parameters => parameters
            .Add(component => component.Action, "login-failed")
            .Add(component => component.Detail, "User does not exist."));

        Assert.Contains("Sign-in did not finish", cut.Markup);
        Assert.Contains("User does not exist.", cut.Markup);
        Assert.Contains("clerk@townofwiley.gov", cut.Markup);
        Assert.Contains("(719) 829-4974", cut.Markup);
        Assert.Contains("tel:+17198294974", cut.Markup);
        Assert.Contains("304 Main Street", cut.Markup);
    }

    [Fact]
    public void LogoutFailed_UsesTheSignOutHeadline()
    {
        var cut = RenderComponent<SignInFailure>(parameters => parameters
            .Add(component => component.Action, "logout-failed"));

        Assert.Contains("Sign-out did not finish", cut.Markup);
        Assert.Contains("clerk@townofwiley.gov", cut.Markup);
    }
}
