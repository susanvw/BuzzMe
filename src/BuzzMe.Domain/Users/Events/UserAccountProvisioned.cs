using BuzzMe.Domain.Boards;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Users.Events;

/// <summary>
/// Deliberately not `AccountRegistered` or `AccountVerified` (IMPLEMENTATION_SPEC.md §2) —
/// Sprint 8's ProvisionAccountAsync collapses both canonical steps (and the Account
/// Provisioning policy that follows AccountVerified) into one operation, since there is no
/// password/verification-code infrastructure in this codebase for the real two-step flow
/// to run against (see SPRINT_8_REPORT.md). Named for what actually happened here, not
/// borrowed from either spec event it only partially resembles.
/// </summary>
public sealed record UserAccountProvisioned(Guid EventId, DateTimeOffset OccurredAt, UserId UserId, BoardId PersonalBoardId) : IDomainEvent;
