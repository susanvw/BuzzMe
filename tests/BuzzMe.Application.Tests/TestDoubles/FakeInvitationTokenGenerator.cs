using BuzzMe.Domain.Invitations;

namespace BuzzMe.Application.Tests.TestDoubles;

public sealed class FakeInvitationTokenGenerator : IInvitationTokenGenerator
{
    public InvitationToken NewToken() => new(Guid.CreateVersion7().ToString());
}
