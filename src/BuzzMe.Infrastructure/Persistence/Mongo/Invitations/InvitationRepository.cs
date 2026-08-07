using BuzzMe.Domain.Invitations;
using BuzzMe.Infrastructure.Persistence.Mongo.Invitations.Mappers;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Invitations;

/// <summary>A hand-written repository for exactly the Invitation aggregate — DEVELOPMENT_GUIDE.md §3's "no generic repository."</summary>
public sealed class InvitationRepository(MongoContext context) : IInvitationRepository
{
    private IMongoCollection<InvitationDocument> Collection => context.Database.GetCollection<InvitationDocument>("invitations");

    public async Task AddAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(InvitationMapper.ToDocument(invitation), cancellationToken: cancellationToken);
    }

    public async Task<Invitation?> GetByIdAsync(InvitationId id, CancellationToken cancellationToken)
    {
        var document = await Collection
            .Find(Builders<InvitationDocument>.Filter.Eq(d => d.Id, id.Value))
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : InvitationMapper.ToDomain(document);
    }

    public async Task<Invitation?> GetByTokenAsync(InvitationToken token, CancellationToken cancellationToken)
    {
        var document = await Collection
            .Find(Builders<InvitationDocument>.Filter.Eq(d => d.Token, token.Value))
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : InvitationMapper.ToDomain(document);
    }

    public async Task UpdateAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        await Collection.ReplaceOneAsync(
            Builders<InvitationDocument>.Filter.Eq(d => d.Id, invitation.Id.Value),
            InvitationMapper.ToDocument(invitation),
            cancellationToken: cancellationToken);
    }
}
