using BuzzMe.Domain.Boards.Events;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Boards;

/// <summary>
/// A shared space holding Members — IMPLEMENTATION_SPEC.md §1. Membership lives inside
/// this aggregate (not as a separate aggregate root) specifically so "exactly one Owner,
/// always" is enforceable in a single transaction (APPLICATION_LAYER_SPEC.md §7).
/// </summary>
public sealed class Board : AggregateRoot<BoardId>
{
    private readonly List<Membership> _memberships = [];

    public BoardName Name { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    public IReadOnlyCollection<Membership> Memberships => _memberships.AsReadOnly();

    /// <summary>Derived, not stored — IMPLEMENTATION_SPEC.md §5 invariant 3 guarantees exactly one **Active** Owner Membership always exists. Historical (Removed/Left) rows may still carry `Role = Owner` as a record of what they once held, so the Active filter is load-bearing, not defensive noise.</summary>
    public Guid OwnerUserId => _memberships.Single(membership => membership.Role == MembershipRole.Owner && membership.Status == MembershipStatus.Active).UserId;

    private Board(BoardId id, BoardName name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>
    /// The only way a new Board comes into existence. The creator becomes both Owner and
    /// Member atomically — APPLICATION_LAYER_SPEC.md §3.1.
    /// </summary>
    public static Board Create(BoardId id, BoardName name, Guid creatorUserId, DateTimeOffset createdAt)
    {
        var board = new Board(id, name) { CreatedAt = createdAt };
        board._memberships.Add(new Membership(creatorUserId, MembershipRole.Owner, createdAt));

        board.Raise(new BoardCreated(Guid.CreateVersion7(), createdAt, id, name));
        board.Raise(new MembershipGranted(Guid.CreateVersion7(), createdAt, id, creatorUserId, MembershipRole.Owner));

        return board;
    }

    /// <summary>
    /// Reconstructs a Board from storage — no domain events raised, since nothing new
    /// happened. Internal: only the Infrastructure Mapper that persisted this Board in the
    /// first place is trusted to rehydrate it (DEVELOPMENT_GUIDE.md §3/§4).
    /// </summary>
    internal static Board Rehydrate(BoardId id, BoardName name, DateTimeOffset createdAt, IEnumerable<Membership> memberships, long version)
    {
        var board = new Board(id, name) { CreatedAt = createdAt };
        board._memberships.AddRange(memberships);
        board.Version = version;
        return board;
    }

    public bool HasMember(Guid userId) => FindActiveMembership(userId) is not null;

    /// <summary>The one Active Membership for this UserId, if any — a Board may hold more than one historical (Removed/Left) row for the same person, so callers must never assume `.First`/`.FirstOrDefault` by UserId alone is safe.</summary>
    public Membership? FindActiveMembership(Guid userId) =>
        _memberships.FirstOrDefault(membership => membership.UserId == userId && membership.Status == MembershipStatus.Active);

    /// <summary>
    /// Sprint 5's "Membership activation" — the only way a Board gains a Member beyond its
    /// creator. Always grants the plain `Member` role (Owner is established only at
    /// creation or an ownership reassignment). Idempotent by the aggregate's own invariant,
    /// not just an Application-layer precondition: accepting an Invitation when already an
    /// Active Member must never create a duplicate Membership (DOMAIN_MODEL.md's Invitation
    /// invariant) — mirrors EVENT_STORMING.md §B4's `GrantMembership` policy step. A User
    /// who previously left/was removed and rejoins gets a brand-new row here, not a
    /// reactivated old one (IMPLEMENTATION_SPEC.md §1's Membership lifecycle invariant).
    /// </summary>
    public void GrantMembership(Guid userId, DateTimeOffset grantedAt)
    {
        if (HasMember(userId))
            return;

        _memberships.Add(new Membership(userId, MembershipRole.Member, grantedAt));
        Raise(new MembershipGranted(Guid.CreateVersion7(), grantedAt, Id, userId, MembershipRole.Member));
    }

    /// <summary>
    /// IMPLEMENTATION_SPEC.md §2's LeaveBoard, including the inline, same-transaction
    /// ReassignOwnership policy (§4) when the departing person was the sole Owner. Returns
    /// the new Owner's UserId when a reassignment happened, otherwise null — the Application
    /// layer needs this to populate `{ reassignedOwnerUserId }` (API_CONTRACT.md §5). The
    /// "cannot leave your Personal Board" and "sole Member entirely, not just sole Owner"
    /// business rules are Application-layer concerns (APPLICATION_LAYER_SPEC.md §3.2 lists
    /// them under "Validation (business)," checked before this is ever called) — this method
    /// only defends the genuine aggregate invariant beneath them: a Board must never be left
    /// with zero Active Memberships.
    /// </summary>
    public Guid? Leave(Guid userId, DateTimeOffset leftAt)
    {
        var membership = FindMembership(userId);
        var otherActiveMembers = _memberships.Where(m => m.Status == MembershipStatus.Active && m.UserId != userId).ToList();

        if (otherActiveMembers.Count == 0)
            throw new InvalidOperationException($"User {userId} is the only Active Member of Board {Id} — Leave would leave it ownerless.");

        Guid? reassignedOwnerUserId = null;
        if (membership.Role == MembershipRole.Owner)
        {
            // IMPLEMENTATION_SPEC.md §4 — the longest-standing other Active Member, automatic, no offer/accept step.
            var successor = otherActiveMembers.OrderBy(m => m.JoinedAt).First();
            membership.SetRole(MembershipRole.Member);
            successor.SetRole(MembershipRole.Owner);
            reassignedOwnerUserId = successor.UserId;

            Raise(new BoardOwnershipReassigned(Guid.CreateVersion7(), leftAt, Id, userId, successor.UserId));
        }

        membership.MarkLeft();
        Raise(new MemberLeft(Guid.CreateVersion7(), leftAt, Id, userId));

        return reassignedOwnerUserId;
    }

    /// <summary>
    /// IMPLEMENTATION_SPEC.md §2's RemoveMember. Never touches ownership: the requester
    /// (checked as Board Owner at the Application layer, APPLICATION_LAYER_SPEC.md §3.6) can
    /// never target themselves, and there is exactly one Active Owner at all times — so a
    /// removal target is always a non-Owner Member.
    /// </summary>
    public void RemoveMember(Guid targetUserId, DateTimeOffset removedAt, Guid removedByUserId)
    {
        var membership = FindMembership(targetUserId);

        membership.MarkRemoved();
        Raise(new MemberRemoved(Guid.CreateVersion7(), removedAt, Id, targetUserId, removedByUserId));
    }

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.4 — a single-aggregate Board transaction updating the
    /// requester's own Membership's `Muted` flag; never anyone else's, never the Board's
    /// own content. Idempotent: setting an already-current mute state is a no-op, not an
    /// error (§3.4's own stated idempotency rule) — only a genuine transition raises
    /// BoardMuted.
    /// </summary>
    public void MuteBoard(Guid userId, DateTimeOffset mutedAt)
    {
        var membership = FindMembership(userId);
        if (membership.Muted)
            return;

        membership.SetMuted(true);
        Raise(new BoardMuted(Guid.CreateVersion7(), mutedAt, Id, userId));
    }

    /// <summary>Reverses MuteBoard — same idempotency rule.</summary>
    public void UnmuteBoard(Guid userId, DateTimeOffset unmutedAt)
    {
        var membership = FindMembership(userId);
        if (!membership.Muted)
            return;

        membership.SetMuted(false);
        Raise(new BoardUnmuted(Guid.CreateVersion7(), unmutedAt, Id, userId));
    }

    /// <summary>Callers are expected to have already verified Board membership (every Application Service does, before calling into the aggregate) — this is the aggregate's own defensive invariant, not a substitute for that check. Active-only, same reasoning as FindActiveMembership/HasMember.</summary>
    private Membership FindMembership(Guid userId) =>
        FindActiveMembership(userId)
            ?? throw new InvalidOperationException($"User {userId} is not an Active Member of Board {Id}.");
}
