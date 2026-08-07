using BuzzMe.Domain.Occurrences;

namespace BuzzMe.Domain.Buzzes;

/// <summary>Declared in Domain, implemented in Infrastructure — only what Sprint 4's generation algorithm and read use cases need.</summary>
public interface IBuzzRepository
{
    Task AddAsync(Buzz buzz, CancellationToken cancellationToken);

    Task<Buzz?> GetByIdAsync(BuzzId id, CancellationToken cancellationToken);

    /// <summary>Every Buzz already generated for this Occurrence — the generation algorithm's "who already has one" idempotency check (one Buzz per Occurrence per recipient).</summary>
    Task<IReadOnlyList<Buzz>> ListByOccurrenceAsync(OccurrenceId occurrenceId, CancellationToken cancellationToken);

    /// <summary>A recipient's own not-yet-delivered Buzzes — the "persistent queue... waiting to be delivered." Ordered by Id (time-sortable), same cursor pattern as the other list use cases.</summary>
    Task<IReadOnlyList<Buzz>> ListPendingByRecipientAsync(Guid recipientUserId, Guid? afterId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically claims up to <paramref name="batchSize"/> Scheduled Buzzes whose
    /// ScheduledAt has arrived, transitioning each to Generated as part of the same
    /// operation that finds it — safe for multiple concurrent worker instances polling at
    /// once, since each underlying claim is one atomic find-and-modify (no caller can ever
    /// observe or re-claim a document another caller already claimed). Oldest-due first.
    /// </summary>
    Task<IReadOnlyList<Buzz>> ClaimPendingAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Persists MarkDelivered/MarkFailed's outcome — a full-document, Version-checked
    /// replace (Sprint 6: see <see cref="SeedWork.ConcurrencyConflictException"/>). Throws
    /// if <paramref name="buzz"/>'s Version no longer matches the stored document, exactly
    /// the "someone else already changed this" fault the check exists to catch.
    /// </summary>
    Task UpdateAsync(Buzz buzz, CancellationToken cancellationToken);
}
