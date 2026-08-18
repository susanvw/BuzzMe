using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Common;
using BuzzMe.Application.Reminders.Models;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Application.Reminders;

/// <summary>
/// One Application Service for the Reminders bounded-context area — Sprint 2's four use
/// cases only (Create/Get/List/Delete). Depends on IBoardRepository, not just
/// IReminderRepository: Board-membership is the authorization gate for every Reminder
/// action, and Reminder itself carries no knowledge of who belongs to its Board (that
/// lives entirely on Board — Sprint 1). Two repositories in one Application Service is
/// normal orchestration, not a layering violation.
/// </summary>
public sealed class ReminderApplicationService(
    IReminderRepository reminderRepository, IBoardRepository boardRepository, IIdGenerator idGenerator, IClock clock)
{
    /// <summary>APPLICATION_LAYER_SPEC.md §3.7 — Authorization: Board Member.</summary>
    public async Task<Result<ReminderResult>> CreateReminderAsync(
        Guid requestingUserId,
        Guid boardId,
        string title,
        string recurrenceCode,
        DateTime startDate,
        string notifyPresetCode,
        CancellationToken cancellationToken)
    {
        var board = await boardRepository.GetByIdAsync(new BoardId(boardId), cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure<ReminderResult>(Error.NotFound("Board not found."));

        if (!RecurrenceCodes.TryParse(recurrenceCode, out var recurrence))
            return Result.Failure<ReminderResult>(Error.Validation("Recurrence must be one of the supported values."));

        if (!NotifyPresetCodes.TryParse(notifyPresetCode, out var notifyPreset))
            return Result.Failure<ReminderResult>(Error.Validation("Notify preset must be one of the supported values."));

        // Sprint 2 gap: no mechanism exists yet for capturing "the creating device's
        // timezone" — no field in the Create request contract, no session/profile concept.
        // Defaults to UTC until that's resolved. See SPRINT_2_REPORT.md §4.
        var schedule = new ReminderSchedule(recurrence, startDate, "UTC");

        var reminder = Reminder.Create(
            new ReminderId(idGenerator.NewId()), board.Id, new ReminderTitle(title), schedule, notifyPreset, clock.UtcNow);

        await reminderRepository.AddAsync(reminder, cancellationToken);

        return Result.Success(ReminderResult.FromDomain(reminder));
    }

    /// <summary>
    /// Authorization: Board Member. Reached via API_CONTRACT.md's existing flat
    /// `/reminders/{reminderId}` path (§1 Principle 3) — see SPRINT_2_REPORT.md for why this
    /// sprint keeps that shape rather than the nested path the brief's prose used. Because
    /// the route carries no `boardId`, the Reminder is loaded first to discover which Board
    /// it belongs to, then that Board's Membership is checked.
    /// </summary>
    public async Task<Result<ReminderResult>> GetReminderAsync(Guid requestingUserId, Guid reminderId, CancellationToken cancellationToken)
    {
        var reminder = await reminderRepository.GetByIdAsync(new ReminderId(reminderId), cancellationToken);
        if (reminder is null)
            return Result.Failure<ReminderResult>(Error.NotFound("Reminder not found."));

        var board = await boardRepository.GetByIdAsync(reminder.BoardId, cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure<ReminderResult>(Error.NotFound("Reminder not found."));

        return Result.Success(ReminderResult.FromDomain(reminder));
    }

    /// <summary>Authorization: Board Member; cursor-paginated, same shape as IBoardRepository.ListByMemberAsync.</summary>
    public async Task<Result<PagedResult<ReminderResult>>> ListBoardRemindersAsync(
        Guid requestingUserId, Guid boardId, string? cursor, int limit, CancellationToken cancellationToken)
    {
        var board = await boardRepository.GetByIdAsync(new BoardId(boardId), cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure<PagedResult<ReminderResult>>(Error.NotFound("Board not found."));

        Guid? afterId = Guid.TryParse(cursor, out var parsed) ? parsed : null;

        var reminders = await reminderRepository.ListByBoardAsync(board.Id, afterId, limit, cancellationToken);

        var nextCursor = reminders.Count == limit ? reminders[^1].Id.Value.ToString() : null;
        var results = reminders.Select(ReminderResult.FromDomain).ToList();

        return Result.Success(new PagedResult<ReminderResult>(results, nextCursor));
    }

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.7 — Authorization: Board Member (any Active Member,
    /// matching Create's default). `title`/`recurrenceCode`/`startDate`/`notifyPresetCode`
    /// are each nullable — a null means "leave this field as it is," resolved against the
    /// Reminder's current values here before calling into the aggregate, which only ever
    /// sees fully-resolved target values (same split as every other business-rule-vs-
    /// invariant use case in this codebase). Uses <see cref="IBoardRepository.GetByIdIncludingDeletedAsync"/>
    /// and an explicit <c>IsDeleted</c> check — not the deleted-excluding GetByIdAsync every
    /// other Reminder method here uses — because App Layer §0's gap-closing note is explicit
    /// that a deleted Board's stale Membership rows may still read as Active during the
    /// soft-delete grace window, so "Board not found" (404) and "Board deleted" (409) must
    /// stay distinguishable rather than both collapsing into 404.
    /// </summary>
    public async Task<Result<ReminderResult>> UpdateReminderAsync(
        Guid requestingUserId,
        Guid reminderId,
        string? title,
        string? recurrenceCode,
        DateTime? startDate,
        string? notifyPresetCode,
        CancellationToken cancellationToken)
    {
        var reminder = await reminderRepository.GetByIdAsync(new ReminderId(reminderId), cancellationToken);
        if (reminder is null)
            return Result.Failure<ReminderResult>(Error.NotFound("Reminder not found."));

        var board = await boardRepository.GetByIdIncludingDeletedAsync(reminder.BoardId, cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure<ReminderResult>(Error.NotFound("Reminder not found."));

        if (board.IsDeleted)
            return Result.Failure<ReminderResult>(Error.Conflict("This Reminder's Board has been deleted."));

        var recurrence = reminder.Schedule.Recurrence;
        if (recurrenceCode is not null && !RecurrenceCodes.TryParse(recurrenceCode, out recurrence))
            return Result.Failure<ReminderResult>(Error.Validation("Recurrence must be one of the supported values."));

        var notifyPreset = reminder.NotifyPreset;
        if (notifyPresetCode is not null && !NotifyPresetCodes.TryParse(notifyPresetCode, out notifyPreset))
            return Result.Failure<ReminderResult>(Error.Validation("Notify preset must be one of the supported values."));

        var newTitle = title is not null ? new ReminderTitle(title) : reminder.Title;
        var newSchedule = new ReminderSchedule(recurrence, startDate ?? reminder.Schedule.StartDate, reminder.Schedule.ReferenceTimezone);

        reminder.Update(newTitle, newSchedule, notifyPreset, clock.UtcNow);
        await reminderRepository.UpdateAsync(reminder, cancellationToken);

        return Result.Success(ReminderResult.FromDomain(reminder));
    }

    /// <summary>
    /// Authorization: Board Member (any Member — matches Edit's shared-responsibility
    /// default, Implementation Spec §1). Same flat-path, load-Reminder-first reasoning as
    /// GetReminderAsync. Uses the "including deleted" lookup specifically so that deleting
    /// an already-deleted Reminder is recognized as the natural no-op API_CONTRACT.md §5
    /// specifies ("already-deleted → 204"), not reported as NotFound.
    /// </summary>
    public async Task<Result> DeleteReminderAsync(Guid requestingUserId, Guid reminderId, CancellationToken cancellationToken)
    {
        var reminder = await reminderRepository.GetByIdIncludingDeletedAsync(new ReminderId(reminderId), cancellationToken);
        if (reminder is null)
            return Result.Failure(Error.NotFound("Reminder not found."));

        var board = await boardRepository.GetByIdAsync(reminder.BoardId, cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure(Error.NotFound("Reminder not found."));

        if (reminder.IsDeleted)
            return Result.Success();

        reminder.Delete(clock.UtcNow);
        await reminderRepository.MarkDeletedAsync(reminder.Id, reminder.DeletedAt!.Value, cancellationToken);

        return Result.Success();
    }
}
