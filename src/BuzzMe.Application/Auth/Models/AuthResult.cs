using BuzzMe.Application.Users.Models;

namespace BuzzMe.Application.Auth.Models;

/// <summary>API_CONTRACT.md §5 — VerifyAccount's and Login's shared success shape: `200, { accessToken, refreshToken, user: User }`.</summary>
public sealed record AuthResult(string AccessToken, string RefreshToken, UserResult User);
