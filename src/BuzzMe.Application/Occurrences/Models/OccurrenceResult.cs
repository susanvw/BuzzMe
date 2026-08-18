using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Users;

namespace BuzzMe.Application.Occurrences.Models;

/// <summary>
/// The Application-layer shape of an Occurrence — DEVELOPMENT_GUIDE.md §3/§4.
/// <see cref="ResolvedByDisplayName"/> is populated only by the Sprint 15 resolution
/// methods (Complete/Dismiss/Reopen), which have a resolving User to look up — mirrors
/// MembershipResult.FromDomain's own optional-User-lookup precedent from Sprint 11.
/// <see cref="Version"/> is not part of API_CONTRACT.md §3's own Occurrence field list, but
/// the wire-level `{ expectedVersion }` mechanism that same document specifies for
/// Complete/Dismiss/Reopen has no other way to reach the client — see SPRINT_15_REPORT.md.
/// </summary>
public sealed record OccurrenceResult(
    Guid Id,
    Guid ReminderId,
    DateTimeOffset DueAt,
    string Status,
    DateTimeOffset GeneratedAt,
    Guid? ResolvedByUserId,
    string? ResolvedByDisplayName,
    DateTimeOffset? ResolvedAt,
    long Version)
{
    public static OccurrenceResult FromDomain(Occurrence occurrence, User? resolvedByUser = null) => new(
        occurrence.Id.Value,
        occurrence.ReminderId.Value,
        occurrence.DueAt,
        occurrence.Status.ToCode(),
        occurrence.GeneratedAt,
        occurrence.ResolvedByUserId,
        resolvedByUser?.DisplayName.Value,
        occurrence.ResolvedAt,
        occurrence.Version);
}

/// <summary>
/// The outcome of a Complete/Dismiss/Reopen attempt. <see cref="VersionConflict"/>
/// distinguishes a genuine fresh transition (or a matching-version idempotent replay) from
/// the "someone else already resolved/reopened it first" race APPLICATION_LAYER_SPEC.md
/// §3.8 documents as "not an error" — both are modeled as a <c>Result</c> success, since
/// neither represents a failure at the business level; only the flag tells the Api layer
/// which HTTP status (200 vs 409) API_CONTRACT.md §5 assigns to each. A plain
/// <c>Result&lt;T&gt;</c> failure has no way to carry a value, which is why this can't just
/// be an <c>Error.Conflict</c> — the 409 case still needs the resolved Occurrence in its body.
/// </summary>
public sealed record OccurrenceResolutionResult(OccurrenceResult Occurrence, bool VersionConflict);
