using BuzzMe.Domain.Invitations;

namespace BuzzMe.Application.Invitations.Models;

/// <summary>
/// The Application-layer shape of an Invitation — DEVELOPMENT_GUIDE.md §3/§4.
/// `InviterDisplayName` (API_CONTRACT.md §3's Invitation resource field) is never
/// populated: no User/Profile domain exists anywhere in this codebase to resolve a
/// display name from a UserId (SPRINT_5_REPORT.md's specification gap) — omitted rather
/// than filled with a placeholder, per this codebase's "no placeholder code" rule.
/// `BoardName` is included instead: unlike a User's display name, it's fully resolvable
/// today (Board is already loaded by every use case that returns this shape).
/// </summary>
public sealed record InvitationResult(
    Guid Id,
    string Token,
    Guid BoardId,
    string BoardName,
    Guid InviterUserId,
    string Channel,
    string? TargetContact,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    Guid? AcceptedByUserId,
    DateTimeOffset? ResolvedAt)
{
    /// <summary>
    /// <paramref name="now"/> drives the effective <see cref="Status"/>: a Pending
    /// Invitation past its ExpiresAt reads as `expired` here even though nothing has
    /// physically transitioned its stored Status (Invitation.IsExpired's lazy-expiration
    /// design — Sprint 5 brief: "expired invitations may simply be rejected when used").
    /// </summary>
    public static InvitationResult FromDomain(Invitation invitation, string boardName, DateTimeOffset now) => new(
        invitation.Id.Value,
        invitation.Token.Value,
        invitation.BoardId.Value,
        boardName,
        invitation.InviterUserId,
        invitation.Channel.ToCode(),
        invitation.TargetContact,
        invitation.IsExpired(now) ? InvitationStatusCodes.ToCode(InvitationStatus.Expired) : invitation.Status.ToCode(),
        invitation.CreatedAt,
        invitation.ExpiresAt,
        invitation.AcceptedByUserId,
        invitation.ResolvedAt);
}
