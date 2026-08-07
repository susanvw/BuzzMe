using System.Security.Cryptography;
using BuzzMe.Domain.Invitations;

namespace BuzzMe.Infrastructure.Ids;

/// <summary>
/// A 256-bit, cryptographically random hex string per token — deliberately not a GUIDv7
/// like <see cref="TimeSortableIdGenerator"/> (see InvitationToken's own doc comment for
/// why a bearer credential must not be time-sortable).
/// </summary>
public sealed class SecureInvitationTokenGenerator : IInvitationTokenGenerator
{
    public InvitationToken NewToken() => new(RandomNumberGenerator.GetHexString(64));
}
