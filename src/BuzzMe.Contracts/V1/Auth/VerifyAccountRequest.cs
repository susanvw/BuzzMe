namespace BuzzMe.Contracts.V1.Auth;

/// <summary>API_CONTRACT.md §5 — POST /v1/auth/verify request body.</summary>
public sealed record VerifyAccountRequest(string? Email, string? Phone, string Code);
