namespace BuzzMe.Contracts.V1.Auth;

/// <summary>API_CONTRACT.md §5 — POST /v1/auth/reset-password request body.</summary>
public sealed record ResetPasswordRequest(string Token, string NewPassword);
