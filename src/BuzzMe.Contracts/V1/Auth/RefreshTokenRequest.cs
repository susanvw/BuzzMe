namespace BuzzMe.Contracts.V1.Auth;

/// <summary>API_CONTRACT.md §5 — POST /v1/auth/refresh-token request body.</summary>
public sealed record RefreshTokenRequest(string RefreshToken);
