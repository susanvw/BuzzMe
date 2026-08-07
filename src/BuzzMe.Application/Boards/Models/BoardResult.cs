using BuzzMe.Domain.Boards;

namespace BuzzMe.Application.Boards.Models;

/// <summary>
/// The Application-layer shape of a Board, distinct from both the Domain aggregate and
/// Contracts' wire-level BoardResponse (DEVELOPMENT_GUIDE.md §3/§4 — two separate mapping
/// boundaries, never one shared type crossing all three layers).
/// </summary>
public sealed record BoardResult(Guid Id, string Name, Guid OwnerUserId, DateTimeOffset CreatedAt)
{
    public static BoardResult FromDomain(Board board) =>
        new(board.Id.Value, board.Name.Value, board.OwnerUserId, board.CreatedAt);
}
