namespace BuzzMe.Domain.Invitations;

/// <summary>API_CONTRACT.md §5's Invite Member request body — `channel` (`link`|`email`|`sms`), with a target contact required only for `email`/`sms` (DOMAIN_MODEL.md: "for targeted invitations" vs. "for link/QR invitations, resolves the invitee identity only at acceptance time").</summary>
public enum InvitationChannel
{
    Link,
    Email,
    Sms,
}

public static class InvitationChannelCodes
{
    public static string ToCode(this InvitationChannel channel) => channel switch
    {
        InvitationChannel.Link => "link",
        InvitationChannel.Email => "email",
        InvitationChannel.Sms => "sms",
        _ => throw new ArgumentOutOfRangeException(nameof(channel)),
    };

    public static bool TryParse(string? code, out InvitationChannel channel)
    {
        switch (code)
        {
            case "link": channel = InvitationChannel.Link; return true;
            case "email": channel = InvitationChannel.Email; return true;
            case "sms": channel = InvitationChannel.Sms; return true;
            default: channel = default; return false;
        }
    }
}
