namespace BuzzMe.Domain.Boards;

/// <summary>Declared in Domain, implemented in Infrastructure (DEVELOPMENT_GUIDE.md §2). Only the operations the implemented use cases actually need — no generic repository.</summary>
public interface IBoardRepository
{
    Task AddAsync(Board board, CancellationToken cancellationToken);

    Task<Board?> GetByIdAsync(BoardId id, CancellationToken cancellationToken);

    /// <summary>
    /// Boards where the given User holds a Membership, ordered by Id (time-sortable —
    /// DEVELOPMENT_GUIDE.md §9) so <paramref name="afterId"/> is a correct, simple cursor.
    /// </summary>
    Task<IReadOnlyList<Board>> ListByMemberAsync(Guid userId, Guid? afterId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// A targeted update (pushes one new Membership sub-document), not a full aggregate
    /// replace — Sprint 5's "Membership activation" is the first caller BoardRepository's
    /// own original comment anticipated ("no Update method with no caller yet"). No
    /// Version/optimistic-concurrency check is needed here: concurrent grants for two
    /// different Users are two independent, naturally-atomic MongoDB array pushes on the
    /// same document. <paramref name="joinedAt"/> (Sprint 10) seeds the new Membership's
    /// JoinedAt, needed for ReassignOwnership's "longest-standing Member" selection.
    /// </summary>
    Task AddMemberAsync(BoardId boardId, Guid userId, DateTimeOffset joinedAt, CancellationToken cancellationToken);

    /// <summary>
    /// Sprint 7 — a targeted update of one Membership sub-document's `Muted` flag within
    /// the array (MongoDB's positional `$` operator, matched by UserId), not a full
    /// aggregate replace. Same no-Version-check reasoning as AddMemberAsync: two different
    /// Users muting/unmuting concurrently are two independent, naturally-atomic updates.
    /// </summary>
    Task SetMembershipMutedAsync(BoardId boardId, Guid userId, bool muted, CancellationToken cancellationToken);

    /// <summary>
    /// Sprint 10 — a full aggregate replace, unlike the targeted updates above: Leave (with
    /// its inline ReassignOwnership) can change two Membership rows' Role/Status in the same
    /// transaction, which a single positional `$` update cannot express. Mirrors
    /// UserRepository.UpdateAsync's own plain-replace shape; no Version check, same category
    /// as that method (no specification names a concurrent-edit conflict scenario for these
    /// use cases).
    /// </summary>
    Task UpdateAsync(Board board, CancellationToken cancellationToken);
}
