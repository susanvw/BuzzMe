namespace BuzzMe.Contracts.V1.Users;

/// <summary>API_CONTRACT.md §3 — the User resource field list (self only), exactly.</summary>
public sealed record UserResponse(Guid Id, string DisplayName, string? PhotoUrl, string? Email, string? Phone, string Status, Guid PersonalBoardId);
