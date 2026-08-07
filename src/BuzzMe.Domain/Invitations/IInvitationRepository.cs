namespace BuzzMe.Domain.Invitations;

/// <summary>
/// Declared in Domain, implemented in Infrastructure — only what the implemented use cases
/// need. `ListPendingByBoardAsync` (Sprint 5) was removed in Sprint 6: it existed solely to
/// back `ListPendingInvitationsAsync`, which review found had no specification basis
/// anywhere (no APPLICATION_LAYER_SPEC.md row, no API_CONTRACT.md endpoint, no Business
/// Behavior scenario) — see SPRINT_6_REPORT.md. Its shape (per-Board) wasn't even what a
/// future global Expire-Invitations sweep would need, confirming it was speculative in
/// both name and shape, not just missing an API surface.
/// </summary>
public interface IInvitationRepository
{
    Task AddAsync(Invitation invitation, CancellationToken cancellationToken);

    Task<Invitation?> GetByIdAsync(InvitationId id, CancellationToken cancellationToken);

    /// <summary>The primary lookup path for the invitee-facing flows (Validate/Accept/Decline) — all addressed by token, per API_CONTRACT.md §5.</summary>
    Task<Invitation?> GetByTokenAsync(InvitationToken token, CancellationToken cancellationToken);

    /// <summary>A full replace of the Invitation's mutable state (Status/AcceptedByUserId/ResolvedAt) — Accept/Decline/Revoke each change more than one field together, unlike Reminder's single-field soft delete.</summary>
    Task UpdateAsync(Invitation invitation, CancellationToken cancellationToken);
}
