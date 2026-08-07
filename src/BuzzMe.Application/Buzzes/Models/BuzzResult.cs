using BuzzMe.Domain.Buzzes;

namespace BuzzMe.Application.Buzzes.Models;

/// <summary>The Application-layer shape of a Buzz — DEVELOPMENT_GUIDE.md §3/§4.</summary>
public sealed record BuzzResult(
    Guid Id,
    Guid OccurrenceId,
    Guid RecipientUserId,
    DateTimeOffset ScheduledAt,
    string Status,
    int AttemptCount,
    DateTimeOffset CreatedAt)
{
    public static BuzzResult FromDomain(Buzz buzz) => new(
        buzz.Id.Value,
        buzz.OccurrenceId.Value,
        buzz.RecipientUserId,
        buzz.ScheduledAt,
        buzz.Status.ToCode(),
        buzz.AttemptCount,
        buzz.CreatedAt);
}
