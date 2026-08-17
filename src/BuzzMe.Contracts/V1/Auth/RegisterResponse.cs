namespace BuzzMe.Contracts.V1.Auth;

/// <summary>API_CONTRACT.md §5 — Register's success shape: `201, { userId }`.</summary>
public sealed record RegisterResponse(Guid UserId);
