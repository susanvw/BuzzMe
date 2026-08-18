using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Infrastructure.Persistence.Mongo.Occurrences.Mappers;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Occurrences;

/// <summary>A hand-written repository for exactly the Occurrence aggregate — DEVELOPMENT_GUIDE.md §3's "no generic repository."</summary>
public sealed class OccurrenceRepository(MongoContext context) : IOccurrenceRepository
{
    private IMongoCollection<OccurrenceDocument> Collection => context.Database.GetCollection<OccurrenceDocument>("occurrences");

    public async Task AddAsync(Occurrence occurrence, CancellationToken cancellationToken)
    {
        // The unique (reminderId, dueAt) index (CreateOccurrenceIndexes) is the ultimate
        // safety net for "cannot generate duplicate occurrences" — the Application layer's
        // own counting/catch-up logic is expected to prevent this in normal operation, so a
        // violation here surfaces as the unexpected fault it would actually be.
        await Collection.InsertOneAsync(OccurrenceMapper.ToDocument(occurrence), cancellationToken: cancellationToken);
    }

    public async Task<Occurrence?> GetByIdAsync(OccurrenceId id, CancellationToken cancellationToken)
    {
        var document = await Collection
            .Find(Builders<OccurrenceDocument>.Filter.Eq(d => d.Id, id.Value))
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : OccurrenceMapper.ToDomain(document);
    }

    public async Task<IReadOnlyList<Occurrence>> ListByReminderAsync(
        ReminderId reminderId, Guid? afterId, int limit, CancellationToken cancellationToken)
    {
        var filter = Builders<OccurrenceDocument>.Filter.Eq(d => d.ReminderId, reminderId.Value);
        if (afterId is { } cursor)
            filter &= Builders<OccurrenceDocument>.Filter.Gt(d => d.Id, cursor);

        var documents = await Collection
            .Find(filter)
            .SortBy(d => d.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return documents.Select(OccurrenceMapper.ToDomain).ToList();
    }

    public async Task<int> CountByReminderAsync(ReminderId reminderId, CancellationToken cancellationToken)
    {
        var count = await Collection.CountDocumentsAsync(
            Builders<OccurrenceDocument>.Filter.Eq(d => d.ReminderId, reminderId.Value), cancellationToken: cancellationToken);
        return (int)count;
    }

    public async Task<Occurrence?> GetLatestByReminderAsync(ReminderId reminderId, CancellationToken cancellationToken)
    {
        var document = await Collection
            .Find(Builders<OccurrenceDocument>.Filter.Eq(d => d.ReminderId, reminderId.Value))
            .SortByDescending(d => d.DueAt)
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : OccurrenceMapper.ToDomain(document);
    }

    public async Task UpdateAsync(Occurrence occurrence, CancellationToken cancellationToken)
    {
        var filter = Builders<OccurrenceDocument>.Filter.Eq(d => d.Id, occurrence.Id.Value)
            & Builders<OccurrenceDocument>.Filter.Eq(d => d.Version, occurrence.Version);

        var replacement = new OccurrenceDocument
        {
            Id = occurrence.Id.Value,
            ReminderId = occurrence.ReminderId.Value,
            DueAt = occurrence.DueAt,
            Status = occurrence.Status.ToCode(),
            GeneratedAt = occurrence.GeneratedAt,
            ResolvedByUserId = occurrence.ResolvedByUserId,
            ResolvedAt = occurrence.ResolvedAt,
            Version = occurrence.Version + 1,
        };

        var result = await Collection.ReplaceOneAsync(filter, replacement, cancellationToken: cancellationToken);
        if (result.MatchedCount == 0)
        {
            throw new ConcurrencyConflictException(
                $"Occurrence {occurrence.Id} was modified by someone else since it was loaded (expected version {occurrence.Version}).");
        }
    }
}
