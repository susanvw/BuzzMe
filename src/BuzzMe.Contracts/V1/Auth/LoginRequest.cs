namespace BuzzMe.Contracts.V1.Auth;

/// <summary>API_CONTRACT.md §5 — POST /v1/auth/login request body.</summary>
public sealed record LoginRequest(string? Email, string? Phone, string Password);
