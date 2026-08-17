namespace BuzzMe.Contracts.V1.Auth;

/// <summary>API_CONTRACT.md §5 — Refresh Token's success shape: `200, { accessToken, refreshToken }` (no `user`, unlike AuthResponse).</summary>
public sealed record TokenPairResponse(string AccessToken, string RefreshToken);
