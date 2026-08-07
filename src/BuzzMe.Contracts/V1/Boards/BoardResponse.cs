namespace BuzzMe.Contracts.V1.Boards;

/// <summary>API_CONTRACT.md §3 — the Board resource field list, exactly.</summary>
public sealed record BoardResponse(Guid Id, string Name, Guid OwnerUserId, DateTimeOffset CreatedAt);
