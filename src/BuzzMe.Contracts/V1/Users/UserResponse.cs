namespace BuzzMe.Contracts.V1.Users;

/// <summary>
/// API_CONTRACT.md §3 — the User resource field list (self only), exactly.
/// PersonalBoardId is nullable: IMPLEMENTATION_SPEC.md §1 sets it "at account provisioning,"
/// which follows VerifyAccount — a PendingVerification User (Sprint 9) has none yet.
/// </summary>
public sealed record UserResponse(Guid Id, string DisplayName, string? PhotoUrl, string? Email, string? Phone, string Status, Guid? PersonalBoardId);
