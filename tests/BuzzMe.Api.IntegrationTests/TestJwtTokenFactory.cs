using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BuzzMe.Api.IntegrationTests;

/// <summary>
/// Mints a validly-signed access token directly, matching the signing key
/// BuzzMeApiFactory configures the host with. Sprint 1 explicitly assumes authentication
/// already succeeds and does not implement Login — this is test infrastructure standing in
/// for "the user is already signed in," not an implementation of the login use case.
/// </summary>
public static class TestJwtTokenFactory
{
    public static string CreateAccessToken(Guid userId, string issuer, string audience, string signingKey)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };

        var token = new JwtSecurityToken(
            issuer, audience, claims, expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
