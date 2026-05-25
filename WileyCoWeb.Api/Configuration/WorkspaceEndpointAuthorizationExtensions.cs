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
}
