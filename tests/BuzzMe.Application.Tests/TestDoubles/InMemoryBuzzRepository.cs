using BuzzMe.Domain.Buzzes;
using BuzzMe.Domain.Occurrences;

namespace BuzzMe.Application.Tests.TestDoubles;

/// <summary>In-memory IBuzzRepository — appropriate for Application-layer orchestration tests, same pattern as InMemoryOccurrenceRepository.</summary>
public sealed class InMemoryBuzzRepository : IBuzzRepository
{
    private readonly List<Buzz> _buzzes = [];

    public Task AddAsync(Buzz buzz, CancellationToken cancellationToken)
    {
        if (_buzzes.Any(existing => existing.OccurrenceId == buzz.OccurrenceId && existing.RecipientUserId == buzz.RecipientUserId))
            throw new InvalidOperationException("Duplicate (OccurrenceId, RecipientUserId) — the real repository enforces this with a unique index.");

        _buzzes.Add(buzz);
        return Task.CompletedTask;
    }

    public Task<Buzz?> GetByIdAsync(BuzzId id, CancellationToken cancellationToken) =>
        Task.FromResult(_buzzes.FirstOrDefault(buzz => buzz.Id == id));

    public Task<IReadOnlyList<Buzz>> ListByOccurrenceAsync(OccurrenceId occurrenceId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Buzz> results = _buzzes.Where(buzz => buzz.OccurrenceId == occurrenceId).ToList();
        return Task.FromResult(results);
    }

    public Task<IReadOnlyList<Buzz>> ListPendingByRecipientAsync(Guid recipientUserId, Guid? afterId, int limit, CancellationToken cancellationToken)
    {
        var query = _buzzes.Where(buzz => buzz.RecipientUserId == recipientUserId && buzz.Status == BuzzStatus.Scheduled);

        if (afterId is { } cursor)
            query = query.Where(buzz => buzz.Id.Value.CompareTo(cursor) > 0);

        IReadOnlyList<Buzz> page = query.OrderBy(buzz => buzz.Id.Value).Take(limit).ToList();
        return Task.FromResult(page);
    }

    public Task<IReadOnlyList<Buzz>> ClaimPendingAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        var eligible = _buzzes
            .Where(buzz => buzz.Status == BuzzStatus.Scheduled && buzz.ScheduledAt <= now)
            .OrderBy(buzz => buzz.ScheduledAt)
            .Take(batchSize)
            .ToList();

        foreach (var buzz in eligible)
            buzz.ClaimForProcessing();

        IReadOnlyList<Buzz> result = eligible;
        return Task.FromResult(result);
    }

    public Task UpdateAsync(Buzz buzz, CancellationToken cancellationToken) =>
        // No-op: the fake holds Buzz by reference, and MarkDelivered/MarkFailed already
        // mutated it in place before this is called. Same reasoning as
        // InMemoryReminderRepository.MarkDeletedAsync.
        Task.CompletedTask;
}
