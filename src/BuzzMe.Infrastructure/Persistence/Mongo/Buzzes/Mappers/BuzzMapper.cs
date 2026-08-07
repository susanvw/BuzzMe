using BuzzMe.Domain.Buzzes;
using BuzzMe.Domain.Occurrences;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Buzzes.Mappers;

/// <summary>The one place Buzz (Domain) and BuzzDocument (Mongo) translate into each other — DEVELOPMENT_GUIDE.md §4.</summary>
internal static class BuzzMapper
{
    public static BuzzDocument ToDocument(Buzz buzz) => new()
    {
        Id = buzz.Id.Value,
        OccurrenceId = buzz.OccurrenceId.Value,
        RecipientUserId = buzz.RecipientUserId,
        ScheduledAt = buzz.ScheduledAt,
        Status = buzz.Status.ToCode(),
        AttemptCount = buzz.AttemptCount,
        CreatedAt = buzz.CreatedAt,
        Version = buzz.Version,
    };

    public static Buzz ToDomain(BuzzDocument document)
    {
        if (!BuzzStatusCodes.TryParse(document.Status, out var status))
            throw new InvalidOperationException($"Stored Buzz {document.Id} has an unrecognized status code '{document.Status}'.");

        return Buzz.Rehydrate(
            new BuzzId(document.Id),
            new OccurrenceId(document.OccurrenceId),
            document.RecipientUserId,
            document.ScheduledAt,
            status,
            document.AttemptCount,
            document.CreatedAt,
            document.Version);
    }
}
