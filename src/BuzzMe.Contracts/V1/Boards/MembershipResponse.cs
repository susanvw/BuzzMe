namespace BuzzMe.Contracts.V1.Boards;

/// <summary>
/// API_CONTRACT.md §3's Membership resource — `userId, displayName, photoUrl, role, muted,
/// joinedAt` — reduced to `boardId, userId, role`: `displayName`/`photoUrl` need a
/// User/Profile domain this codebase doesn't have, `muted`/`joinedAt` need fields
/// Membership.cs itself has never carried (SPRINT_5_REPORT.md's specification gap).
/// </summary>
public sealed record MembershipResponse(Guid BoardId, Guid UserId, string Role);
