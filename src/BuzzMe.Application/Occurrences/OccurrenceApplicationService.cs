using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Common;
using BuzzMe.Application.Occurrences.Models;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Domain.Users;

namespace BuzzMe.Application.Occurrences;

/// <summary>
/// One Application Service for the Occurrences bounded-context area — Sprint 3's three
/// generation/read use cases, plus Sprint 15's Complete/Dismiss/Reopen. GetOccurrenceAsync
/// and ListOccurrencesAsync still have no API endpoint (API_CONTRACT.md has none for plain
/// Occurrence access) — exercised by tests only; Complete/Dismiss/Reopen do.
/// </summary>
public sealed class OccurrenceApplicationService(
    IOccurrenceRepository occurrenceRepository, IReminderRepository reminderRepository, IBoardRepository boardRepository,
    IUserRepository userRepository, IIdGenerator idGenerator, IClock clock)
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
        var results = occurrences.Select(occurrence => OccurrenceResult.FromDomain(occurrence)).ToList();

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

    /// <summary>IMPLEMENTATION_SPEC.md §5 — the same 24-hour window that governs the (not-yet-built) Missed-transition sweep also bounds Reopen (IMPLEMENTATION_SPEC.md §1's UndoOccurrenceResolution), anchored to DueAt, not to when the Occurrence was resolved.</summary>
    private static readonly TimeSpan GraceWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.8 — Authorization: Board Member. Shared by
    /// Complete/Dismiss/Reopen: existence + membership (404, hiding an invisible
    /// Occurrence identically to one that doesn't exist), then the parent-Reminder-Deleted
    /// check (410 Gone — once Deleted, every Occurrence of that Reminder is permanently
    /// read-only, App Layer §0/§3.8's own resolved gap). <paramref name="reminderId"/> is
    /// the path's own outer segment (API_CONTRACT.md §1's note that Occurrence actions nest
    /// two levels deep for readability, not because the aggregate boundary requires it) — a
    /// mismatch against the Occurrence's actual ReminderId is treated as NotFound, same as
    /// any other wrong/invisible resource reference in this codebase, not silently ignored.
    /// </summary>
    private async Task<Result<Occurrence>> LoadResolvableOccurrenceAsync(
        Guid requestingUserId, Guid reminderId, Guid occurrenceId, CancellationToken cancellationToken)
    {
        var occurrence = await occurrenceRepository.GetByIdAsync(new OccurrenceId(occurrenceId), cancellationToken);
        if (occurrence is null || occurrence.ReminderId.Value != reminderId)
            return Result.Failure<Occurrence>(Error.NotFound("Occurrence not found."));

        var reminder = await reminderRepository.GetByIdIncludingDeletedAsync(occurrence.ReminderId, cancellationToken);
        if (reminder is null)
            return Result.Failure<Occurrence>(Error.NotFound("Occurrence not found."));

        var board = await boardRepository.GetByIdAsync(reminder.BoardId, cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure<Occurrence>(Error.NotFound("Occurrence not found."));

        if (reminder.IsDeleted)
            return Result.Failure<Occurrence>(Error.Gone("This Reminder has been deleted; its Occurrences are now read-only."));

        return Result.Success(occurrence);
    }

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.8's CompleteReminder. <paramref name="expectedVersion"/>
    /// not matching the Occurrence's current Version is treated as a success carrying the
    /// current (already-resolved) state, flagged via
    /// <see cref="OccurrenceResolutionResult.VersionConflict"/> — this is the "already done
    /// by X" outcome the spec explicitly calls "not an error," even though API_CONTRACT.md
    /// §5 puts it on the wire as `409`. A same-version call against an already-resolved
    /// Occurrence (a genuine idempotent replay) skips the repository write entirely, same
    /// "check before writing" pattern as every other idempotent write in this codebase.
    /// </summary>
    public async Task<Result<OccurrenceResolutionResult>> CompleteOccurrenceAsync(
        Guid requestingUserId, Guid reminderId, Guid occurrenceId, long expectedVersion, CancellationToken cancellationToken) =>
        await ResolveOccurrenceAsync(requestingUserId, reminderId, occurrenceId, expectedVersion, isComplete: true, cancellationToken);

    /// <summary>Reverses Complete's own reasoning — a deliberate "not doing this one," identical authorization/idempotency/conflict shape.</summary>
    public async Task<Result<OccurrenceResolutionResult>> DismissOccurrenceAsync(
        Guid requestingUserId, Guid reminderId, Guid occurrenceId, long expectedVersion, CancellationToken cancellationToken) =>
        await ResolveOccurrenceAsync(requestingUserId, reminderId, occurrenceId, expectedVersion, isComplete: false, cancellationToken);

    private async Task<Result<OccurrenceResolutionResult>> ResolveOccurrenceAsync(
        Guid requestingUserId, Guid reminderId, Guid occurrenceId, long expectedVersion, bool isComplete, CancellationToken cancellationToken)
    {
        var loaded = await LoadResolvableOccurrenceAsync(requestingUserId, reminderId, occurrenceId, cancellationToken);
        if (loaded.IsFailure)
            return Result.Failure<OccurrenceResolutionResult>(loaded.Error);

        var occurrence = loaded.Value;

        if (occurrence.Version != expectedVersion)
            return Result.Success(new OccurrenceResolutionResult(OccurrenceResult.FromDomain(occurrence), VersionConflict: true));

        if (occurrence.IsResolved)
            return Result.Success(new OccurrenceResolutionResult(OccurrenceResult.FromDomain(occurrence), VersionConflict: false));

        if (isComplete)
            occurrence.Complete(requestingUserId, clock.UtcNow);
        else
            occurrence.Dismiss(requestingUserId, clock.UtcNow);

        try
        {
            await occurrenceRepository.UpdateAsync(occurrence, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Lost a genuine race between our own version check above and this write —
            // someone else's Complete/Dismiss/Reopen landed in between. Same "not an
            // error" treatment as a pre-write version mismatch, not an unhandled fault.
            var current = await occurrenceRepository.GetByIdAsync(occurrence.Id, cancellationToken) ?? occurrence;
            return Result.Success(new OccurrenceResolutionResult(OccurrenceResult.FromDomain(current), VersionConflict: true));
        }

        // Re-fetched, not the in-memory `occurrence`: UpdateAsync's version-checked replace
        // increments Version only in the stored document, never on the domain instance that
        // was handed to it (repositories don't reach back into AggregateRoot's protected
        // setter) — reusing `occurrence` here would hand the client back a `Version` one
        // behind what's actually stored, breaking their next call's own expectedVersion.
        var persisted = await occurrenceRepository.GetByIdAsync(occurrence.Id, cancellationToken) ?? occurrence;
        var resolvedByUser = await userRepository.GetByIdAsync(new UserId(requestingUserId), cancellationToken);
        return Result.Success(new OccurrenceResolutionResult(OccurrenceResult.FromDomain(persisted, resolvedByUser), VersionConflict: false));
    }

    /// <summary>
    /// IMPLEMENTATION_SPEC.md §1's UndoOccurrenceResolution. Unlike Complete/Dismiss,
    /// APPLICATION_LAYER_SPEC.md §3.8's Idempotency row names only "a second Complete/Dismiss
    /// call" — Reopen against an Occurrence that isn't currently resolved is a genuine
    /// rejection (409 Conflict), not treated as idempotent. The grace window (24 hours past
    /// DueAt) is checked here, before touching the aggregate — same business-rule-vs-invariant
    /// split as every other use case in this codebase; Occurrence.Undo only defends the
    /// underlying "must currently be resolved" invariant.
    /// </summary>
    public async Task<Result<OccurrenceResolutionResult>> ReopenOccurrenceAsync(
        Guid requestingUserId, Guid reminderId, Guid occurrenceId, long expectedVersion, CancellationToken cancellationToken)
    {
        var loaded = await LoadResolvableOccurrenceAsync(requestingUserId, reminderId, occurrenceId, cancellationToken);
        if (loaded.IsFailure)
            return Result.Failure<OccurrenceResolutionResult>(loaded.Error);

        var occurrence = loaded.Value;

        if (occurrence.Version != expectedVersion)
            return Result.Success(new OccurrenceResolutionResult(OccurrenceResult.FromDomain(occurrence), VersionConflict: true));

        if (!occurrence.IsResolved)
            return Result.Failure<OccurrenceResolutionResult>(
                Error.Conflict("This Occurrence is not currently resolved and cannot be reopened."));

        if (clock.UtcNow - occurrence.DueAt > GraceWindow)
            return Result.Failure<OccurrenceResolutionResult>(Error.Forbidden("The grace window to reopen this Occurrence has passed."));

        occurrence.Undo(clock.UtcNow);

        try
        {
            await occurrenceRepository.UpdateAsync(occurrence, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            var current = await occurrenceRepository.GetByIdAsync(occurrence.Id, cancellationToken) ?? occurrence;
            return Result.Success(new OccurrenceResolutionResult(OccurrenceResult.FromDomain(current), VersionConflict: true));
        }

        // Re-fetched for the same reason ResolveOccurrenceAsync does — see its own comment.
        var persisted = await occurrenceRepository.GetByIdAsync(occurrence.Id, cancellationToken) ?? occurrence;
        return Result.Success(new OccurrenceResolutionResult(OccurrenceResult.FromDomain(persisted), VersionConflict: false));
    }
}
