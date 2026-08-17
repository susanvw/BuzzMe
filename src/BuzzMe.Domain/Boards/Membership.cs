namespace BuzzMe.Domain.Boards;

/// <summary>
/// A User's relationship to a Board — an entity within the Board aggregate, per
/// DOMAIN_MODEL.md §2/§6 (Membership lives inside Board so the "always exactly one Owner"
/// invariant is enforceable transactionally). Identified naturally by its UserId within a
/// Board — no separate synthetic id, since at most one **Active** Membership per
/// (Board, User) pair can ever exist (IMPLEMENTATION_SPEC.md §1) — a User who left or was
/// removed and later rejoins gets a brand-new row, not a reactivated old one, so a Board
/// may hold more than one historical row for the same UserId.
/// </summary>
public sealed class Membership
{
    public Guid UserId { get; }

    public MembershipRole Role { get; private set; }

    public MembershipStatus Status { get; private set; }

    /// <summary>Sprint 10 — needed to select the "longest-standing other Active Member" for automatic ownership reassignment (IMPLEMENTATION_SPEC.md §4's ReassignOwnership policy). Never changes after construction.</summary>
    public DateTimeOffset JoinedAt { get; }

    public bool Muted { get; private set; }

    internal Membership(Guid userId, MembershipRole role, DateTimeOffset joinedAt, bool muted = false, MembershipStatus status = MembershipStatus.Active)
    {
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
        Muted = muted;
        Status = status;
    }

    internal void SetMuted(bool muted) => Muted = muted;

    internal void SetRole(MembershipRole role) => Role = role;

    internal void MarkLeft() => Status = MembershipStatus.Left;

    internal void MarkRemoved() => Status = MembershipStatus.Removed;
}
