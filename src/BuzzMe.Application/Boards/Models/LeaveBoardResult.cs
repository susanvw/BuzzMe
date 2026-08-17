namespace BuzzMe.Application.Boards.Models;

/// <summary>API_CONTRACT.md §5 — Leave Board's success shape: `200, { reassignedOwnerUserId: string | null }`.</summary>
public sealed record LeaveBoardResult(Guid? ReassignedOwnerUserId);
