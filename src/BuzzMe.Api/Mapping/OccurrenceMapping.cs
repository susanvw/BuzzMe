using BuzzMe.Application.Occurrences.Models;
using BuzzMe.Contracts.V1.Occurrences;

namespace BuzzMe.Api.Mapping;

/// <summary>Application → Contracts mapping for Occurrences — extension methods, not a generic mapper (DEVELOPMENT_GUIDE.md §3).</summary>
public static class OccurrenceMapping
{
    public static OccurrenceResponse ToResponse(this OccurrenceResult result) => new(
        result.Id,
        result.ReminderId,
        result.DueAt,
        result.Status,
        result.ResolvedByUserId is { } resolvedByUserId ? new ResolvedByResponse(resolvedByUserId, result.ResolvedByDisplayName) : null,
        result.ResolvedAt,
        result.Version);
}
