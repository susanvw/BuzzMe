namespace BuzzMe.Contracts.V1.Boards;

/// <summary>API_CONTRACT.md §5 — PATCH /v1/boards/{boardId} request body.</summary>
public sealed record RenameBoardRequest(string Name);
