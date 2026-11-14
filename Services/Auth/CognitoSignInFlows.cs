namespace WileyCoWeb.Services.Auth;

public static class CognitoSignInFlows
{
    public const string HostedUi = "HostedUi";

    public const string UserPassword = "UserPassword";

    public static bool IsHostedUi(string? signInFlow) =>
        string.IsNullOrWhiteSpace(signInFlow)
        || string.Equals(signInFlow.Trim(), HostedUi, StringComparison.OrdinalIgnoreCase);

    public static bool IsUserPassword(string? signInFlow) =>
        string.Equals(signInFlow?.Trim(), UserPassword, StringComparison.OrdinalIgnoreCase);
}
