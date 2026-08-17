namespace BuzzMe.Application.Auth.Models;

/// <summary>API_CONTRACT.md §5 — Refresh Token's success shape: `200, { accessToken, refreshToken }` (no `user`, unlike AuthResult).</summary>
public sealed record TokenPairResult(string AccessToken, string RefreshToken);
