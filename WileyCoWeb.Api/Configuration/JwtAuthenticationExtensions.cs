using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace WileyCoWeb.Api.Configuration;

public static class JwtAuthenticationExtensions
{
    public const string WorkspaceMutatingPolicy = "WorkspaceMutating";
    public const string WorkspaceReadPolicy = "WorkspaceRead";

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

        // Fix 2 (P1 JWT null safety): safely extract + validate Authority (prevents any NRE on null/whitespace inside check block);
        // preserves test-dummy (example.invalid) intent for 401 integration tests; throws clear InvalidOperation on real misconfig.
        string? authority = options.Authority?.Trim();
        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new InvalidOperationException(
                "Authentication:Jwt:Enabled is true but Authority is not configured.");
        }

        // Allow test dummies (e.g. example.invalid) used by integration auth/401 tests without requiring a live IdP.
        bool isTestDummyAuthority = authority.Contains("example.invalid", StringComparison.OrdinalIgnoreCase);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.Authority = authority;
                jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = authority.TrimEnd('/'),
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
            .AddPolicy(WorkspaceMutatingPolicy, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(WorkspaceReadPolicy, policy => policy.RequireAuthenticatedUser());

        return services;
    }
}
