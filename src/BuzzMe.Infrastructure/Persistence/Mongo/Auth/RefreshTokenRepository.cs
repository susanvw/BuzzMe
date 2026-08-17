using BuzzMe.Domain.Auth;
using BuzzMe.Infrastructure.Persistence.Mongo.Auth.Mappers;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Auth;

/// <summary>A hand-written repository for exactly the RefreshToken aggregate — DEVELOPMENT_GUIDE.md §3's "no generic repository."</summary>
public sealed class RefreshTokenRepository(MongoContext context) : IRefreshTokenRepository
{
    private IMongoCollection<RefreshTokenDocument> Collection => context.Database.GetCollection<RefreshTokenDocument>("refreshtokens");

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(RefreshTokenMapper.ToDocument(refreshToken), cancellationToken: cancellationToken);
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var document = await Collection
            .Find(Builders<RefreshTokenDocument>.Filter.Eq(d => d.TokenHash, tokenHash))
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : RefreshTokenMapper.ToDomain(document);
    }

    public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        await Collection.ReplaceOneAsync(
            Builders<RefreshTokenDocument>.Filter.Eq(d => d.Id, refreshToken.Id.Value),
            RefreshTokenMapper.ToDocument(refreshToken),
            cancellationToken: cancellationToken);
    }
}
