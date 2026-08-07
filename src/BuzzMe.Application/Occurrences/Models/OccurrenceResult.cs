using BuzzMe.Domain.Occurrences;

namespace BuzzMe.Application.Occurrences.Models;

/// <summary>The Application-layer shape of an Occurrence — DEVELOPMENT_GUIDE.md §3/§4. ResolvedBy/ResolvedAt are always null this sprint (completion is out of scope) but kept present, matching Reminder's `nextOccurrence` precedent from Sprint 2.</summary>
public sealed record OccurrenceResult(
    Guid Id,
    Guid ReminderId,
    DateTimeOffset DueAt,
    string Status,
    DateTimeOffset GeneratedAt,
    Guid? ResolvedByUserId,
    DateTimeOffset? ResolvedAt)
{
    public static OccurrenceResult FromDomain(Occurrence occurrence) => new(
        occurrence.Id.Value,
        occurrence.ReminderId.Value,
        occurrence.DueAt,
        occurrence.Status.ToCode(),
        occurrence.GeneratedAt,
        occurrence.ResolvedByUserId,
        occurrence.ResolvedAt);
}
