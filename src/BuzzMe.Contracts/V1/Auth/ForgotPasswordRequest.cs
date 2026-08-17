namespace BuzzMe.Contracts.V1.Auth;

/// <summary>API_CONTRACT.md §5 — POST /v1/auth/forgot-password request body.</summary>
public sealed record ForgotPasswordRequest(string? Email, string? Phone);
