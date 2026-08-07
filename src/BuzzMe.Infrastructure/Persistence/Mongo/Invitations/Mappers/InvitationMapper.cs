using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Invitations;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Invitations.Mappers;

/// <summary>The one place Invitation (Domain) and InvitationDocument (Mongo) translate into each other — DEVELOPMENT_GUIDE.md §4.</summary>
internal static class InvitationMapper
{
    public static InvitationDocument ToDocument(Invitation invitation) => new()
    {
        Id = invitation.Id.Value,
        Token = invitation.Token.Value,
        BoardId = invitation.BoardId.Value,
        InviterUserId = invitation.InviterUserId,
        Channel = invitation.Channel.ToCode(),
        TargetContact = invitation.TargetContact,
        Status = invitation.Status.ToCode(),
        CreatedAt = invitation.CreatedAt,
        ExpiresAt = invitation.ExpiresAt,
        AcceptedByUserId = invitation.AcceptedByUserId,
        ResolvedAt = invitation.ResolvedAt,
        Version = invitation.Version,
    };

    public static Invitation ToDomain(InvitationDocument document)
    {
        if (!InvitationChannelCodes.TryParse(document.Channel, out var channel))
            throw new InvalidOperationException($"Stored Invitation {document.Id} has an unrecognized channel code '{document.Channel}'.");

        if (!InvitationStatusCodes.TryParse(document.Status, out var status))
            throw new InvalidOperationException($"Stored Invitation {document.Id} has an unrecognized status code '{document.Status}'.");

        return Invitation.Rehydrate(
            new InvitationId(document.Id),
            new InvitationToken(document.Token),
            new BoardId(document.BoardId),
            document.InviterUserId,
            channel,
            document.TargetContact,
            status,
            document.CreatedAt,
            document.ExpiresAt,
            document.AcceptedByUserId,
            document.ResolvedAt,
            document.Version);
    }
}
