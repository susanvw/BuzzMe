namespace BuzzMe.Domain.Invitations;

/// <summary>
/// DOMAIN_MODEL.md's Invitation lifecycle: `Created → Pending → (Accepted | Declined |
/// Expired | Revoked)`. "Created" and "Pending" are one atomic step here (no separate
/// pre-Pending state is ever observable — same reasoning as every other aggregate's
/// Create/Generate factory landing directly in its first real status).
///
/// <see cref="Expired"/> is modeled but never stored this sprint: expiration is evaluated
/// lazily via <see cref="Invitation.IsExpired"/> against <see cref="Invitation.ExpiresAt"/>,
/// not a background sweep transitioning Status (Sprint 5 brief: "No background cleanup
/// worker yet. Expired invitations may simply be rejected when used."). The value is kept
/// in the enum because IMPLEMENTATION_SPEC.md's event table already names
/// `InvitationExpired` as a real, specified terminal outcome — a future sweep only needs to
/// start setting it, not invent it.
/// </summary>
public enum InvitationStatus
{
    Pending,
    Accepted,
    Declined,
    Expired,
    Revoked,
}

/// <summary>Canonical short-codes matching API_CONTRACT.md §3's Invitation resource — `pending`|`accepted`|`declined`|`revoked`|`expired`.</summary>
public static class InvitationStatusCodes
{
    public static string ToCode(this InvitationStatus status) => status switch
    {
        InvitationStatus.Pending => "pending",
        InvitationStatus.Accepted => "accepted",
        InvitationStatus.Declined => "declined",
        InvitationStatus.Expired => "expired",
        InvitationStatus.Revoked => "revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static bool TryParse(string? code, out InvitationStatus status)
    {
        switch (code)
        {
            case "pending": status = InvitationStatus.Pending; return true;
            case "accepted": status = InvitationStatus.Accepted; return true;
            case "declined": status = InvitationStatus.Declined; return true;
            case "expired": status = InvitationStatus.Expired; return true;
            case "revoked": status = InvitationStatus.Revoked; return true;
            default: status = default; return false;
        }
    }
}
