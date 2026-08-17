using BuzzMe.Application.Auth.Models;
using BuzzMe.Contracts.V1.Auth;

namespace BuzzMe.Api.Mapping;

/// <summary>Application → Contracts mapping for Auth — extension methods, not a generic mapper (DEVELOPMENT_GUIDE.md §3).</summary>
public static class AuthMapping
{
    public static RegisterResponse ToResponse(this RegisterResult result) => new(result.UserId);

    public static AuthResponse ToResponse(this AuthResult result) =>
        new(result.AccessToken, result.RefreshToken, result.User.ToResponse());

    public static TokenPairResponse ToResponse(this TokenPairResult result) => new(result.AccessToken, result.RefreshToken);
}
