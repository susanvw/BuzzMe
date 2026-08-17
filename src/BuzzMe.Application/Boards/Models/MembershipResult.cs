using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Users;

namespace BuzzMe.Application.Boards.Models;

/// <summary>
/// The Application-layer shape of a Membership — API_CONTRACT.md §3's Membership resource:
/// `userId, displayName, photoUrl, role, muted, joinedAt`. `Muted`/`JoinedAt` come straight
/// off Membership itself (both added in Sprint 10, following the mute flag from Sprint 7 and
/// the ownership-reassignment "longest-standing Member" need); `DisplayName`/`PhotoUrl` come
/// from a User lookup, possible for the first time now that Sprint 8/9 built a real User
/// domain — SPRINT_5_REPORT.md's original specification gap for these two fields is closed.
/// <paramref name="user"/> is optional and nullable-out: a Membership existing with no
/// matching User record would be a data-integrity anomaly this codebase has no way to
/// produce today, but nothing enforces referential integrity across the two aggregates, so
/// callers degrade gracefully (null name/photo) rather than fail the whole read.
/// </summary>
public sealed record MembershipResult(
    Guid BoardId, Guid UserId, string? DisplayName, string? PhotoUrl, string Role, bool Muted, DateTimeOffset JoinedAt)
{
    public static MembershipResult FromDomain(BoardId boardId, Membership membership, User? user = null) => new(
        boardId.Value,
        membership.UserId,
        user?.DisplayName.Value,
        user?.PhotoUrl,
        membership.Role.ToString(),
        membership.Muted,
        membership.JoinedAt);
}
