using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace WileyCoWeb.Api.Configuration;

public static class JwtAuthenticationExtensions
{
    public const string WorkspaceMutatingPolicy = "WorkspaceMutating";

    public static IServiceCollection AddWorkspaceJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtAuthenticationOptions>(configuration.GetSection(JwtAuthenticationOptions.SectionName));
        var options = configuration.GetSection(JwtAuthenticationOptions.SectionName).Get<JwtAuthenticationOptions>()
            ?? new JwtAuthenticationOptions();

        if (!options.Enabled)
        {
            services.AddAuthorization();
            return services;
        }

        if (string.IsNullOrWhiteSpace(options.Authority))
        {
            throw new InvalidOperationException(
                "Authentication:Jwt:Enabled is true but Authority is not configured.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.Authority = options.Authority;
                jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = options.Authority.TrimEnd('/'),
                    ValidateIssuer = true,
                    ValidateLifetime = true,
                    ValidateAudience = !string.IsNullOrWhiteSpace(options.Audience),
                    ValidAudience = options.Audience,
                    AudienceValidator = (audiences, securityToken, validationParameters) =>
                    {
                        if (string.IsNullOrWhiteSpace(options.Audience))
                        {
                            return true;
                        }

                        if (audiences.Contains(options.Audience, StringComparer.Ordinal))
                        {
                            return true;
                        }

                        if (securityToken is JwtSecurityToken jwtToken)
                        {
                            var clientId = jwtToken.Claims.FirstOrDefault(claim =>
                                string.Equals(claim.Type, "client_id", StringComparison.Ordinal))?.Value;
                            if (string.Equals(clientId, options.Audience, StringComparison.Ordinal))
                            {
                                return true;
                            }
                        }

                        return false;
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(WorkspaceMutatingPolicy, policy => policy.RequireAuthenticatedUser());

        return services;
    }
}
