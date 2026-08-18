using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Reminders.Events;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Reminders;

/// <summary>
/// The definition of something a Board's members care about remembering —
/// IMPLEMENTATION_SPEC.md §1. Sprint 3.1 aligns Delete with what was already specified
/// there and confirmed in REMINDER_LIFECYCLE_REVIEW.md/SOFT_DELETE_IMPACT_REVIEW.md: a
/// soft delete (DeletedAt marker), not a physical removal — History and Occurrences
/// survive by construction, since nothing about them depends on this document existing
/// in an "active" state. No new lifecycle state is introduced; DeletedAt is a nullable
/// marker on the same aggregate, not a third status value.
/// </summary>
public sealed class Reminder : AggregateRoot<ReminderId>
{
    public BoardId BoardId { get; private init; }

    public ReminderTitle Title { get; private set; }

    public ReminderSchedule Schedule { get; private set; }

    public NotifyPreset NotifyPreset { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    private Reminder(BoardId boardId, ReminderTitle title, ReminderSchedule schedule, NotifyPreset notifyPreset)
    {
        BoardId = boardId;
        Title = title;
        Schedule = schedule;
        NotifyPreset = notifyPreset;
    }

    public static Reminder Create(
        ReminderId id, BoardId boardId, ReminderTitle title, ReminderSchedule schedule, NotifyPreset notifyPreset, DateTimeOffset createdAt)
    {
        var reminder = new Reminder(boardId, title, schedule, notifyPreset)
        {
            Id = id,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };

        reminder.Raise(new ReminderCreated(Guid.CreateVersion7(), createdAt, id, boardId, title));

        return reminder;
    }

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.7 — the Application layer resolves the caller's partial
    /// request into fully-resolved target values (falling back to the current value for any
    /// field the caller omitted) before calling this; this method only decides which of the
    /// three field groups actually changed and raises exactly the matching event(s) —
    /// title/date share <see cref="Events.ReminderUpdated"/>, recurrence gets its own
    /// <see cref="Events.RecurrenceRuleUpdated"/>, notify preset its own
    /// <see cref="Events.NotifyPresetUpdated"/>, since each drives a different downstream
    /// policy (§7). Re-applying identical values raises nothing and leaves
    /// <see cref="UpdatedAt"/> untouched — API_CONTRACT.md §5's own stated idempotency rule.
    /// </summary>
    public void Update(ReminderTitle title, ReminderSchedule schedule, NotifyPreset notifyPreset, DateTimeOffset updatedAt)
    {
        var titleOrDateChanged = Title != title || Schedule.StartDate != schedule.StartDate;
        var recurrenceChanged = Schedule.Recurrence != schedule.Recurrence;
        var notifyPresetChanged = NotifyPreset != notifyPreset;

        if (!titleOrDateChanged && !recurrenceChanged && !notifyPresetChanged)
            return;

        Title = title;
        Schedule = schedule;
        NotifyPreset = notifyPreset;
        UpdatedAt = updatedAt;

        if (titleOrDateChanged)
            Raise(new ReminderUpdated(Guid.CreateVersion7(), updatedAt, Id, title, schedule.StartDate));
        if (recurrenceChanged)
            Raise(new RecurrenceRuleUpdated(Guid.CreateVersion7(), updatedAt, Id, schedule.Recurrence));
        if (notifyPresetChanged)
            Raise(new NotifyPresetUpdated(Guid.CreateVersion7(), updatedAt, Id, notifyPreset));
    }

    /// <summary>Sets DeletedAt — History and Occurrences are untouched by this call, by construction (they never reference this field).</summary>
    public void Delete(DateTimeOffset deletedAt)
    {
        DeletedAt = deletedAt;
        Raise(new ReminderDeleted(Guid.CreateVersion7(), deletedAt, Id, BoardId));
    }

    internal static Reminder Rehydrate(
        ReminderId id, BoardId boardId, ReminderTitle title, ReminderSchedule schedule, NotifyPreset notifyPreset,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, DateTimeOffset? deletedAt, long version)
    {
        var reminder = new Reminder(boardId, title, schedule, notifyPreset)
        {
            Id = id,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            DeletedAt = deletedAt,
        };
        reminder.Version = version;
        return reminder;
    }
}
