namespace BuzzMe.Contracts.V1.Boards;

/// <summary>
/// API_CONTRACT.md §3's Membership resource, exactly: `userId, displayName, photoUrl, role,
/// muted, joinedAt` (plus `boardId`, carried on every Membership shape in this codebase
/// since Sprint 5). SPRINT_5_REPORT.md's original gap — `displayName`/`photoUrl`/`muted`/
/// `joinedAt` all missing — is closed as of Sprint 11 (List Members); `displayName`/
/// `photoUrl` are nullable since nothing enforces referential integrity between a
/// Membership's UserId and an existing User record (see MembershipResult's own doc comment).
/// </summary>
public sealed record MembershipResponse(
    Guid BoardId, Guid UserId, string? DisplayName, string? PhotoUrl, string Role, bool Muted, DateTimeOffset JoinedAt);
