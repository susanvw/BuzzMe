namespace BuzzMe.Contracts.V1.Boards;

/// <summary>API_CONTRACT.md §5 — POST /v1/boards request body.</summary>
public sealed record CreateBoardRequest(string Name);
