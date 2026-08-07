using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Reminders;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Occurrences.Mappers;

/// <summary>The one place Occurrence (Domain) and OccurrenceDocument (Mongo) translate into each other — DEVELOPMENT_GUIDE.md §4.</summary>
internal static class OccurrenceMapper
{
    public static OccurrenceDocument ToDocument(Occurrence occurrence) => new()
    {
        Id = occurrence.Id.Value,
        ReminderId = occurrence.ReminderId.Value,
        DueAt = occurrence.DueAt,
        Status = occurrence.Status.ToCode(),
        GeneratedAt = occurrence.GeneratedAt,
        ResolvedByUserId = occurrence.ResolvedByUserId,
        ResolvedAt = occurrence.ResolvedAt,
        Version = occurrence.Version,
    };

    public static Occurrence ToDomain(OccurrenceDocument document)
    {
        if (!OccurrenceStatusCodes.TryParse(document.Status, out var status))
            throw new InvalidOperationException($"Stored Occurrence {document.Id} has an unrecognized status code '{document.Status}'.");

        return Occurrence.Rehydrate(
            new OccurrenceId(document.Id),
            new ReminderId(document.ReminderId),
            document.DueAt,
            status,
            document.GeneratedAt,
            document.ResolvedByUserId,
            document.ResolvedAt,
            document.Version);
    }
}
