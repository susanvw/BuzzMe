using BuzzMe.Domain.Boards;

namespace BuzzMe.Domain.Reminders;

/// <summary>Declared in Domain, implemented in Infrastructure.</summary>
public interface IReminderRepository
{
    Task AddAsync(Reminder reminder, CancellationToken cancellationToken);

    /// <summary>Excludes soft-deleted Reminders — a deleted Reminder is "not found" for every normal read/write path (Create, Update, Generate, Get, List). See GetByIdIncludingDeletedAsync for the one case that needs the distinction.</summary>
    Task<Reminder?> GetByIdAsync(ReminderId id, CancellationToken cancellationToken);

    /// <summary>
    /// Finds a Reminder regardless of DeletedAt — used only where reading historical
    /// information must not fail simply because the Reminder was deleted (Occurrence
    /// retrieval, and DeleteReminder's own idempotency check against an already-deleted
    /// Reminder — API_CONTRACT.md §5's "already-deleted → 204").
    /// </summary>
    Task<Reminder?> GetByIdIncludingDeletedAsync(ReminderId id, CancellationToken cancellationToken);

    /// <summary>Excludes soft-deleted Reminders — ordered by Id (time-sortable), same cursor pattern as IBoardRepository.ListByMemberAsync.</summary>
    Task<IReadOnlyList<Reminder>> ListByBoardAsync(BoardId boardId, Guid? afterId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// A targeted update (sets DeletedAt), not a document removal — IMPLEMENTATION_SPEC.md
    /// §1's Delete was always a soft delete; see REMINDER_LIFECYCLE_REVIEW.md. Takes the
    /// aggregate itself (Sprint 17), not just its id/timestamp, so the implementation can
    /// write <paramref name="reminder"/>'s raised events (ReminderDeleted) to the outbox in
    /// the same MongoDB transaction as the DeletedAt update — DEVELOPMENT_GUIDE.md §7's
    /// transactional-outbox requirement, needed here for the first time since Delete is
    /// this codebase's only ReminderDeleted-raising write path.
    /// </summary>
    Task MarkDeletedAsync(Reminder reminder, CancellationToken cancellationToken);

    /// <summary>
    /// Sprint 16 — a full aggregate replace, version-checked exactly like
    /// BoardRepository/OccurrenceRepository's own UpdateAsync: UpdateReminder can change
    /// Title/Schedule/NotifyPreset together in one call. Throws
    /// <see cref="SeedWork.ConcurrencyConflictException"/> on a stale write — the first
    /// caller this codebase has that needs a full Reminder replace at all (every prior write
    /// path was either an insert or the single-field MarkDeletedAsync above).
    /// </summary>
    Task UpdateAsync(Reminder reminder, CancellationToken cancellationToken);
}
