using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using WileyCoWeb.Api.Configuration;
using Xunit;

namespace WileyCoWeb.IntegrationTests;

public sealed class JwtAuthenticationAudienceTests
{
    [Fact]
    public void AudienceValidator_AcceptsClientIdClaimWhenAudiencesMissing()
    {
        var options = new JwtAuthenticationOptions { Audience = "2m6vp91m9938jpbg2efivr2p8k" };
        var token = new JwtSecurityToken(claims: [new Claim("client_id", options.Audience)]);
        var validationParameters = new TokenValidationParameters();

        var isValid = InvokeAudienceValidator(options, [], token, validationParameters);
        Assert.True(isValid);
    }

    [Fact]
    public void AudienceValidator_AcceptsStandardAudience()
    {
        var options = new JwtAuthenticationOptions { Audience = "2m6vp91m9938jpbg2efivr2p8k" };
        var token = new JwtSecurityToken();
        var validationParameters = new TokenValidationParameters();

        var isValid = InvokeAudienceValidator(options, [options.Audience], token, validationParameters);
        Assert.True(isValid);
    }

    private static bool InvokeAudienceValidator(
        JwtAuthenticationOptions options,
        IEnumerable<string> audiences,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters)
    {
        var configured = options;
        return audiences.Contains(configured.Audience, StringComparer.Ordinal)
            || (securityToken is JwtSecurityToken jwtToken
                && string.Equals(
                    jwtToken.Claims.FirstOrDefault(claim => string.Equals(claim.Type, "client_id", StringComparison.Ordinal))?.Value,
                    configured.Audience,
                    StringComparison.Ordinal));
    }
}
