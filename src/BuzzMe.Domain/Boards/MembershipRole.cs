namespace BuzzMe.Domain.Boards;

/// <summary>IMPLEMENTATION_SPEC.md §1/§5 — exactly these two values in V1. No Guest, no Admin, no Moderator.</summary>
public enum MembershipRole
{
    Owner,
    Member,
}
