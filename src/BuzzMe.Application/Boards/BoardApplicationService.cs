using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Boards.Models;
using BuzzMe.Application.Common;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Application.Boards;

/// <summary>
/// One Application Service for the Boards bounded-context area, one method per use case —
/// DEVELOPMENT_GUIDE.md §3's deliberate choice over a command-handler-per-file mediator.
/// Covers exactly the three use cases this sprint implements
/// (APPLICATION_LAYER_SPEC.md §3.1, §5).
/// </summary>
public sealed class BoardApplicationService(IBoardRepository boardRepository, IIdGenerator idGenerator, IClock clock)
{
    /// <summary>APPLICATION_LAYER_SPEC.md §3.1 — Authorization: Authenticated User.</summary>
    public async Task<Result<BoardResult>> CreateBoardAsync(Guid requestingUserId, string name, CancellationToken cancellationToken)
    {
        var board = Board.Create(new BoardId(idGenerator.NewId()), new BoardName(name), requestingUserId, clock.UtcNow);

        await boardRepository.AddAsync(board, cancellationToken);

        return Result.Success(BoardResult.FromDomain(board));
    }

    /// <summary>
    /// API_CONTRACT.md §5 — Authorization: Board Member. A Board the requester doesn't
    /// belong to is reported identically to one that doesn't exist (§1 Principle 6 —
    /// never confirm existence to someone who can't see it).
    /// </summary>
    public async Task<Result<BoardResult>> GetBoardAsync(Guid requestingUserId, Guid boardId, CancellationToken cancellationToken)
    {
        var board = await boardRepository.GetByIdAsync(new BoardId(boardId), cancellationToken);

        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure<BoardResult>(Error.NotFound("Board not found."));

        return Result.Success(BoardResult.FromDomain(board));
    }

    /// <summary>API_CONTRACT.md §5/§7 — Authorization: Authenticated User, own Boards only; cursor-paginated.</summary>
    public async Task<Result<PagedResult<BoardResult>>> ListBoardsAsync(
        Guid requestingUserId, string? cursor, int limit, CancellationToken cancellationToken)
    {
        Guid? afterId = Guid.TryParse(cursor, out var parsed) ? parsed : null;

        var boards = await boardRepository.ListByMemberAsync(requestingUserId, afterId, limit, cancellationToken);

        var nextCursor = boards.Count == limit ? boards[^1].Id.Value.ToString() : null;
        var results = boards.Select(BoardResult.FromDomain).ToList();

        return Result.Success(new PagedResult<BoardResult>(results, nextCursor));
    }
}
