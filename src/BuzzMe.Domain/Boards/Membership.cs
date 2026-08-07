namespace BuzzMe.Domain.Boards;

/// <summary>
/// A User's relationship to a Board — an entity within the Board aggregate, per
/// DOMAIN_MODEL.md §2/§6 (Membership lives inside Board so the "always exactly one Owner"
/// invariant is enforceable transactionally). Identified naturally by its UserId within a
/// Board — no separate synthetic id, since at most one Membership per (Board, User) can
/// ever exist.
///
/// No lifecycle status field yet — every Membership on a Board is implicitly active by
/// construction (Remove Member / Leave Board, a future sprint, is what will introduce
/// that state — see SPRINT_1_REPORT.md). <see cref="Muted"/> (Sprint 7) is a delivery
/// preference, not a lifecycle state: APPLICATION_LAYER_SPEC.md §3.4 places one person's
/// own Board-mute flag directly on their own Membership, a single-aggregate Board
/// transaction — not inside a separate Notification Preferences aggregate, resolving an
/// inconsistency with BUSINESS_BEHAVIOR_MODEL.md's older, less precise framing (see
/// SPRINT_7_REPORT.md).
/// </summary>
public sealed class Membership
{
    public Guid UserId { get; }

    public MembershipRole Role { get; }

    public bool Muted { get; private set; }

    internal Membership(Guid userId, MembershipRole role, bool muted = false)
    {
        UserId = userId;
        Role = role;
        Muted = muted;
    }

    internal void SetMuted(bool muted) => Muted = muted;
}
