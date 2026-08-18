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

    [Fact]
    public void Complete_SetsCompletedAndStampsResolution()
    {
        var occurrenceId = new OccurrenceId(Guid.CreateVersion7());
        var occurrence = Occurrence.Generate(occurrenceId, SomeReminderId, Now.AddDays(1), Now);
        var resolvingUserId = Guid.CreateVersion7();

        occurrence.Complete(resolvingUserId, Now);

        Assert.Equal(OccurrenceStatus.Completed, occurrence.Status);
        Assert.Equal(resolvingUserId, occurrence.ResolvedByUserId);
        Assert.Equal(Now, occurrence.ResolvedAt);
        var raised = Assert.Single(occurrence.DomainEvents.OfType<OccurrenceCompleted>());
        Assert.Equal(occurrenceId, raised.OccurrenceId);
        Assert.Equal(SomeReminderId, raised.ReminderId);
        Assert.Equal(resolvingUserId, raised.ResolvedByUserId);
    }

    [Fact]
    public void Complete_IsIdempotentWhenAlreadyCompleted()
    {
        var occurrence = Occurrence.Generate(new OccurrenceId(Guid.CreateVersion7()), SomeReminderId, Now.AddDays(1), Now);
        var firstResolverUserId = Guid.CreateVersion7();
        occurrence.Complete(firstResolverUserId, Now);

        occurrence.Complete(Guid.CreateVersion7(), Now.AddMinutes(1));

        Assert.Equal(firstResolverUserId, occurrence.ResolvedByUserId);
        Assert.Single(occurrence.DomainEvents.OfType<OccurrenceCompleted>());
    }

    [Fact]
    public void Complete_DoesNotOverrideAPriorDismiss()
    {
        var occurrence = Occurrence.Generate(new OccurrenceId(Guid.CreateVersion7()), SomeReminderId, Now.AddDays(1), Now);
        var dismisserUserId = Guid.CreateVersion7();
        occurrence.Dismiss(dismisserUserId, Now);

        occurrence.Complete(Guid.CreateVersion7(), Now.AddMinutes(1));

        Assert.Equal(OccurrenceStatus.Dismissed, occurrence.Status);
        Assert.Equal(dismisserUserId, occurrence.ResolvedByUserId);
        Assert.Empty(occurrence.DomainEvents.OfType<OccurrenceCompleted>());
    }

    [Fact]
    public void Dismiss_SetsDismissedAndStampsResolution()
    {
        var occurrenceId = new OccurrenceId(Guid.CreateVersion7());
        var occurrence = Occurrence.Generate(occurrenceId, SomeReminderId, Now.AddDays(1), Now);
        var resolvingUserId = Guid.CreateVersion7();

        occurrence.Dismiss(resolvingUserId, Now);

        Assert.Equal(OccurrenceStatus.Dismissed, occurrence.Status);
        Assert.Equal(resolvingUserId, occurrence.ResolvedByUserId);
        var raised = Assert.Single(occurrence.DomainEvents.OfType<OccurrenceDismissed>());
        Assert.Equal(occurrenceId, raised.OccurrenceId);
        Assert.Equal(resolvingUserId, raised.ResolvedByUserId);
    }

    [Fact]
    public void Dismiss_IsIdempotentWhenAlreadyDismissed()
    {
        var occurrence = Occurrence.Generate(new OccurrenceId(Guid.CreateVersion7()), SomeReminderId, Now.AddDays(1), Now);
        var firstResolverUserId = Guid.CreateVersion7();
        occurrence.Dismiss(firstResolverUserId, Now);

        occurrence.Dismiss(Guid.CreateVersion7(), Now.AddMinutes(1));

        Assert.Equal(firstResolverUserId, occurrence.ResolvedByUserId);
        Assert.Single(occurrence.DomainEvents.OfType<OccurrenceDismissed>());
    }

    [Fact]
    public void Undo_ClearsResolutionAndRestoresDueWhenDueAtHasPassed()
    {
        var occurrenceId = new OccurrenceId(Guid.CreateVersion7());
        var dueAt = Now.AddHours(-1);
        var occurrence = Occurrence.Generate(occurrenceId, SomeReminderId, dueAt, Now.AddHours(-2));
        occurrence.Complete(Guid.CreateVersion7(), Now);

        occurrence.Undo(Now);

        Assert.Equal(OccurrenceStatus.Due, occurrence.Status);
        Assert.Null(occurrence.ResolvedByUserId);
        Assert.Null(occurrence.ResolvedAt);
        var raised = Assert.Single(occurrence.DomainEvents.OfType<OccurrenceUndone>());
        Assert.Equal(occurrenceId, raised.OccurrenceId);
        Assert.Equal(SomeReminderId, raised.ReminderId);
    }

    [Fact]
    public void Undo_RestoresScheduledWhenDueAtHasNotYetPassed()
    {
        var dueAt = Now.AddDays(1);
        var occurrence = Occurrence.Generate(new OccurrenceId(Guid.CreateVersion7()), SomeReminderId, dueAt, Now);
        occurrence.Complete(Guid.CreateVersion7(), Now);

        occurrence.Undo(Now);

        Assert.Equal(OccurrenceStatus.Scheduled, occurrence.Status);
    }

    [Fact]
    public void Undo_ThrowsWhenNotCurrentlyResolved()
    {
        var occurrence = Occurrence.Generate(new OccurrenceId(Guid.CreateVersion7()), SomeReminderId, Now.AddDays(1), Now);

        Assert.Throws<InvalidOperationException>(() => occurrence.Undo(Now));
    }

    [Fact]
    public void IsResolved_TrueForCompletedAndDismissedOnly()
    {
        var completed = Occurrence.Generate(new OccurrenceId(Guid.CreateVersion7()), SomeReminderId, Now.AddDays(1), Now);
        completed.Complete(Guid.CreateVersion7(), Now);
        var dismissed = Occurrence.Generate(new OccurrenceId(Guid.CreateVersion7()), SomeReminderId, Now.AddDays(1), Now);
        dismissed.Dismiss(Guid.CreateVersion7(), Now);
        var scheduled = Occurrence.Generate(new OccurrenceId(Guid.CreateVersion7()), SomeReminderId, Now.AddDays(1), Now);

        Assert.True(completed.IsResolved);
        Assert.True(dismissed.IsResolved);
        Assert.False(scheduled.IsResolved);
    }
}
