using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.Reminders.Events;

namespace BuzzMe.Domain.Tests.Reminders;

public sealed class ReminderTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly BoardId SomeBoardId = new(Guid.CreateVersion7());

    private static ReminderSchedule YearlySchedule() =>
        new(Recurrence.Yearly, new DateTime(2026, 7, 9, 16, 0, 0), "UTC");

    [Fact]
    public void Create_StampsTheGivenFields()
    {
        var reminderId = new ReminderId(Guid.CreateVersion7());
        var schedule = YearlySchedule();

        var reminder = Reminder.Create(reminderId, SomeBoardId, new ReminderTitle("Emma's birthday"), schedule, NotifyPreset.OneDayBefore, Now);

        Assert.Equal(reminderId, reminder.Id);
        Assert.Equal(SomeBoardId, reminder.BoardId);
        Assert.Equal("Emma's birthday", reminder.Title.Value);
        Assert.Equal(Recurrence.Yearly, reminder.Schedule.Recurrence);
        Assert.Equal(NotifyPreset.OneDayBefore, reminder.NotifyPreset);
        Assert.Equal(Now, reminder.CreatedAt);
        Assert.Equal(Now, reminder.UpdatedAt);
    }

    [Fact]
    public void Create_RaisesReminderCreated()
    {
        var reminderId = new ReminderId(Guid.CreateVersion7());

        var reminder = Reminder.Create(reminderId, SomeBoardId, new ReminderTitle("Vet visit"), YearlySchedule(), NotifyPreset.AtTime, Now);

        var created = Assert.Single(reminder.DomainEvents.OfType<ReminderCreated>());
        Assert.Equal(reminderId, created.ReminderId);
        Assert.Equal(SomeBoardId, created.BoardId);
        Assert.Equal("Vet visit", created.Title.Value);
    }

    [Fact]
    public void Delete_RaisesReminderDeleted()
    {
        var reminder = Reminder.Create(
            new ReminderId(Guid.CreateVersion7()), SomeBoardId, new ReminderTitle("Vet visit"), YearlySchedule(), NotifyPreset.AtTime, Now);
        var deletedAt = Now.AddDays(1);

        reminder.Delete(deletedAt);

        var deleted = Assert.Single(reminder.DomainEvents.OfType<ReminderDeleted>());
        Assert.Equal(reminder.Id, deleted.ReminderId);
        Assert.Equal(SomeBoardId, deleted.BoardId);
        Assert.Equal(deletedAt, deleted.OccurredAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReminderTitle_RejectsAnEmptyValue(string invalidTitle)
    {
        Assert.Throws<ArgumentException>(() => new ReminderTitle(invalidTitle));
    }

    [Fact]
    public void ReminderSchedule_RejectsAnEmptyReferenceTimezone()
    {
        Assert.Throws<ArgumentException>(() => new ReminderSchedule(Recurrence.Once, DateTime.UtcNow, ""));
    }

    [Theory]
    [InlineData("once", Recurrence.Once)]
    [InlineData("daily", Recurrence.Daily)]
    [InlineData("weekly", Recurrence.Weekly)]
    [InlineData("monthly", Recurrence.Monthly)]
    [InlineData("yearly", Recurrence.Yearly)]
    public void RecurrenceCodes_RoundTripEveryValue(string code, Recurrence expected)
    {
        Assert.True(RecurrenceCodes.TryParse(code, out var parsed));
        Assert.Equal(expected, parsed);
        Assert.Equal(code, expected.ToCode());
    }

    [Theory]
    [InlineData("atTime", NotifyPreset.AtTime)]
    [InlineData("15MinBefore", NotifyPreset.FifteenMinutesBefore)]
    [InlineData("1HourBefore", NotifyPreset.OneHourBefore)]
    [InlineData("8HoursBefore", NotifyPreset.EightHoursBefore)]
    [InlineData("1DayBefore", NotifyPreset.OneDayBefore)]
    [InlineData("1WeekBefore", NotifyPreset.OneWeekBefore)]
    public void NotifyPresetCodes_RoundTripEveryValue(string code, NotifyPreset expected)
    {
        Assert.True(NotifyPresetCodes.TryParse(code, out var parsed));
        Assert.Equal(expected, parsed);
        Assert.Equal(code, expected.ToCode());
    }

    [Fact]
    public void RecurrenceCodes_RejectsAnUnknownCode()
    {
        Assert.False(RecurrenceCodes.TryParse("fortnightly", out _));
    }
}
