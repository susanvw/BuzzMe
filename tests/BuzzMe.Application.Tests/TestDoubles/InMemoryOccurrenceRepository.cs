using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Reminders;

namespace BuzzMe.Application.Tests.TestDoubles;

/// <summary>In-memory IOccurrenceRepository — appropriate for Application-layer orchestration tests, same pattern as InMemoryBoardRepository/InMemoryReminderRepository.</summary>
public sealed class InMemoryOccurrenceRepository : IOccurrenceRepository
{
    private readonly List<Occurrence> _occurrences = [];

    public Task AddAsync(Occurrence occurrence, CancellationToken cancellationToken)
    {
        _occurrences.Add(occurrence);
        return Task.CompletedTask;
    }

    public Task<Occurrence?> GetByIdAsync(OccurrenceId id, CancellationToken cancellationToken) =>
        Task.FromResult(_occurrences.FirstOrDefault(occurrence => occurrence.Id == id));

    public Task<IReadOnlyList<Occurrence>> ListByReminderAsync(ReminderId reminderId, Guid? afterId, int limit, CancellationToken cancellationToken)
    {
        var query = _occurrences.Where(occurrence => occurrence.ReminderId == reminderId);

        if (afterId is { } cursor)
            query = query.Where(occurrence => occurrence.Id.Value.CompareTo(cursor) > 0);

        IReadOnlyList<Occurrence> page = query.OrderBy(occurrence => occurrence.Id.Value).Take(limit).ToList();
        return Task.FromResult(page);
    }

    public Task<int> CountByReminderAsync(ReminderId reminderId, CancellationToken cancellationToken) =>
        Task.FromResult(_occurrences.Count(occurrence => occurrence.ReminderId == reminderId));

    public Task<Occurrence?> GetLatestByReminderAsync(ReminderId reminderId, CancellationToken cancellationToken) =>
        Task.FromResult(_occurrences
            .Where(occurrence => occurrence.ReminderId == reminderId)
            .OrderByDescending(occurrence => occurrence.DueAt)
            .FirstOrDefault());

    public Task UpdateAsync(Occurrence occurrence, CancellationToken cancellationToken) =>
        // No-op — same by-reference reasoning as InMemoryBuzzRepository/InMemoryBoardRepository:
        // Complete/Dismiss/Undo already mutated the shared Occurrence instance in place before
        // this is called. Real version-checked conflict behavior is Infrastructure integration
        // test territory (OccurrenceRepositoryTests), matching BuzzRepositoryTests' own precedent.
        Task.CompletedTask;
}
