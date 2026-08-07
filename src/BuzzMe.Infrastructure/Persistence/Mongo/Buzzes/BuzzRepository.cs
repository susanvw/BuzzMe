using BuzzMe.Domain.Buzzes;
using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Infrastructure.Persistence.Mongo.Buzzes.Mappers;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Buzzes;

/// <summary>A hand-written repository for exactly the Buzz aggregate — DEVELOPMENT_GUIDE.md §3's "no generic repository."</summary>
public sealed class BuzzRepository(MongoContext context) : IBuzzRepository
{
    private IMongoCollection<BuzzDocument> Collection => context.Database.GetCollection<BuzzDocument>("buzzes");

    public async Task AddAsync(Buzz buzz, CancellationToken cancellationToken)
    {
        // The unique (occurrenceId, recipientUserId) index (CreateBuzzIndexes) is the
        // ultimate safety net for "no duplicate Buzz generation" — the Application layer's
        // own existing-recipient check is expected to prevent this in normal operation, so
        // a violation here surfaces as the unexpected fault it would actually be (same
        // pattern as OccurrenceRepository.AddAsync).
        await Collection.InsertOneAsync(BuzzMapper.ToDocument(buzz), cancellationToken: cancellationToken);
    }

    public async Task<Buzz?> GetByIdAsync(BuzzId id, CancellationToken cancellationToken)
    {
        var document = await Collection
            .Find(Builders<BuzzDocument>.Filter.Eq(d => d.Id, id.Value))
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : BuzzMapper.ToDomain(document);
    }

    public async Task<IReadOnlyList<Buzz>> ListByOccurrenceAsync(OccurrenceId occurrenceId, CancellationToken cancellationToken)
    {
        var documents = await Collection
            .Find(Builders<BuzzDocument>.Filter.Eq(d => d.OccurrenceId, occurrenceId.Value))
            .ToListAsync(cancellationToken);

        return documents.Select(BuzzMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Buzz>> ListPendingByRecipientAsync(
        Guid recipientUserId, Guid? afterId, int limit, CancellationToken cancellationToken)
    {
        var filter = Builders<BuzzDocument>.Filter.Eq(d => d.RecipientUserId, recipientUserId)
            & Builders<BuzzDocument>.Filter.Eq(d => d.Status, BuzzStatus.Scheduled.ToCode());

        if (afterId is { } cursor)
            filter &= Builders<BuzzDocument>.Filter.Gt(d => d.Id, cursor);

        var documents = await Collection
            .Find(filter)
            .SortBy(d => d.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return documents.Select(BuzzMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Buzz>> ClaimPendingAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        var filter = Builders<BuzzDocument>.Filter.Eq(d => d.Status, BuzzStatus.Scheduled.ToCode())
            & Builders<BuzzDocument>.Filter.Lte(d => d.ScheduledAt, now);

        var update = Builders<BuzzDocument>.Update
            .Set(d => d.Status, BuzzStatus.Generated.ToCode())
            .Inc(d => d.AttemptCount, 1)
            .Inc(d => d.Version, 1);

        var options = new FindOneAndUpdateOptions<BuzzDocument>
        {
            Sort = Builders<BuzzDocument>.Sort.Ascending(d => d.ScheduledAt),
            ReturnDocument = ReturnDocument.After,
        };

        // Each FindOneAndUpdateAsync call is one atomic MongoDB operation — no two
        // concurrent workers (or two calls in the same batch) can ever claim the same
        // document, without needing any additional locking. This is the "claim work
        // safely" / "concurrent claim tests" / "duplicate processing prevention" guarantee
        // the Sprint 6 brief asks for, not a bulk UpdateMany (which would give every
        // matching document to every caller, not one caller each).
        var claimed = new List<Buzz>();
        for (var i = 0; i < batchSize; i++)
        {
            var document = await Collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
            if (document is null)
                break;

            claimed.Add(BuzzMapper.ToDomain(document));
        }

        return claimed;
    }

    public async Task UpdateAsync(Buzz buzz, CancellationToken cancellationToken)
    {
        var filter = Builders<BuzzDocument>.Filter.Eq(d => d.Id, buzz.Id.Value)
            & Builders<BuzzDocument>.Filter.Eq(d => d.Version, buzz.Version);

        var replacement = new BuzzDocument
        {
            Id = buzz.Id.Value,
            OccurrenceId = buzz.OccurrenceId.Value,
            RecipientUserId = buzz.RecipientUserId,
            ScheduledAt = buzz.ScheduledAt,
            Status = buzz.Status.ToCode(),
            AttemptCount = buzz.AttemptCount,
            CreatedAt = buzz.CreatedAt,
            Version = buzz.Version + 1,
        };

        var result = await Collection.ReplaceOneAsync(filter, replacement, cancellationToken: cancellationToken);
        if (result.MatchedCount == 0)
        {
            throw new ConcurrencyConflictException(
                $"Buzz {buzz.Id} was modified by someone else since it was loaded (expected version {buzz.Version}).");
        }
    }
}
