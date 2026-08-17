using BuzzMe.Domain.Boards;

namespace BuzzMe.Application.Tests.TestDoubles;

/// <summary>
/// An in-memory IBoardRepository — appropriate for Application-layer orchestration tests
/// specifically (DEVELOPMENT_GUIDE.md §8: "Application Service orchestration logic against
/// fakes/mocks"). The real repository is exercised only by Infrastructure integration
/// tests against real MongoDB, per Sprint 1's explicit "do not mock MongoDB repositories."
/// </summary>
public sealed class InMemoryBoardRepository : IBoardRepository
{
    private readonly List<Board> _boards = [];

    public Task AddAsync(Board board, CancellationToken cancellationToken)
    {
        _boards.Add(board);
        return Task.CompletedTask;
    }

    public Task<Board?> GetByIdAsync(BoardId id, CancellationToken cancellationToken) =>
        Task.FromResult(_boards.FirstOrDefault(board => board.Id == id));

    public Task<IReadOnlyList<Board>> ListByMemberAsync(
        Guid userId, Guid? afterId, int limit, CancellationToken cancellationToken)
    {
        var query = _boards.Where(board => board.HasMember(userId));

        if (afterId is { } cursor)
            query = query.Where(board => board.Id.Value.CompareTo(cursor) > 0);

        IReadOnlyList<Board> page = query.OrderBy(board => board.Id.Value).Take(limit).ToList();
        return Task.FromResult(page);
    }

    public Task AddMemberAsync(BoardId boardId, Guid userId, DateTimeOffset joinedAt, CancellationToken cancellationToken)
    {
        // No-op: the fake holds Board by reference, and the caller always invokes
        // board.GrantMembership(...) — which already mutated _memberships — before calling
        // this. Same reasoning as InMemoryReminderRepository.MarkDeletedAsync.
        return Task.CompletedTask;
    }

    public Task SetMembershipMutedAsync(BoardId boardId, Guid userId, bool muted, CancellationToken cancellationToken)
    {
        // No-op — same by-reference reasoning as AddMemberAsync: board.MuteBoard/UnmuteBoard
        // already mutated the shared Membership instance before this is called.
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Board board, CancellationToken cancellationToken)
    {
        // No-op — same by-reference reasoning: board.Leave/RemoveMember already mutated
        // the shared Membership instances before this is called.
        return Task.CompletedTask;
    }
}
