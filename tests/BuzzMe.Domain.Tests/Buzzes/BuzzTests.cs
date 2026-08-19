using BuzzMe.Domain.Buzzes;
using BuzzMe.Domain.Buzzes.Events;
using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Reminders;

namespace BuzzMe.Domain.Tests.Buzzes;

public sealed class BuzzTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly OccurrenceId SomeOccurrenceId = new(Guid.CreateVersion7());

    [Fact]
    public void Generate_StampsTheGivenFieldsAndStartsScheduledWithZeroAttempts()
    {
        var buzzId = new BuzzId(Guid.CreateVersion7());
        var recipientUserId = Guid.CreateVersion7();
        var scheduledAt = Now.AddHours(1);

        var buzz = Buzz.Generate(buzzId, SomeOccurrenceId, recipientUserId, scheduledAt, Now);

        Assert.Equal(buzzId, buzz.Id);
        Assert.Equal(SomeOccurrenceId, buzz.OccurrenceId);
        Assert.Equal(recipientUserId, buzz.RecipientUserId);
        Assert.Equal(scheduledAt, buzz.ScheduledAt);
        Assert.Equal(BuzzStatus.Scheduled, buzz.Status);
        Assert.Equal(0, buzz.AttemptCount);
        Assert.Equal(Now, buzz.CreatedAt);
    }

    [Fact]
    public void Generate_RaisesBuzzGenerated()
    {
        var buzzId = new BuzzId(Guid.CreateVersion7());
        var recipientUserId = Guid.CreateVersion7();
        var scheduledAt = Now.AddHours(1);

        var buzz = Buzz.Generate(buzzId, SomeOccurrenceId, recipientUserId, scheduledAt, Now);

        var raised = Assert.Single(buzz.DomainEvents.OfType<BuzzGenerated>());
        Assert.Equal(buzzId, raised.BuzzId);
        Assert.Equal(SomeOccurrenceId, raised.OccurrenceId);
        Assert.Equal(recipientUserId, raised.RecipientUserId);
        Assert.Equal(scheduledAt, raised.ScheduledAt);
    }

    private static Buzz NewScheduledBuzz() =>
        Buzz.Generate(new BuzzId(Guid.CreateVersion7()), SomeOccurrenceId, Guid.CreateVersion7(), Now.AddHours(1), Now);

    [Fact]
    public void ClaimForProcessing_TransitionsToGeneratedAndIncrementsAttemptCount()
    {
        var buzz = NewScheduledBuzz();

        buzz.ClaimForProcessing();

        Assert.Equal(BuzzStatus.Generated, buzz.Status);
        Assert.Equal(1, buzz.AttemptCount);
    }

    [Fact]
    public void ClaimForProcessing_ThrowsWhenNotScheduled()
    {
        var buzz = NewScheduledBuzz();
        buzz.ClaimForProcessing();

        Assert.Throws<InvalidOperationException>(() => buzz.ClaimForProcessing());
    }

    [Fact]
    public void MarkDelivered_TransitionsToDelivered()
    {
        var buzz = NewScheduledBuzz();
        buzz.ClaimForProcessing();

        buzz.MarkDelivered(Now);

        Assert.Equal(BuzzStatus.Delivered, buzz.Status);
    }

    [Fact]
    public void MarkDelivered_RaisesBuzzDelivered()
    {
        var buzz = NewScheduledBuzz();
        buzz.ClaimForProcessing();

        buzz.MarkDelivered(Now);

        var raised = Assert.Single(buzz.DomainEvents.OfType<BuzzDelivered>());
        Assert.Equal(buzz.Id, raised.BuzzId);
        Assert.Equal(buzz.RecipientUserId, raised.RecipientUserId);
    }

    [Fact]
    public void MarkDelivered_ThrowsWhenNotGenerated()
    {
        var buzz = NewScheduledBuzz();

        Assert.Throws<InvalidOperationException>(() => buzz.MarkDelivered(Now));
    }

    [Fact]
    public void MarkFailed_TransitionsToFailed()
    {
        var buzz = NewScheduledBuzz();
        buzz.ClaimForProcessing();

        buzz.MarkFailed(Now);

        Assert.Equal(BuzzStatus.Failed, buzz.Status);
    }

    [Fact]
    public void MarkFailed_RaisesBuzzDeliveryFailed()
    {
        var buzz = NewScheduledBuzz();
        buzz.ClaimForProcessing();

        buzz.MarkFailed(Now);

        var raised = Assert.Single(buzz.DomainEvents.OfType<BuzzDeliveryFailed>());
        Assert.Equal(buzz.Id, raised.BuzzId);
        Assert.Equal(buzz.RecipientUserId, raised.RecipientUserId);
    }

    [Fact]
    public void MarkFailed_ThrowsWhenNotGenerated()
    {
        var buzz = NewScheduledBuzz();

        Assert.Throws<InvalidOperationException>(() => buzz.MarkFailed(Now));
    }

    [Fact]
    public void Cancel_FromScheduled_TransitionsToCancelledAndRaisesBuzzCancelled()
    {
        var buzz = NewScheduledBuzz();

        buzz.Cancel(Now);

        Assert.Equal(BuzzStatus.Cancelled, buzz.Status);
        var raised = Assert.Single(buzz.DomainEvents.OfType<BuzzCancelled>());
        Assert.Equal(buzz.Id, raised.BuzzId);
        Assert.Equal(buzz.OccurrenceId, raised.OccurrenceId);
        Assert.Equal(buzz.RecipientUserId, raised.RecipientUserId);
    }

    [Fact]
    public void Cancel_FromGenerated_TransitionsToCancelled()
    {
        var buzz = NewScheduledBuzz();
        buzz.ClaimForProcessing();

        buzz.Cancel(Now);

        Assert.Equal(BuzzStatus.Cancelled, buzz.Status);
    }

    [Fact]
    public void Cancel_WhenAlreadyDelivered_IsANoOp()
    {
        var buzz = NewScheduledBuzz();
        buzz.ClaimForProcessing();
        buzz.MarkDelivered(Now);

        buzz.Cancel(Now);

        Assert.Equal(BuzzStatus.Delivered, buzz.Status);
        Assert.Empty(buzz.DomainEvents.OfType<BuzzCancelled>());
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_IsIdempotent()
    {
        var buzz = NewScheduledBuzz();
        buzz.Cancel(Now);

        buzz.Cancel(Now.AddMinutes(1));

        Assert.Single(buzz.DomainEvents.OfType<BuzzCancelled>());
    }

    [Theory]
    [InlineData("scheduled", BuzzStatus.Scheduled)]
    [InlineData("generated", BuzzStatus.Generated)]
    [InlineData("delivered", BuzzStatus.Delivered)]
    [InlineData("failed", BuzzStatus.Failed)]
    [InlineData("retried", BuzzStatus.Retried)]
    [InlineData("exhausted", BuzzStatus.Exhausted)]
    [InlineData("seen", BuzzStatus.Seen)]
    [InlineData("dismissed", BuzzStatus.Dismissed)]
    [InlineData("cancelled", BuzzStatus.Cancelled)]
    public void BuzzStatusCodes_RoundTripEveryValue(string code, BuzzStatus expected)
    {
        Assert.True(BuzzStatusCodes.TryParse(code, out var parsed));
        Assert.Equal(expected, parsed);
        Assert.Equal(code, expected.ToCode());
    }

    [Fact]
    public void BuzzStatusCodes_RejectsAnUnknownCode()
    {
        Assert.False(BuzzStatusCodes.TryParse("muted", out _));
    }

    [Theory]
    [InlineData(NotifyPreset.AtTime, 0)]
    [InlineData(NotifyPreset.FifteenMinutesBefore, 15)]
    [InlineData(NotifyPreset.OneHourBefore, 60)]
    [InlineData(NotifyPreset.EightHoursBefore, 480)]
    [InlineData(NotifyPreset.OneDayBefore, 1440)]
    [InlineData(NotifyPreset.OneWeekBefore, 10080)]
    public void ToLeadTime_ReturnsTheExpectedOffsetForEveryPreset(NotifyPreset preset, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), preset.ToLeadTime());
    }
}
