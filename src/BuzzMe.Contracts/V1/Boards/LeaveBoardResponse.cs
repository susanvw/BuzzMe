namespace BuzzMe.Contracts.V1.Boards;

/// <summary>API_CONTRACT.md §5 — Leave Board's success shape, exactly: `200, { reassignedOwnerUserId: string | null }`.</summary>
public sealed record LeaveBoardResponse(Guid? ReassignedOwnerUserId);
