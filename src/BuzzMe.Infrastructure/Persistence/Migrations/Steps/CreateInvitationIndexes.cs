using BuzzMe.Infrastructure.Persistence.Mongo;
using BuzzMe.Infrastructure.Persistence.Mongo.Invitations;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Migrations.Steps;

/// <summary>
/// A unique index on Token — DOMAIN_MODEL.md's "single-use token" invariant enforced as a
/// real database constraint (an actual collision is astronomically unlikely given
/// SecureInvitationTokenGenerator's 256 bits of entropy, but the constraint is still the
/// correct defensive default, same reasoning as every other unique index in this
/// codebase). The second index originally backed `ListPendingByBoardAsync`'s
/// board+status+cursor query; that method was removed in Sprint 6 (no specification basis
/// — see SPRINT_6_REPORT.md). Left in place rather than editing this already-shipped,
/// numbered migration to drop it — a genuinely unused index is a minor, low-cost cleanup
/// item, not a correctness concern; see SPRINT_6_REPORT.md for the note.
/// </summary>
public sealed class CreateInvitationIndexes(MongoContext context) : IMongoMigration
{
    public int Version => 5;

    public string Description => "Create invitations.token (unique) and boardId+status+_id indexes";

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        var collection = context.Database.GetCollection<InvitationDocument>("invitations");

        var uniqueTokenIndex = new CreateIndexModel<InvitationDocument>(
            Builders<InvitationDocument>.IndexKeys.Ascending(d => d.Token),
            new CreateIndexOptions { Name = "ux_invitations_token", Unique = true });

        var pendingListIndex = new CreateIndexModel<InvitationDocument>(
            Builders<InvitationDocument>.IndexKeys.Ascending(d => d.BoardId).Ascending(d => d.Status).Ascending(d => d.Id),
            new CreateIndexOptions { Name = "ix_invitations_boardId_status_id" });

        await collection.Indexes.CreateManyAsync([uniqueTokenIndex, pendingListIndex], cancellationToken);
    }
}
