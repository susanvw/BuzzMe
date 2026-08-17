using BuzzMe.Domain.Users;
using BuzzMe.Infrastructure.Persistence.Mongo.Users.Mappers;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Users;

/// <summary>A hand-written repository for exactly the User aggregate — DEVELOPMENT_GUIDE.md §3's "no generic repository."</summary>
public sealed class UserRepository(MongoContext context) : IUserRepository
{
    private IMongoCollection<UserDocument> Collection => context.Database.GetCollection<UserDocument>("users");

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        // The unique, sparse email/phone indexes (CreateUserIndexes) are the ultimate
        // safety net for the uniqueness invariant — the Application layer's own
        // ExistsWithEmailOrPhoneAsync check is expected to prevent this in normal
        // operation, so a violation here surfaces as the unexpected fault it would
        // actually be (same pattern as every other AddAsync in this codebase).
        await Collection.InsertOneAsync(UserMapper.ToDocument(user), cancellationToken: cancellationToken);
    }

    public async Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken)
    {
        var document = await Collection
            .Find(Builders<UserDocument>.Filter.Eq(d => d.Id, id.Value))
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : UserMapper.ToDomain(document);
    }

    public async Task<bool> ExistsWithEmailOrPhoneAsync(
        string? email, string? phone, UserId? excludingUserId, CancellationToken cancellationToken)
    {
        var clauses = new List<FilterDefinition<UserDocument>>();
        if (!string.IsNullOrWhiteSpace(email))
            clauses.Add(Builders<UserDocument>.Filter.Eq(d => d.Email, email));
        if (!string.IsNullOrWhiteSpace(phone))
            clauses.Add(Builders<UserDocument>.Filter.Eq(d => d.Phone, phone));

        if (clauses.Count == 0)
            return false;

        var filter = Builders<UserDocument>.Filter.Or(clauses);
        if (excludingUserId is { } userId)
            filter &= Builders<UserDocument>.Filter.Ne(d => d.Id, userId.Value);

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<User?> GetByEmailOrPhoneAsync(string? email, string? phone, CancellationToken cancellationToken)
    {
        var clauses = new List<FilterDefinition<UserDocument>>();
        if (!string.IsNullOrWhiteSpace(email))
            clauses.Add(Builders<UserDocument>.Filter.Eq(d => d.Email, email));
        if (!string.IsNullOrWhiteSpace(phone))
            clauses.Add(Builders<UserDocument>.Filter.Eq(d => d.Phone, phone));

        if (clauses.Count == 0)
            return null;

        var document = await Collection
            .Find(Builders<UserDocument>.Filter.Or(clauses))
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : UserMapper.ToDomain(document);
    }

    public async Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var document = await Collection
            .Find(Builders<UserDocument>.Filter.Eq(d => d.PasswordResetTokenHash, tokenHash))
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : UserMapper.ToDomain(document);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        await Collection.ReplaceOneAsync(
            Builders<UserDocument>.Filter.Eq(d => d.Id, user.Id.Value),
            UserMapper.ToDocument(user),
            cancellationToken: cancellationToken);
    }
}
