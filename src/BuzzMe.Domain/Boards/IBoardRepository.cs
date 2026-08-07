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
    /// own original comment anticipated ("no Update method with no caller yet"). Membership
    /// itself carries no fields beyond UserId/Role, so no Version/optimistic-concurrency
    /// check is needed here: concurrent grants for two different Users are two independent,
    /// naturally-atomic MongoDB array pushes on the same document.
    /// </summary>
    Task AddMemberAsync(BoardId boardId, Guid userId, CancellationToken cancellationToken);
}
