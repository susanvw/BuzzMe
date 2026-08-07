using BuzzMe.Infrastructure.Persistence.Mongo;
using BuzzMe.Infrastructure.Persistence.Mongo.Reminders;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Migrations.Steps;

/// <summary>
/// Backs List Board Reminders. Indexed on (boardId, _id) rather than the
/// (boardId, createdAt) DEVELOPMENT_GUIDE.md §6 originally described — this sprint's cursor
/// is Id-based, consistent with IBoardRepository.ListByMemberAsync (Sprint 1), and Id
/// (GUIDv7) is already time-sortable, so the two orderings are equivalent. See
/// SPRINT_2_REPORT.md's architecture observations.
/// </summary>
public sealed class CreateReminderIndexes(MongoContext context) : IMongoMigration
{
    public int Version => 2;

    public string Description => "Create reminders.boardId+_id index";

    public Task ApplyAsync(CancellationToken cancellationToken)
    {
        var collection = context.Database.GetCollection<ReminderDocument>("reminders");

        var boardIndex = new CreateIndexModel<ReminderDocument>(
            Builders<ReminderDocument>.IndexKeys.Ascending(d => d.BoardId).Ascending(d => d.Id),
            new CreateIndexOptions { Name = "ix_reminders_boardId_id" });

        return collection.Indexes.CreateOneAsync(boardIndex, cancellationToken: cancellationToken);
    }
}
