using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Reminders;

namespace BuzzMe.Application.Tests.TestDoubles;

/// <summary>In-memory IReminderRepository — appropriate for Application-layer orchestration tests (DEVELOPMENT_GUIDE.md §8), same pattern as InMemoryBoardRepository.</summary>
public sealed class InMemoryReminderRepository : IReminderRepository
{
    private readonly List<Reminder> _reminders = [];

    public Task AddAsync(Reminder reminder, CancellationToken cancellationToken)
    {
        _reminders.Add(reminder);
        return Task.CompletedTask;
    }

    public Task<Reminder?> GetByIdAsync(ReminderId id, CancellationToken cancellationToken) =>
        Task.FromResult(_reminders.FirstOrDefault(reminder => reminder.Id == id && !reminder.IsDeleted));

    public Task<Reminder?> GetByIdIncludingDeletedAsync(ReminderId id, CancellationToken cancellationToken) =>
        Task.FromResult(_reminders.FirstOrDefault(reminder => reminder.Id == id));

    public Task<IReadOnlyList<Reminder>> ListByBoardAsync(BoardId boardId, Guid? afterId, int limit, CancellationToken cancellationToken)
    {
        var query = _reminders.Where(reminder => reminder.BoardId == boardId && !reminder.IsDeleted);

        if (afterId is { } cursor)
            query = query.Where(reminder => reminder.Id.Value.CompareTo(cursor) > 0);

        IReadOnlyList<Reminder> page = query.OrderBy(reminder => reminder.Id.Value).Take(limit).ToList();
        return Task.FromResult(page);
    }

    public Task MarkDeletedAsync(ReminderId id, DateTimeOffset deletedAt, CancellationToken cancellationToken)
    {
        var reminder = _reminders.FirstOrDefault(reminder => reminder.Id == id);
        if (reminder is not null && !reminder.IsDeleted)
            reminder.Delete(deletedAt);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Reminder reminder, CancellationToken cancellationToken) =>
        // No-op — same by-reference reasoning as InMemoryBoardRepository/InMemoryBuzzRepository:
        // Reminder.Update already mutated the shared instance in place before this is called.
        Task.CompletedTask;
}
