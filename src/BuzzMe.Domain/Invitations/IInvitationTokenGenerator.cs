namespace BuzzMe.Domain.Invitations;

/// <summary>
/// Generates the unguessable external credential a new Invitation is addressed by.
/// Declared in Domain so <see cref="Invitation"/>'s callers can depend on it without
/// depending on Infrastructure, same reasoning as SeedWork's IIdGenerator/IClock
/// (DEVELOPMENT_GUIDE.md §2) — implemented in Infrastructure using a cryptographically
/// random source, deliberately not the same time-sortable generator every aggregate ID
/// uses (see InvitationToken's own doc comment for why).
/// </summary>
public interface IInvitationTokenGenerator
{
    InvitationToken NewToken();
}
