using BuzzMe.Domain.Occurrences.Events;
using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Occurrences;

/// <summary>
/// A single, concrete, dated instance of a Reminder becoming due — IMPLEMENTATION_SPEC.md
/// §1. Its own Aggregate Root, deliberately separate from Reminder, for the scale reasons
/// that document already gives.
/// </summary>
public sealed class Occurrence : AggregateRoot<OccurrenceId>
{
    public ReminderId ReminderId { get; private init; }

    /// <summary>The absolute UTC instant this Occurrence is due — computed once, at generation, and immutable afterward (IMPLEMENTATION_SPEC.md §1).</summary>
    public DateTimeOffset DueAt { get; private init; }

    public OccurrenceStatus Status { get; private set; }

    public DateTimeOffset GeneratedAt { get; private init; }

    public Guid? ResolvedByUserId { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    private Occurrence(ReminderId reminderId, DateTimeOffset dueAt)
    {
        ReminderId = reminderId;
        DueAt = dueAt;
    }

    /// <summary>The only way a new Occurrence comes into existence — always starts `Scheduled` (Sprint 3 has no path to any other status).</summary>
    public static Occurrence Generate(OccurrenceId id, ReminderId reminderId, DateTimeOffset dueAt, DateTimeOffset generatedAt)
    {
        var occurrence = new Occurrence(reminderId, dueAt)
        {
            Id = id,
            Status = OccurrenceStatus.Scheduled,
            GeneratedAt = generatedAt,
        };

        occurrence.Raise(new OccurrenceGenerated(Guid.CreateVersion7(), generatedAt, id, reminderId, dueAt));

        return occurrence;
    }

    internal static Occurrence Rehydrate(
        OccurrenceId id, ReminderId reminderId, DateTimeOffset dueAt, OccurrenceStatus status, DateTimeOffset generatedAt,
        Guid? resolvedByUserId, DateTimeOffset? resolvedAt, long version)
    {
        var occurrence = new Occurrence(reminderId, dueAt)
        {
            Id = id,
            Status = status,
            GeneratedAt = generatedAt,
            ResolvedByUserId = resolvedByUserId,
            ResolvedAt = resolvedAt,
        };
        occurrence.Version = version;
        return occurrence;
    }

    /// <summary>Not yet resolved either way — the only pre-state Complete/Dismiss/Undo care about distinguishing.</summary>
    public bool IsResolved => Status is OccurrenceStatus.Completed or OccurrenceStatus.Dismissed;

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.8's CompleteReminder, at the domain layer. "Already
    /// resolved by someone else" is treated as a no-op here — not just a duplicate-Complete
    /// call, but also a prior Dismiss: the first valid resolution of either kind wins, and a
    /// later, different resolution attempt does not override it. The Application layer's own
    /// version check (this method has no opinion on Version) is what turns "already resolved"
    /// into the wire-level "already done by X," not this guard — this guard only prevents a
    /// same-version idempotent replay from raising a duplicate event.
    /// </summary>
    public void Complete(Guid userId, DateTimeOffset resolvedAt)
    {
        if (IsResolved)
            return;

        Status = OccurrenceStatus.Completed;
        ResolvedByUserId = userId;
        ResolvedAt = resolvedAt;
        Raise(new OccurrenceCompleted(Guid.CreateVersion7(), resolvedAt, Id, ReminderId, userId));
    }

    /// <summary>Reverses Complete's own reasoning — a deliberate "not doing this one," same idempotency shape.</summary>
    public void Dismiss(Guid userId, DateTimeOffset resolvedAt)
    {
        if (IsResolved)
            return;

        Status = OccurrenceStatus.Dismissed;
        ResolvedByUserId = userId;
        ResolvedAt = resolvedAt;
        Raise(new OccurrenceDismissed(Guid.CreateVersion7(), resolvedAt, Id, ReminderId, userId));
    }

    /// <summary>
    /// IMPLEMENTATION_SPEC.md §1's `UndoOccurrenceResolution`. The grace-window deadline
    /// (IMPLEMENTATION_SPEC.md §5 — 24 hours after DueAt) is checked by the Application
    /// layer before this is ever called, same aggregate/use-case split as every other
    /// business-rule check in this codebase (e.g. Board.Leave's own callers). This method
    /// only defends the genuine invariant beneath it: reopening something that was never
    /// resolved makes no sense and is not treated as idempotent — unlike Complete/Dismiss,
    /// APPLICATION_LAYER_SPEC.md §3.8's own Idempotency row names only "a second
    /// Complete/Dismiss call," not Reopen. Restores to Due or Scheduled depending on
    /// whether DueAt has already passed "now" — this Occurrence never recorded which of the
    /// two it was in before being resolved, so its status is recomputed from its own DueAt
    /// rather than stored and restored.
    /// </summary>
    public void Undo(DateTimeOffset undoneAt)
    {
        if (!IsResolved)
            throw new InvalidOperationException($"Occurrence {Id} is not currently resolved and cannot be reopened.");

        Status = undoneAt >= DueAt ? OccurrenceStatus.Due : OccurrenceStatus.Scheduled;
        ResolvedByUserId = null;
        ResolvedAt = null;
        Raise(new OccurrenceUndone(Guid.CreateVersion7(), undoneAt, Id, ReminderId));
    }
}
