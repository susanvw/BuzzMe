using BuzzMe.Domain.Reminders;

namespace BuzzMe.Domain.Buzzes;

/// <summary>
/// Resolves a Reminder's NotifyPreset (Sprint 2) into the lead time a Buzz's ScheduledAt
/// is offset from its Occurrence's DueAt — the one piece of arithmetic Buzz generation
/// needs from an already-fully-specified value (NotifyPreset's six values, IMPLEMENTATION_SPEC.md
/// §1). Kept in the Buzzes namespace rather than added to NotifyPreset.cs itself, so
/// Sprint 4 touches no Sprint 2 file.
/// </summary>
public static class NotifyPresetLeadTimeExtensions
{
    public static TimeSpan ToLeadTime(this NotifyPreset preset) => preset switch
    {
        NotifyPreset.AtTime => TimeSpan.Zero,
        NotifyPreset.FifteenMinutesBefore => TimeSpan.FromMinutes(15),
        NotifyPreset.OneHourBefore => TimeSpan.FromHours(1),
        NotifyPreset.EightHoursBefore => TimeSpan.FromHours(8),
        NotifyPreset.OneDayBefore => TimeSpan.FromDays(1),
        NotifyPreset.OneWeekBefore => TimeSpan.FromDays(7),
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };
}
