using BuzzMe.Domain.Boards;

namespace BuzzMe.Application.Boards.Models;

/// <summary>
/// The Application-layer shape of a Membership — API_CONTRACT.md §3's Membership resource
/// is `userId, displayName, photoUrl, role, muted, joinedAt`; only `userId`/`role` are
/// populated here. `displayName`/`photoUrl` need a User/Profile domain this codebase
/// doesn't have; `muted`/`joinedAt` need fields Membership.cs itself has never carried
/// (Mute isn't in Sprint 5's scope, and no sprint has ever added a Membership timestamp).
/// Omitted rather than filled with placeholders — SPRINT_5_REPORT.md's specification gap.
/// </summary>
public sealed record MembershipResult(Guid BoardId, Guid UserId, string Role)
{
    public static MembershipResult FromDomain(BoardId boardId, Membership membership) => new(
        boardId.Value,
        membership.UserId,
        membership.Role.ToString());
}
