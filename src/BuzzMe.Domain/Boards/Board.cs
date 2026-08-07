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

    /// <summary>Derived, not stored — IMPLEMENTATION_SPEC.md §5 invariant 3 guarantees exactly one Owner Membership always exists.</summary>
    public Guid OwnerUserId => _memberships.Single(membership => membership.Role == MembershipRole.Owner).UserId;

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
        board._memberships.Add(new Membership(creatorUserId, MembershipRole.Owner));

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

    public bool HasMember(Guid userId) => _memberships.Any(membership => membership.UserId == userId);

    /// <summary>
    /// Sprint 5's "Membership activation" — the only way a Board gains a Member beyond its
    /// creator. Always grants the plain `Member` role (Owner is established only at
    /// creation or future ownership transfer, neither in scope here). Idempotent by the
    /// aggregate's own invariant, not just an Application-layer precondition: accepting an
    /// Invitation when already an Active Member must never create a duplicate Membership
    /// (DOMAIN_MODEL.md's Invitation invariant) — mirrors EVENT_STORMING.md §B4's
    /// `GrantMembership` policy step.
    /// </summary>
    public void GrantMembership(Guid userId, DateTimeOffset grantedAt)
    {
        if (HasMember(userId))
            return;

        _memberships.Add(new Membership(userId, MembershipRole.Member));
        Raise(new MembershipGranted(Guid.CreateVersion7(), grantedAt, Id, userId, MembershipRole.Member));
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

    /// <summary>Callers are expected to have already verified Board membership (every Application Service does, before calling into the aggregate) — this is the aggregate's own defensive invariant, not a substitute for that check.</summary>
    private Membership FindMembership(Guid userId) =>
        _memberships.FirstOrDefault(membership => membership.UserId == userId)
            ?? throw new InvalidOperationException($"User {userId} is not a Member of Board {Id}.");
}
