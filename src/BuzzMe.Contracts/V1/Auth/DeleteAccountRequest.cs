namespace BuzzMe.Contracts.V1.Auth;

/// <summary>
/// API_CONTRACT.md §5 — DELETE /v1/users/me request body: `{ confirmation: true }`. Not a
/// password re-entry — IMPLEMENTATION_SPEC.md §2 separately names "valid re-authentication"
/// as ConfirmAccountDeletion's precondition, which this endpoint's own required Bearer
/// token (already short-lived, Sprint 9) satisfies; `confirmation` is the explicit
/// "yes I mean it" acknowledgment, the same role IMPLEMENTATION_SPEC.md §2 gives DeleteBoard's
/// "explicit confirmation... naming the Board," simplified to a boolean since an account has
/// no name to type back. See SPRINT_12_REPORT.md for the full contradiction note.
/// </summary>
public sealed record DeleteAccountRequest(bool Confirmation);
