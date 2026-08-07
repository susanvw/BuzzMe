using BuzzMe.Domain.Reminders;

namespace BuzzMe.Domain.Occurrences;

/// <summary>Declared in Domain, implemented in Infrastructure — only what Sprint 3's generation algorithm and read use cases need.</summary>
public interface IOccurrenceRepository
{
    Task AddAsync(Occurrence occurrence, CancellationToken cancellationToken);

    Task<Occurrence?> GetByIdAsync(OccurrenceId id, CancellationToken cancellationToken);

    /// <summary>Ordered by Id (time-sortable), same cursor pattern as IReminderRepository.ListByBoardAsync.</summary>
    Task<IReadOnlyList<Occurrence>> ListByReminderAsync(ReminderId reminderId, Guid? afterId, int limit, CancellationToken cancellationToken);

    /// <summary>How many Occurrences already exist for this Reminder — the generation algorithm's "which index is next" input (SPRINT_3_REPORT.md §3).</summary>
    Task<int> CountByReminderAsync(ReminderId reminderId, CancellationToken cancellationToken);

    /// <summary>The Occurrence with the latest DueAt for this Reminder, if any — used to decide whether generation has already caught up to "now."</summary>
    Task<Occurrence?> GetLatestByReminderAsync(ReminderId reminderId, CancellationToken cancellationToken);
}
