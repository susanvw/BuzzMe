using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Invitations;
using BuzzMe.Infrastructure.Persistence.Migrations.Steps;
using BuzzMe.Infrastructure.Persistence.Mongo.Invitations;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.IntegrationTests.Invitations;

/// <summary>Against a real, ephemeral MongoDB — Sprint 5's explicit "use a real MongoDB instance."</summary>
[Collection(MongoIntegrationTestCollection.Name)]
public sealed class InvitationRepositoryTests(MongoIntegrationTestFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly BoardId SomeBoardId = new(Guid.CreateVersion7());

    private InvitationRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _repository = new InvitationRepository(fixture.Context);
        await new CreateInvitationIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static Invitation NewInvitation(BoardId? boardId = null, InvitationToken? token = null, DateTimeOffset? expiresAt = null) =>
        Invitation.Send(
            new InvitationId(Guid.CreateVersion7()),
            token ?? new InvitationToken(Guid.NewGuid().ToString()),
            boardId ?? SomeBoardId,
            Guid.CreateVersion7(),
            InvitationChannel.Link,
            targetContact: null,
            expiresAt ?? Now.AddDays(7),
            Now);

    [Fact]
    public async Task AddAsync_PersistsTheInvitationAtVersionZero()
    {
        var invitation = NewInvitation();

        await _repository.AddAsync(invitation, CancellationToken.None);
        var reloaded = await _repository.GetByIdAsync(invitation.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(0, reloaded.Version);
        Assert.Equal(InvitationStatus.Pending, reloaded.Status);
        Assert.Equal(invitation.Token, reloaded.Token);
    }

    [Fact]
    public async Task AddAsync_RejectsADuplicateToken()
    {
        // The unique index is the real enforcement of the single-use token invariant
        // (DOMAIN_MODEL.md) — verified here at the database level, same pattern as
        // OccurrenceRepositoryTests/BuzzRepositoryTests' own duplicate-key tests.
        var token = new InvitationToken(Guid.NewGuid().ToString());
        await _repository.AddAsync(NewInvitation(token: token), CancellationToken.None);

        var duplicate = NewInvitation(token: token);
        await Assert.ThrowsAsync<MongoWriteException>(() => _repository.AddAsync(duplicate, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForAnUnknownId()
    {
        var result = await _repository.GetByIdAsync(new InvitationId(Guid.CreateVersion7()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByTokenAsync_ReturnsTheMatchingInvitation()
    {
        var token = new InvitationToken(Guid.NewGuid().ToString());
        var invitation = NewInvitation(token: token);
        await _repository.AddAsync(invitation, CancellationToken.None);

        var result = await _repository.GetByTokenAsync(token, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(invitation.Id, result.Id);
    }

    [Fact]
    public async Task GetByTokenAsync_ReturnsNullForAnUnknownToken()
    {
        var result = await _repository.GetByTokenAsync(new InvitationToken("unknown"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_PersistsTheAcceptedStatusAndAcceptedByUserId()
    {
        var invitation = NewInvitation();
        await _repository.AddAsync(invitation, CancellationToken.None);
        var acceptingUserId = Guid.CreateVersion7();
        invitation.Accept(acceptingUserId, Now);

        await _repository.UpdateAsync(invitation, CancellationToken.None);
        var reloaded = await _repository.GetByIdAsync(invitation.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(InvitationStatus.Accepted, reloaded.Status);
        Assert.Equal(acceptingUserId, reloaded.AcceptedByUserId);
        Assert.Equal(Now, reloaded.ResolvedAt);
    }
}
