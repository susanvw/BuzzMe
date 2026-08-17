namespace BuzzMe.Application.Auth.Models;

/// <summary>API_CONTRACT.md §5 — Register's success shape: `201, { userId }`.</summary>
public sealed record RegisterResult(Guid UserId);
