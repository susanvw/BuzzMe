namespace BuzzMe.Contracts.V1.Invitations;

/// <summary>
/// API_CONTRACT.md §3's Invitation resource — `token, boardId, boardName,
/// inviterDisplayName, status, expiresAt` — minus `inviterDisplayName`, which no
/// User/Profile domain in this codebase can currently resolve (SPRINT_5_REPORT.md's
/// specification gap; omitted rather than filled with a placeholder).
/// </summary>
public sealed record InvitationResponse(string Token, Guid BoardId, string BoardName, string Status, DateTimeOffset ExpiresAt);

/// <summary>GET /v1/invitations/{token} (Validate Invitation)'s deliberately minimal success shape — API_CONTRACT.md §5. Same field omission as <see cref="InvitationResponse"/>, minus `token` (redundant — the caller already supplied it).</summary>
public sealed record ValidateInvitationResponse(string BoardName, string Status, DateTimeOffset ExpiresAt);

/// <summary>POST /v1/invitations/{token}/decline's success shape — API_CONTRACT.md §5.</summary>
public sealed record DeclineInvitationResponse(string Status);
