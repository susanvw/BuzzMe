using BuzzMe.Domain.Occurrences.Events;
using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Occurrences;

/// <summary>
/// A single, concrete, dated instance of a Reminder becoming due — IMPLEMENTATION_SPEC.md
/// §1. Its own Aggregate Root, deliberately separate from Reminder, for the scale reasons
/// that document already gives. Sprint 3 scope only: generation. No completion, no
/// resolution — <see cref="ResolvedByUserId"/>/<see cref="ResolvedAt"/> exist because
/// API_CONTRACT.md §3 already reserves them on the wire shape, but nothing in this sprint
/// ever sets them (mirrors Reminder's `nextOccurrence`/`updatedAt` precedent from Sprint 2).
/// </summary>
public sealed class Occurrence : AggregateRoot<OccurrenceId>
{
    public ReminderId ReminderId { get; private init; }

    /// <summary>The absolute UTC instant this Occurrence is due — computed once, at generation, and immutable afterward (IMPLEMENTATION_SPEC.md §1).</summary>
    public DateTimeOffset DueAt { get; private init; }

    public OccurrenceStatus Status { get; private init; }

    public DateTimeOffset GeneratedAt { get; private init; }

    public Guid? ResolvedByUserId { get; private init; }

    public DateTimeOffset? ResolvedAt { get; private init; }

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
}
