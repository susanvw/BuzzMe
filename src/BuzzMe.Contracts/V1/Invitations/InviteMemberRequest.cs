namespace BuzzMe.Contracts.V1.Invitations;

/// <summary>API_CONTRACT.md §5 — POST /v1/boards/{boardId}/invitations request body: `{ channel }` (`link`|`email`|`sms`; target contact required for `email`/`sms`).</summary>
public sealed record InviteMemberRequest(string Channel, string? TargetContact);
