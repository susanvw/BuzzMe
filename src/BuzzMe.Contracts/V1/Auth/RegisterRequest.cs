namespace BuzzMe.Contracts.V1.Auth;

/// <summary>API_CONTRACT.md §5 — POST /v1/auth/register request body.</summary>
public sealed record RegisterRequest(string DisplayName, string? Email, string? Phone, string Password);
