using BuzzMe.Domain.Invitations;

namespace BuzzMe.Application.Tests.TestDoubles;

/// <summary>In-memory IInvitationRepository — appropriate for Application-layer orchestration tests, same pattern as InMemoryBuzzRepository.</summary>
public sealed class InMemoryInvitationRepository : IInvitationRepository
{
    private readonly List<Invitation> _invitations = [];

    public Task AddAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        _invitations.Add(invitation);
        return Task.CompletedTask;
    }

    public Task<Invitation?> GetByIdAsync(InvitationId id, CancellationToken cancellationToken) =>
        Task.FromResult(_invitations.FirstOrDefault(invitation => invitation.Id == id));

    public Task<Invitation?> GetByTokenAsync(InvitationToken token, CancellationToken cancellationToken) =>
        Task.FromResult(_invitations.FirstOrDefault(invitation => invitation.Token == token));

    public Task UpdateAsync(Invitation invitation, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
