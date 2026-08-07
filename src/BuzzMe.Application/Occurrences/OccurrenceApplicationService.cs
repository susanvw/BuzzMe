using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Common;
using BuzzMe.Application.Occurrences.Models;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Application.Occurrences;

/// <summary>
/// One Application Service for the Occurrences bounded-context area — Sprint 3's three use
/// cases. No API endpoints exist for any of these yet (API_CONTRACT.md has none for plain
/// Occurrence access — only the Complete/Dismiss/Reopen actions, which are out of scope) —
/// these methods exist purely as internal application capability, exercised by tests and
/// (in a future sprint) a scheduler.
/// </summary>
public sealed class OccurrenceApplicationService(
    IOccurrenceRepository occurrenceRepository, IReminderRepository reminderRepository, IBoardRepository boardRepository,
    IIdGenerator idGenerator, IClock clock)
{
    /// <summary>
    /// Ensures Occurrences exist for a Reminder up to and including the next one due at or
    /// after "now" — idempotent and safe to call repeatedly (SPRINT_3_REPORT.md §3's
    /// resolved "rolling horizon" interpretation). Returns only the Occurrences newly
    /// created by this call, if any.
    /// </summary>
    public async Task<Result<IReadOnlyList<OccurrenceResult>>> GenerateOccurrencesAsync(
        Guid requestingUserId, Guid reminderId, CancellationToken cancellationToken)
    {
        var reminder = await reminderRepository.GetByIdAsync(new ReminderId(reminderId), cancellationToken);
        if (reminder is null)
            return Result.Failure<IReadOnlyList<OccurrenceResult>>(Error.NotFound("Reminder not found."));

        var board = await boardRepository.GetByIdAsync(reminder.BoardId, cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure<IReadOnlyList<OccurrenceResult>>(Error.NotFound("Reminder not found."));

        var existingCount = await occurrenceRepository.CountByReminderAsync(reminder.Id, cancellationToken);

        // "Once" has exactly one occurrence, ever, generated regardless of whether its due
        // instant is in the past or future — there is no "catch up to now" concept for a
        // single, non-repeating event.
        if (reminder.Schedule.Recurrence == Recurrence.Once)
        {
            if (existingCount >= 1)
                return Result.Success<IReadOnlyList<OccurrenceResult>>([]);

            var onceOccurrence = await CreateOccurrenceAsync(reminder, occurrenceIndex: 0, cancellationToken);
            return Result.Success<IReadOnlyList<OccurrenceResult>>([OccurrenceResult.FromDomain(onceOccurrence)]);
        }

        // Idempotency check: if the latest existing Occurrence is already due at or after
        // "now," generation has already caught up — nothing more to do this call.
        if (existingCount > 0)
        {
            var latest = await occurrenceRepository.GetLatestByReminderAsync(reminder.Id, cancellationToken);
            if (latest is not null && latest.DueAt >= clock.UtcNow)
                return Result.Success<IReadOnlyList<OccurrenceResult>>([]);
        }

        var generated = new List<OccurrenceResult>();
        var index = existingCount;
        while (true)
        {
            var occurrence = await CreateOccurrenceAsync(reminder, index, cancellationToken);
            generated.Add(OccurrenceResult.FromDomain(occurrence));
            index++;

            if (occurrence.DueAt >= clock.UtcNow)
                break;
        }

        return Result.Success<IReadOnlyList<OccurrenceResult>>(generated);
    }

    private async Task<Occurrence> CreateOccurrenceAsync(Reminder reminder, int occurrenceIndex, CancellationToken cancellationToken)
    {
        var dueAt = reminder.Schedule.ResolveDueInstant(occurrenceIndex);
        var occurrence = Occurrence.Generate(new OccurrenceId(idGenerator.NewId()), reminder.Id, dueAt, clock.UtcNow);
        await occurrenceRepository.AddAsync(occurrence, cancellationToken);
        return occurrence;
    }

    /// <summary>
    /// Authorization: Board Member. Reached only via the owning Reminder — mirrors
    /// ReminderApplicationService.ListBoardRemindersAsync's shape. Uses the "including
    /// deleted" lookup so listing historical Occurrences keeps working after the owning
    /// Reminder is soft-deleted (Sprint 3.1 — see GetOccurrenceAsync below).
    /// </summary>
    public async Task<Result<PagedResult<OccurrenceResult>>> ListOccurrencesAsync(
        Guid requestingUserId, Guid reminderId, string? cursor, int limit, CancellationToken cancellationToken)
    {
        var reminder = await reminderRepository.GetByIdIncludingDeletedAsync(new ReminderId(reminderId), cancellationToken);
        if (reminder is null)
            return Result.Failure<PagedResult<OccurrenceResult>>(Error.NotFound("Reminder not found."));

        var board = await boardRepository.GetByIdAsync(reminder.BoardId, cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure<PagedResult<OccurrenceResult>>(Error.NotFound("Reminder not found."));

        Guid? afterId = Guid.TryParse(cursor, out var parsed) ? parsed : null;

        var occurrences = await occurrenceRepository.ListByReminderAsync(reminder.Id, afterId, limit, cancellationToken);

        var nextCursor = occurrences.Count == limit ? occurrences[^1].Id.Value.ToString() : null;
        var results = occurrences.Select(OccurrenceResult.FromDomain).ToList();

        return Result.Success(new PagedResult<OccurrenceResult>(results, nextCursor));
    }

    /// <summary>
    /// Authorization: Board Member, resolved via the Occurrence's own Reminder (flat-path
    /// precedent, same reasoning as ReminderApplicationService.GetReminderAsync). Uses the
    /// "including deleted" lookup so reading a historical Occurrence does not fail simply
    /// because its owning Reminder was soft-deleted — REMINDER_LIFECYCLE_REVIEW.md /
    /// SOFT_DELETE_IMPACT_REVIEW.md's resolution of the orphaned-Occurrence gap
    /// SPRINT_3_REPORT.md §3 originally surfaced under hard delete.
    /// </summary>
    public async Task<Result<OccurrenceResult>> GetOccurrenceAsync(Guid requestingUserId, Guid occurrenceId, CancellationToken cancellationToken)
    {
        var occurrence = await occurrenceRepository.GetByIdAsync(new OccurrenceId(occurrenceId), cancellationToken);
        if (occurrence is null)
            return Result.Failure<OccurrenceResult>(Error.NotFound("Occurrence not found."));

        var reminder = await reminderRepository.GetByIdIncludingDeletedAsync(occurrence.ReminderId, cancellationToken);
        if (reminder is null)
            return Result.Failure<OccurrenceResult>(Error.NotFound("Occurrence not found."));

        var board = await boardRepository.GetByIdAsync(reminder.BoardId, cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure<OccurrenceResult>(Error.NotFound("Occurrence not found."));

        return Result.Success(OccurrenceResult.FromDomain(occurrence));
    }
}
