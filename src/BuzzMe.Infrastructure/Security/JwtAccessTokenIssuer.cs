using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BuzzMe.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BuzzMe.Infrastructure.Security;

/// <summary>
/// Mints the Bearer access token BuzzMe.Api's JwtBearer handler already validates
/// (Program.cs's TokenValidationParameters) and HttpCurrentUserContext already reads a
/// ClaimTypes.NameIdentifier claim out of — matching claim type and signing scheme exactly,
/// same as the test-only TestJwtTokenFactory this supersedes as the real implementation.
/// </summary>
public sealed class JwtAccessTokenIssuer(IOptions<JwtIssuerOptions> options) : IAccessTokenIssuer
{
    public string Issue(Guid userId, DateTimeOffset now)
    {
        var jwtOptions = options.Value;
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };

        var token = new JwtSecurityToken(
            jwtOptions.Issuer,
            jwtOptions.Audience,
            claims,
            notBefore: now.UtcDateTime,
            expires: now.UtcDateTime.AddMinutes(jwtOptions.AccessTokenLifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
