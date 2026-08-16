namespace BuzzMe.Domain.Users;

/// <summary>
/// IMPLEMENTATION_SPEC.md §1 — the full lifecycle (`PendingVerification → Active ⇄
/// Deactivated`; `Active|Deactivated → Suspended` platform-only; any non-Deleted state
/// `→ Deleted`, terminal). Sprint 8 only ever constructs `Active` — there is no
/// credential/verification-code infrastructure anywhere in this codebase for a User to
/// ever legitimately sit in `PendingVerification`, and no self-service Deactivate/Suspend/
/// Delete command exists yet either (see SPRINT_8_REPORT.md's specification gap). The
/// complete, already-specified enum is modeled now rather than a partial one, since the
/// values aren't speculative — same reasoning as OccurrenceStatus (Sprint 3) and BuzzStatus
/// (Sprint 4).
/// </summary>
public enum UserStatus
{
    PendingVerification,
    Active,
    Deactivated,
    Suspended,
    Deleted,
}

/// <summary>Canonical short-codes matching API_CONTRACT.md §3's User resource `status` field.</summary>
public static class UserStatusCodes
{
    public static string ToCode(this UserStatus status) => status switch
    {
        UserStatus.PendingVerification => "pendingVerification",
        UserStatus.Active => "active",
        UserStatus.Deactivated => "deactivated",
        UserStatus.Suspended => "suspended",
        UserStatus.Deleted => "deleted",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static bool TryParse(string? code, out UserStatus status)
    {
        switch (code)
        {
            case "pendingVerification": status = UserStatus.PendingVerification; return true;
            case "active": status = UserStatus.Active; return true;
            case "deactivated": status = UserStatus.Deactivated; return true;
            case "suspended": status = UserStatus.Suspended; return true;
            case "deleted": status = UserStatus.Deleted; return true;
            default: status = default; return false;
        }
    }
}
