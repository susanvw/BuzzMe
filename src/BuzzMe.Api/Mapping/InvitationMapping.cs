using BuzzMe.Application.Invitations.Models;
using BuzzMe.Contracts.V1.Invitations;

namespace BuzzMe.Api.Mapping;

/// <summary>Application → Contracts mapping for Invitations — extension methods, not a generic mapper (DEVELOPMENT_GUIDE.md §3).</summary>
public static class InvitationMapping
{
    public static InvitationResponse ToResponse(this InvitationResult result) =>
        new(result.Token, result.BoardId, result.BoardName, result.Status, result.ExpiresAt);

    public static ValidateInvitationResponse ToValidateResponse(this InvitationResult result) =>
        new(result.BoardName, result.Status, result.ExpiresAt);
}
