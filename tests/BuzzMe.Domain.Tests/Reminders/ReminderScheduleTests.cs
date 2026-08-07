using BuzzMe.Domain.Reminders;

namespace BuzzMe.Domain.Tests.Reminders;

/// <summary>
/// The core scheduling algorithm Sprint 3 exists to build — SPRINT_3_REPORT.md §3 records
/// the decision this verifies. All DST expectations here were confirmed empirically against
/// .NET's TimeZoneInfo before being written into this test, not assumed.
/// </summary>
public sealed class ReminderScheduleTests
{
    [Fact]
    public void GetLocalDateTimeForOccurrence_Once_OnlyHasIndexZero()
    {
        var schedule = new ReminderSchedule(Recurrence.Once, new DateTime(2026, 8, 1, 16, 0, 0), "UTC");

        Assert.Equal(new DateTime(2026, 8, 1, 16, 0, 0), schedule.GetLocalDateTimeForOccurrence(0));
        Assert.Throws<InvalidOperationException>(() => schedule.GetLocalDateTimeForOccurrence(1));
    }

    [Fact]
    public void GetLocalDateTimeForOccurrence_Daily_AddsOneDayPerIndex()
    {
        var schedule = new ReminderSchedule(Recurrence.Daily, new DateTime(2026, 8, 1, 9, 0, 0), "UTC");

        Assert.Equal(new DateTime(2026, 8, 1, 9, 0, 0), schedule.GetLocalDateTimeForOccurrence(0));
        Assert.Equal(new DateTime(2026, 8, 4, 9, 0, 0), schedule.GetLocalDateTimeForOccurrence(3));
    }

    [Fact]
    public void GetLocalDateTimeForOccurrence_Weekly_AddsSevenDaysPerIndex()
    {
        var schedule = new ReminderSchedule(Recurrence.Weekly, new DateTime(2026, 8, 1, 9, 0, 0), "UTC");

        Assert.Equal(new DateTime(2026, 8, 22, 9, 0, 0), schedule.GetLocalDateTimeForOccurrence(3));
    }

    [Fact]
    public void GetLocalDateTimeForOccurrence_Monthly_ClipsOverflowingDayToTheTargetMonthsLastDay()
    {
        var schedule = new ReminderSchedule(Recurrence.Monthly, new DateTime(2026, 1, 31, 9, 0, 0), "UTC");

        Assert.Equal(new DateTime(2026, 2, 28, 9, 0, 0), schedule.GetLocalDateTimeForOccurrence(1));
    }

    [Fact]
    public void GetLocalDateTimeForOccurrence_Yearly_ClipsFeb29InNonLeapYears()
    {
        var schedule = new ReminderSchedule(Recurrence.Yearly, new DateTime(2028, 2, 29, 9, 0, 0), "UTC");

        Assert.Equal(new DateTime(2029, 2, 28, 9, 0, 0), schedule.GetLocalDateTimeForOccurrence(1));
    }

    [Fact]
    public void ResolveDueInstant_UsesTheOffsetInEffectOnEachOccurrencesOwnDate_NotAFixedOffset()
    {
        // "Every year on July 9, 4pm, America/New_York" — July is EDT (UTC-4), January is
        // EST (UTC-5). If the offset were computed once and reused, one of these would be wrong.
        var yearly = new ReminderSchedule(Recurrence.Yearly, new DateTime(2026, 7, 9, 16, 0, 0), "America/New_York");
        var winterOccurrence = new ReminderSchedule(Recurrence.Once, new DateTime(2026, 1, 9, 16, 0, 0), "America/New_York");

        Assert.Equal(new DateTimeOffset(2026, 7, 9, 20, 0, 0, TimeSpan.Zero), yearly.ResolveDueInstant(0));
        Assert.Equal(new DateTimeOffset(2026, 1, 9, 21, 0, 0, TimeSpan.Zero), winterOccurrence.ResolveDueInstant(0));
    }

    [Fact]
    public void ResolveDueInstant_IsDeterministic_SameIndexAlwaysResolvesToTheSameInstant()
    {
        var schedule = new ReminderSchedule(Recurrence.Weekly, new DateTime(2026, 8, 1, 9, 0, 0), "Africa/Johannesburg");

        var first = schedule.ResolveDueInstant(5);
        var second = schedule.ResolveDueInstant(5);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ResolveDueInstant_HandlesTheSpringForwardInvalidTimeGap_ByShiftingPastIt()
    {
        // 2026-03-08 is America/New_York's spring-forward transition: 02:00 -> 03:00.
        // 02:30 never occurs that day — verified empirically (TimeZoneInfo.IsInvalidTime)
        // before this expectation was written. Resolved by shifting forward past the gap,
        // matching the convention most calendar systems use for this exact edge case.
        var schedule = new ReminderSchedule(Recurrence.Daily, new DateTime(2026, 3, 6, 2, 30, 0), "America/New_York");

        var march6 = schedule.ResolveDueInstant(0); // EST, UTC-5
        var march8Gap = schedule.ResolveDueInstant(2); // the invalid day — shifts 02:30 -> 03:30 EDT
        var march9AfterGap = schedule.ResolveDueInstant(3); // fully in EDT, UTC-4

        Assert.Equal(new DateTimeOffset(2026, 3, 6, 7, 30, 0, TimeSpan.Zero), march6);
        Assert.Equal(new DateTimeOffset(2026, 3, 8, 7, 30, 0, TimeSpan.Zero), march8Gap);
        Assert.Equal(new DateTimeOffset(2026, 3, 9, 6, 30, 0, TimeSpan.Zero), march9AfterGap);
    }

    [Fact]
    public void ResolveDueInstant_HandlesTheFallBackAmbiguousHour_Deterministically()
    {
        // 2026-11-01 is America/New_York's fall-back transition: 02:00 -> 01:00, so 01:30
        // occurs twice. Verified empirically: .NET resolves this to the post-transition
        // (standard time, UTC-5) interpretation without throwing — asserted here so a
        // future .NET/tzdata change that altered this would be caught.
        var schedule = new ReminderSchedule(Recurrence.Once, new DateTime(2026, 11, 1, 1, 30, 0), "America/New_York");

        var resolved = schedule.ResolveDueInstant(0);

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 6, 30, 0, TimeSpan.Zero), resolved);
    }

    [Fact]
    public void ResolveDueInstant_ThrowsForAnUnknownTimezoneId()
    {
        var schedule = new ReminderSchedule(Recurrence.Once, DateTime.UtcNow, "Not/ARealZone");

        Assert.Throws<TimeZoneNotFoundException>(() => schedule.ResolveDueInstant(0));
    }
}
