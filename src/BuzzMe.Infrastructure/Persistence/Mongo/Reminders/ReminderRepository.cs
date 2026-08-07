using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Reminders;
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
}
