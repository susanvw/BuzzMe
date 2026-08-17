namespace BuzzMe.Domain.Boards;

/// <summary>
/// IMPLEMENTATION_SPEC.md §1 — "Active → (Removed | Left), terminal per record." A
/// Membership row is never deleted and never reactivated — a subsequent Invitation
/// acceptance by the same User creates a brand-new row, preserving the historical fact
/// that they left/were removed once. Every Membership was implicitly Active-by-construction
/// until this sprint (see Membership.cs's own prior doc comment) — Leave/RemoveMember are
/// what actually need this state to exist.
/// </summary>
public enum MembershipStatus
{
    Active,
    Removed,
    Left,
}
