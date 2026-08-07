using BuzzMe.Domain.Buzzes.Events;
using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Buzzes;

/// <summary>
/// One planned delivery, to one recipient, about one Occurrence — IMPLEMENTATION_SPEC.md
/// §1's Buzz (Notification) responsibilities. Its own Aggregate Root, same reasoning as
/// Occurrence: a shared Reminder produces up to one Buzz per Member per Occurrence, an
/// unbounded stream at scale, so it cannot be a child entity of Occurrence or Reminder.
/// Sprint 4 built generation of the pending queue; Sprint 6 adds the orchestration
/// transitions (claim → delivered/failed) — still no real delivery provider, no retry/
/// backoff (see SPRINT_6_REPORT.md's specification gap on why `RetryScheduled` is not
/// implemented).
/// </summary>
public sealed class Buzz : AggregateRoot<BuzzId>
{
    public OccurrenceId OccurrenceId { get; private init; }

    public Guid RecipientUserId { get; private init; }

    /// <summary>The instant this Buzz should be delivered — the Occurrence's DueAt adjusted by the owning Reminder's NotifyPreset lead time.</summary>
    public DateTimeOffset ScheduledAt { get; private init; }

    public BuzzStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    private Buzz(OccurrenceId occurrenceId, Guid recipientUserId, DateTimeOffset scheduledAt)
    {
        OccurrenceId = occurrenceId;
        RecipientUserId = recipientUserId;
        ScheduledAt = scheduledAt;
    }

    /// <summary>The only way a new Buzz comes into existence — always starts `Scheduled`, `AttemptCount` zero (Sprint 4 has no path to any other status or attempt).</summary>
    public static Buzz Generate(BuzzId id, OccurrenceId occurrenceId, Guid recipientUserId, DateTimeOffset scheduledAt, DateTimeOffset createdAt)
    {
        var buzz = new Buzz(occurrenceId, recipientUserId, scheduledAt)
        {
            Id = id,
            Status = BuzzStatus.Scheduled,
            AttemptCount = 0,
            CreatedAt = createdAt,
        };

        buzz.Raise(new BuzzGenerated(Guid.CreateVersion7(), createdAt, id, occurrenceId, recipientUserId, scheduledAt));

        return buzz;
    }

    /// <summary>
    /// The worker has picked this Buzz up and is about to attempt delivery —
    /// `Scheduled → Generated` (IMPLEMENTATION_SPEC.md §1's lifecycle; "Generated" reads
    /// as "ready to deliver," which is exactly what a claimed-for-processing Buzz is —
    /// see SPRINT_6_REPORT.md for why no new "Processing" status was added). Not a
    /// business-significant fact on its own (no event raised) — the actual outcome,
    /// <see cref="MarkDelivered"/> or <see cref="MarkFailed"/>, is what matters
    /// downstream. Counts as the start of a delivery attempt.
    /// </summary>
    public void ClaimForProcessing()
    {
        EnsureStatus(BuzzStatus.Scheduled);

        Status = BuzzStatus.Generated;
        AttemptCount++;
    }

    public void MarkDelivered(DateTimeOffset deliveredAt)
    {
        EnsureStatus(BuzzStatus.Generated);

        Status = BuzzStatus.Delivered;
        Raise(new BuzzDelivered(Guid.CreateVersion7(), deliveredAt, Id, OccurrenceId, RecipientUserId));
    }

    /// <summary>Terminal for this sprint — no retry is scheduled (see SPRINT_6_REPORT.md's specification gap).</summary>
    public void MarkFailed(DateTimeOffset failedAt)
    {
        EnsureStatus(BuzzStatus.Generated);

        Status = BuzzStatus.Failed;
        Raise(new BuzzDeliveryFailed(Guid.CreateVersion7(), failedAt, Id, OccurrenceId, RecipientUserId));
    }

    /// <summary>Same defensive-invariant reasoning as Invitation.EnsurePending (Sprint 5) — a Buzz can never be moved out of a status it isn't currently in.</summary>
    private void EnsureStatus(BuzzStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Buzz {Id} is not {expected} (current status: {Status}).");
    }

    internal static Buzz Rehydrate(
        BuzzId id, OccurrenceId occurrenceId, Guid recipientUserId, DateTimeOffset scheduledAt, BuzzStatus status,
        int attemptCount, DateTimeOffset createdAt, long version)
    {
        var buzz = new Buzz(occurrenceId, recipientUserId, scheduledAt)
        {
            Id = id,
            Status = status,
            AttemptCount = attemptCount,
            CreatedAt = createdAt,
        };
        buzz.Version = version;
        return buzz;
    }
}
