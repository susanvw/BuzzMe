using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Buzzes.Models;
using BuzzMe.Application.Common;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Buzzes;
using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Application.Buzzes;

/// <summary>
/// One Application Service for the Buzzes bounded-context area — Sprint 4's three use
/// cases (Generate/ListPending/Get). GenerateBuzzes is a System/Internal use case
/// (APPLICATION_LAYER_SPEC.md §2), but keeps the same requestingUserId-first,
/// Board-membership-gated shape as GenerateOccurrencesAsync (Sprint 3), for the same
/// consistency reason that precedent already established.
/// </summary>
public sealed class BuzzApplicationService(
    IBuzzRepository buzzRepository, IOccurrenceRepository occurrenceRepository,
    IReminderRepository reminderRepository, IBoardRepository boardRepository,
    IIdGenerator idGenerator, IClock clock)
{
    /// <summary>
    /// Generation Rules (Sprint 4 brief): the Reminder exists and is not deleted (enforced
    /// by ReminderRepository's soft-delete-filtering GetByIdAsync — Sprint 3.1), the
    /// Occurrence exists, the Board still exists, and one Buzz is generated per current
    /// Active Board Member who does not already have one for this Occurrence — which is
    /// exactly what makes repeated calls idempotent (no membership change → nothing left to
    /// generate → empty result). "The Member is active" is checked explicitly (Sprint 10):
    /// Board.Memberships can now hold historical Removed/Left rows for someone who left or
    /// was removed (Membership.cs), so iterating the raw collection would incorrectly
    /// re-buzz them. "The Member is not blocked" is NOT checked — see SPRINT_4_REPORT.md's
    /// specification gap: no Block concept is implemented anywhere in this codebase, so
    /// there is nothing to check against.
    /// </summary>
    public async Task<Result<IReadOnlyList<BuzzResult>>> GenerateBuzzesAsync(
        Guid requestingUserId, Guid occurrenceId, CancellationToken cancellationToken)
    {
        var occurrence = await occurrenceRepository.GetByIdAsync(new OccurrenceId(occurrenceId), cancellationToken);
        if (occurrence is null)
            return Result.Failure<IReadOnlyList<BuzzResult>>(Error.NotFound("Occurrence not found."));

        var reminder = await reminderRepository.GetByIdAsync(occurrence.ReminderId, cancellationToken);
        if (reminder is null)
            return Result.Failure<IReadOnlyList<BuzzResult>>(Error.NotFound("Occurrence not found."));

        var board = await boardRepository.GetByIdAsync(reminder.BoardId, cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure<IReadOnlyList<BuzzResult>>(Error.NotFound("Occurrence not found."));

        var existingRecipients = (await buzzRepository.ListByOccurrenceAsync(occurrence.Id, cancellationToken))
            .Select(buzz => buzz.RecipientUserId)
            .ToHashSet();

        var scheduledAt = occurrence.DueAt - reminder.NotifyPreset.ToLeadTime();

        var generated = new List<BuzzResult>();
        foreach (var membership in board.Memberships.Where(m => m.Status == MembershipStatus.Active))
        {
            if (existingRecipients.Contains(membership.UserId))
                continue;

            var buzz = Buzz.Generate(new BuzzId(idGenerator.NewId()), occurrence.Id, membership.UserId, scheduledAt, clock.UtcNow);
            await buzzRepository.AddAsync(buzz, cancellationToken);
            generated.Add(BuzzResult.FromDomain(buzz));
        }

        return Result.Success<IReadOnlyList<BuzzResult>>(generated);
    }

    /// <summary>Authorization: the requesting User can only ever see their own pending Buzzes — DOMAIN_MODEL.md's "no one else can act on [or see] another person's Buzz."</summary>
    public async Task<Result<PagedResult<BuzzResult>>> ListPendingBuzzesAsync(
        Guid requestingUserId, string? cursor, int limit, CancellationToken cancellationToken)
    {
        Guid? afterId = Guid.TryParse(cursor, out var parsed) ? parsed : null;

        var buzzes = await buzzRepository.ListPendingByRecipientAsync(requestingUserId, afterId, limit, cancellationToken);

        var nextCursor = buzzes.Count == limit ? buzzes[^1].Id.Value.ToString() : null;
        var results = buzzes.Select(BuzzResult.FromDomain).ToList();

        return Result.Success(new PagedResult<BuzzResult>(results, nextCursor));
    }

    /// <summary>Authorization: only the recipient may read their own Buzz — a NotFound (not Forbidden) for anyone else, same privacy-preserving default used throughout this codebase.</summary>
    public async Task<Result<BuzzResult>> GetBuzzAsync(Guid requestingUserId, Guid buzzId, CancellationToken cancellationToken)
    {
        var buzz = await buzzRepository.GetByIdAsync(new BuzzId(buzzId), cancellationToken);
        if (buzz is null || buzz.RecipientUserId != requestingUserId)
            return Result.Failure<BuzzResult>(Error.NotFound("Buzz not found."));

        return Result.Success(BuzzResult.FromDomain(buzz));
    }

    /// <summary>
    /// System/Internal use case (Sprint 6) — no requestingUserId, since this is the
    /// Worker's own polling query, not an authenticated User action; there is no
    /// meaningful failure branch (an empty result on a quiet queue isn't an error), so no
    /// Result&lt;T&gt; wrapper. Claiming is safe under concurrent workers by construction —
    /// see IBuzzRepository.ClaimPendingAsync's own doc comment.
    /// </summary>
    public Task<IReadOnlyList<Buzz>> ClaimPendingBuzzesAsync(int batchSize, CancellationToken cancellationToken) =>
        buzzRepository.ClaimPendingAsync(clock.UtcNow, batchSize, cancellationToken);

    /// <summary>
    /// Takes the already-claimed Buzz directly (not an ID) — it was just returned by
    /// <see cref="ClaimPendingBuzzesAsync"/>, so re-loading it would be a wasted round trip
    /// and would also throw away the Version IBuzzRepository.UpdateAsync's
    /// optimistic-concurrency check needs.
    /// </summary>
    public async Task MarkDeliveredAsync(Buzz buzz, CancellationToken cancellationToken)
    {
        buzz.MarkDelivered(clock.UtcNow);
        await buzzRepository.UpdateAsync(buzz, cancellationToken);
    }

    /// <summary>Terminal for this sprint — no retry is scheduled (SPRINT_6_REPORT.md's specification gap on `RetryScheduled`).</summary>
    public async Task MarkFailedAsync(Buzz buzz, CancellationToken cancellationToken)
    {
        buzz.MarkFailed(clock.UtcNow);
        await buzzRepository.UpdateAsync(buzz, cancellationToken);
    }
}
