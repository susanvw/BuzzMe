namespace BuzzMe.Domain.SeedWork;

/// <summary>
/// Generates an unguessable, high-entropy bearer credential — the general-purpose version
/// of Invitations' IInvitationTokenGenerator, needed here for RefreshToken and password
/// reset tokens, neither of which is an Invitation. Declared in Domain for the same reason
/// as IIdGenerator/IClock; implemented in Infrastructure using a cryptographically random
/// source, deliberately not the time-sortable IIdGenerator (a bearer credential must not be
/// sortable — same reasoning as InvitationToken's own doc comment).
/// </summary>
public interface ISecureTokenGenerator
{
    string NewToken();
}
