using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Reminders;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Reminders.Mappers;

/// <summary>The one place Reminder (Domain) and ReminderDocument (Mongo) translate into each other — DEVELOPMENT_GUIDE.md §4.</summary>
internal static class ReminderMapper
{
    public static ReminderDocument ToDocument(Reminder reminder) => new()
    {
        Id = reminder.Id.Value,
        BoardId = reminder.BoardId.Value,
        Title = reminder.Title.Value,
        Recurrence = reminder.Schedule.Recurrence.ToCode(),
        StartDate = reminder.Schedule.StartDate,
        ReferenceTimezone = reminder.Schedule.ReferenceTimezone,
        NotifyPreset = reminder.NotifyPreset.ToCode(),
        CreatedAt = reminder.CreatedAt,
        UpdatedAt = reminder.UpdatedAt,
        DeletedAt = reminder.DeletedAt,
        Version = reminder.Version,
    };

    public static Reminder ToDomain(ReminderDocument document)
    {
        if (!RecurrenceCodes.TryParse(document.Recurrence, out var recurrence))
            throw new InvalidOperationException($"Stored Reminder {document.Id} has an unrecognized recurrence code '{document.Recurrence}'.");

        if (!NotifyPresetCodes.TryParse(document.NotifyPreset, out var notifyPreset))
            throw new InvalidOperationException($"Stored Reminder {document.Id} has an unrecognized notify preset code '{document.NotifyPreset}'.");

        var schedule = new ReminderSchedule(recurrence, document.StartDate, document.ReferenceTimezone);

        return Reminder.Rehydrate(
            new ReminderId(document.Id),
            new BoardId(document.BoardId),
            new ReminderTitle(document.Title),
            schedule,
            notifyPreset,
            document.CreatedAt,
            document.UpdatedAt,
            document.DeletedAt,
            document.Version);
    }
}
