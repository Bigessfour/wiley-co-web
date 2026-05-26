using Microsoft.AspNetCore.Authorization;

namespace WileyCoWeb.Api.Configuration;

public static class WorkspaceEndpointAuthorizationExtensions
{
    public static RouteHandlerBuilder RequireWorkspaceMutatingAuth(
        this RouteHandlerBuilder builder,
        IConfiguration configuration)
    {
        if (configuration.GetValue<bool>($"{JwtAuthenticationOptions.SectionName}:Enabled"))
        {
            return builder.RequireAuthorization(JwtAuthenticationExtensions.WorkspaceMutatingPolicy);
        }

        return builder;
    }

    /// <summary>
    /// Applies WorkspaceReadPolicy (RequireAuthenticatedUser) only when Authentication:Jwt:Enabled.
    /// In Development with JWT disabled, leaves endpoint open (existing pattern).
    /// </summary>
    public static RouteHandlerBuilder RequireWorkspaceReadAuth(
        this RouteHandlerBuilder builder,
        IConfiguration configuration)
    {
        if (configuration.GetValue<bool>($"{JwtAuthenticationOptions.SectionName}:Enabled"))
        {
            return builder.RequireAuthorization(JwtAuthenticationExtensions.WorkspaceReadPolicy);
        }

        return builder;
    }
}
