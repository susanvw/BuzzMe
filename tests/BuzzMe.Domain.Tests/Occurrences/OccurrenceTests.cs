using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Occurrences.Events;
using BuzzMe.Domain.Reminders;

namespace BuzzMe.Domain.Tests.Occurrences;

public sealed class OccurrenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly ReminderId SomeReminderId = new(Guid.CreateVersion7());

    [Fact]
    public void Generate_StampsTheGivenFieldsAndStartsScheduled()
    {
        var occurrenceId = new OccurrenceId(Guid.CreateVersion7());
        var dueAt = Now.AddDays(1);

        var occurrence = Occurrence.Generate(occurrenceId, SomeReminderId, dueAt, Now);

        Assert.Equal(occurrenceId, occurrence.Id);
        Assert.Equal(SomeReminderId, occurrence.ReminderId);
        Assert.Equal(dueAt, occurrence.DueAt);
        Assert.Equal(OccurrenceStatus.Scheduled, occurrence.Status);
        Assert.Equal(Now, occurrence.GeneratedAt);
        Assert.Null(occurrence.ResolvedByUserId);
        Assert.Null(occurrence.ResolvedAt);
    }

    [Fact]
    public void Generate_RaisesOccurrenceGenerated()
    {
        var occurrenceId = new OccurrenceId(Guid.CreateVersion7());
        var dueAt = Now.AddDays(1);

        var occurrence = Occurrence.Generate(occurrenceId, SomeReminderId, dueAt, Now);

        var raised = Assert.Single(occurrence.DomainEvents.OfType<OccurrenceGenerated>());
        Assert.Equal(occurrenceId, raised.OccurrenceId);
        Assert.Equal(SomeReminderId, raised.ReminderId);
        Assert.Equal(dueAt, raised.DueAt);
    }

    [Theory]
    [InlineData("scheduled", OccurrenceStatus.Scheduled)]
    [InlineData("due", OccurrenceStatus.Due)]
    [InlineData("completed", OccurrenceStatus.Completed)]
    [InlineData("dismissed", OccurrenceStatus.Dismissed)]
    [InlineData("missed", OccurrenceStatus.Missed)]
    public void OccurrenceStatusCodes_RoundTripEveryValue(string code, OccurrenceStatus expected)
    {
        Assert.True(OccurrenceStatusCodes.TryParse(code, out var parsed));
        Assert.Equal(expected, parsed);
        Assert.Equal(code, expected.ToCode());
    }

    [Fact]
    public void OccurrenceStatusCodes_RejectsAnUnknownCode()
    {
        Assert.False(OccurrenceStatusCodes.TryParse("snoozed", out _));
    }
}
