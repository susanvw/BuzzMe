using BuzzMe.Contracts.V1.Users;

namespace BuzzMe.Contracts.V1.Auth;

/// <summary>API_CONTRACT.md §5 — VerifyAccount's and Login's shared success shape: `200, { accessToken, refreshToken, user: User }`.</summary>
public sealed record AuthResponse(string AccessToken, string RefreshToken, UserResponse User);
