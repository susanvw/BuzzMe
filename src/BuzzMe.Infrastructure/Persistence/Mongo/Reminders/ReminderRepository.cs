using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Infrastructure.Persistence.Mongo.Reminders.Mappers;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Reminders;

/// <summary>A hand-written repository for exactly the Reminder aggregate — DEVELOPMENT_GUIDE.md §3's "no generic repository."</summary>
public sealed class ReminderRepository(MongoContext context) : IReminderRepository
{
    private IMongoCollection<ReminderDocument> Collection => context.Database.GetCollection<ReminderDocument>("reminders");

    private static readonly FilterDefinition<ReminderDocument> NotDeletedFilter =
        Builders<ReminderDocument>.Filter.Eq(d => d.DeletedAt, null);

    public async Task AddAsync(Reminder reminder, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(ReminderMapper.ToDocument(reminder), cancellationToken: cancellationToken);
    }

    public async Task<Reminder?> GetByIdAsync(ReminderId id, CancellationToken cancellationToken)
    {
        var filter = Builders<ReminderDocument>.Filter.Eq(d => d.Id, id.Value) & NotDeletedFilter;

        var document = await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : ReminderMapper.ToDomain(document);
    }

    public async Task<Reminder?> GetByIdIncludingDeletedAsync(ReminderId id, CancellationToken cancellationToken)
    {
        var document = await Collection
            .Find(Builders<ReminderDocument>.Filter.Eq(d => d.Id, id.Value))
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : ReminderMapper.ToDomain(document);
    }

    public async Task<IReadOnlyList<Reminder>> ListByBoardAsync(
        BoardId boardId, Guid? afterId, int limit, CancellationToken cancellationToken)
    {
        var filter = Builders<ReminderDocument>.Filter.Eq(d => d.BoardId, boardId.Value) & NotDeletedFilter;
        if (afterId is { } cursor)
            filter &= Builders<ReminderDocument>.Filter.Gt(d => d.Id, cursor);

        var documents = await Collection
            .Find(filter)
            .SortBy(d => d.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return documents.Select(ReminderMapper.ToDomain).ToList();
    }

    public async Task MarkDeletedAsync(ReminderId id, DateTimeOffset deletedAt, CancellationToken cancellationToken)
    {
        var filter = Builders<ReminderDocument>.Filter.Eq(d => d.Id, id.Value);
        var update = Builders<ReminderDocument>.Update.Set(d => d.DeletedAt, deletedAt);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(Reminder reminder, CancellationToken cancellationToken)
    {
        var filter = Builders<ReminderDocument>.Filter.Eq(d => d.Id, reminder.Id.Value)
            & Builders<ReminderDocument>.Filter.Eq(d => d.Version, reminder.Version);

        var replacement = new ReminderDocument
        {
            Id = reminder.Id.Value,
            BoardId = reminder.BoardId.Value,
            Title = reminder.Title.Value,
            Recurrence = reminder.Schedule.Recurrence.ToCode(),
            StartDate = reminder.Schedule.StartDate,
            ReferenceTimezone = reminder.Schedule.ReferenceTimezone,
            NotifyPreset = reminder.NotifyPreset.ToCode(),
            CreatedAt = reminder.CreatedAt,
            UpdatedAt = reminder.UpdatedAt,
            DeletedAt = reminder.DeletedAt,
            Version = reminder.Version + 1,
        };

        var result = await Collection.ReplaceOneAsync(filter, replacement, cancellationToken: cancellationToken);
        if (result.MatchedCount == 0)
        {
            throw new ConcurrencyConflictException(
                $"Reminder {reminder.Id} was modified by someone else since it was loaded (expected version {reminder.Version}).");
        }
    }
}
