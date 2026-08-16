namespace BuzzMe.Contracts.V1.Users;

/// <summary>API_CONTRACT.md §5 — PATCH /v1/users/me request body, exactly: `{ displayName?, photoUrl?, email?, phone? }`.</summary>
public sealed record UpdateProfileRequest(string? DisplayName, string? PhotoUrl, string? Email, string? Phone);
