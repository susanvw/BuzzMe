using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Boards.Models;
using BuzzMe.Application.Common;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Domain.Users;

namespace BuzzMe.Application.Boards;

/// <summary>
/// One Application Service for the Boards bounded-context area, one method per use case —
/// DEVELOPMENT_GUIDE.md §3's deliberate choice over a command-handler-per-file mediator.
/// Depends on IUserRepository (Sprint 10) only for LeaveBoardAsync's "cannot leave your
/// Personal Board" check (APPLICATION_LAYER_SPEC.md §3.2) — every other method here still
/// needs only IBoardRepository.
/// </summary>
public sealed class BoardApplicationService(
    IBoardRepository boardRepository, IUserRepository userRepository, IIdGenerator idGenerator, IClock clock)
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

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.4 — Authorization: Board Member, acting only on their
    /// own Membership, never another person's — there is no target-user parameter by
    /// design. Idempotent: setting an already-current mute state is a no-op (§3.4's own
    /// stated rule) — checked here, before touching the aggregate or the database, same
    /// pattern as every other idempotent write in this codebase.
    /// </summary>
    public async Task<Result> MuteBoardAsync(Guid requestingUserId, Guid boardId, CancellationToken cancellationToken)
    {
        var board = await boardRepository.GetByIdAsync(new BoardId(boardId), cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure(Error.NotFound("Board not found."));

        var membership = board.FindActiveMembership(requestingUserId)!; // HasMember above already confirmed this exists
        if (membership.Muted)
            return Result.Success();

        board.MuteBoard(requestingUserId, clock.UtcNow);
        await boardRepository.SetMembershipMutedAsync(board.Id, requestingUserId, muted: true, cancellationToken);

        return Result.Success();
    }

    /// <summary>Reverses MuteBoardAsync — same authorization and idempotency rules.</summary>
    public async Task<Result> UnmuteBoardAsync(Guid requestingUserId, Guid boardId, CancellationToken cancellationToken)
    {
        var board = await boardRepository.GetByIdAsync(new BoardId(boardId), cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure(Error.NotFound("Board not found."));

        var membership = board.FindActiveMembership(requestingUserId)!; // HasMember above already confirmed this exists
        if (!membership.Muted)
            return Result.Success();

        board.UnmuteBoard(requestingUserId, clock.UtcNow);
        await boardRepository.SetMembershipMutedAsync(board.Id, requestingUserId, muted: false, cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.2 — Authorization: Board Member. Business validation
    /// (Personal Board, sole-Member-entirely) is checked here, before touching the
    /// aggregate — Board.Leave itself only defends the underlying invariant (never zero
    /// Active Memberships), matching this codebase's usual authorization/business-rule vs.
    /// aggregate-invariant split. Idempotent: a second Leave against an already-`Left`
    /// Membership returns success with no reassignment, not an error.
    /// </summary>
    public async Task<Result<LeaveBoardResult>> LeaveBoardAsync(Guid requestingUserId, Guid boardId, CancellationToken cancellationToken)
    {
        var board = await boardRepository.GetByIdAsync(new BoardId(boardId), cancellationToken);
        // Not HasMember (Active-only) — someone who already Left must still be found here,
        // so the idempotency check below is reachable rather than shadowed by a 404.
        if (board is null || !board.Memberships.Any(m => m.UserId == requestingUserId))
            return Result.Failure<LeaveBoardResult>(Error.NotFound("Board not found."));

        if (board.FindActiveMembership(requestingUserId) is null)
            return Result.Success(new LeaveBoardResult(null));

        var user = await userRepository.GetByIdAsync(new UserId(requestingUserId), cancellationToken);
        if (user?.PersonalBoardId?.Value == boardId)
            return Result.Failure<LeaveBoardResult>(Error.Forbidden("Cannot leave your Personal Board."));

        // API_CONTRACT.md §5's Leave Board row groups this with the Personal Board case
        // under `403`, not `409` — both read as "this action isn't available to you here,"
        // not a business-state conflict.
        var activeMemberCount = board.Memberships.Count(m => m.Status == MembershipStatus.Active);
        if (activeMemberCount == 1)
            return Result.Failure<LeaveBoardResult>(
                Error.Forbidden("You are the only Member of this Board — delete it instead of leaving."));

        var reassignedOwnerUserId = board.Leave(requestingUserId, clock.UtcNow);
        await boardRepository.UpdateAsync(board, cancellationToken);

        return Result.Success(new LeaveBoardResult(reassignedOwnerUserId));
    }

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.6 — Authorization: Board Owner. Never reassigns
    /// ownership: the requester (already confirmed to be the sole Owner) can never target
    /// themselves, so the target is always a non-Owner Member. Idempotent: removing an
    /// already-Removed/Left Membership is a no-op; targeting someone who was never a Member
    /// at all is a genuine 404, distinct from that no-op.
    /// </summary>
    public async Task<Result> RemoveMemberAsync(Guid requestingUserId, Guid boardId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var board = await boardRepository.GetByIdAsync(new BoardId(boardId), cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure(Error.NotFound("Board not found."));

        if (board.OwnerUserId != requestingUserId)
            return Result.Failure(Error.Forbidden("Only the Board Owner may remove a Member."));

        if (targetUserId == requestingUserId)
            return Result.Failure(Error.Conflict("Cannot remove yourself — use Leave Board instead."));

        if (!board.Memberships.Any(m => m.UserId == targetUserId))
            return Result.Failure(Error.NotFound("Member not found."));

        if (board.FindActiveMembership(targetUserId) is null)
            return Result.Success();

        board.RemoveMember(targetUserId, clock.UtcNow, requestingUserId);
        await boardRepository.UpdateAsync(board, cancellationToken);

        return Result.Success();
    }
}
