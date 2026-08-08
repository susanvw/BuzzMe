using BuzzMe.Domain.Boards;
using BuzzMe.Infrastructure.Persistence.Mongo.Boards.Mappers;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Boards;

/// <summary>
/// A hand-written repository for exactly the Board aggregate — DEVELOPMENT_GUIDE.md §3's
/// "no generic repository." Implements the three operations this sprint's use cases need;
/// nothing else.
/// </summary>
public sealed class BoardRepository(MongoContext context) : IBoardRepository
{
    private IMongoCollection<BoardDocument> Collection => context.Database.GetCollection<BoardDocument>("boards");

    public async Task AddAsync(Board board, CancellationToken cancellationToken)
    {
        // Insert-only for this sprint: nothing yet updates an existing Board, so there is
        // no version-checked replace path to write (and none to test) — Version is stamped
        // onto the document at 0 here, ready for the update path a future sprint adds.
        await Collection.InsertOneAsync(BoardMapper.ToDocument(board), cancellationToken: cancellationToken);
    }

    public async Task<Board?> GetByIdAsync(BoardId id, CancellationToken cancellationToken)
    {
        var document = await Collection
            .Find(Builders<BoardDocument>.Filter.Eq(d => d.Id, id.Value))
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : BoardMapper.ToDomain(document);
    }

    public async Task<IReadOnlyList<Board>> ListByMemberAsync(
        Guid userId, Guid? afterId, int limit, CancellationToken cancellationToken)
    {
        var filter = Builders<BoardDocument>.Filter.ElemMatch(d => d.Memberships, m => m.UserId == userId);
        if (afterId is { } cursor)
            filter &= Builders<BoardDocument>.Filter.Gt(d => d.Id, cursor);

        var documents = await Collection
            .Find(filter)
            .SortBy(d => d.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return documents.Select(BoardMapper.ToDomain).ToList();
    }

    public async Task AddMemberAsync(BoardId boardId, Guid userId, CancellationToken cancellationToken)
    {
        var filter = Builders<BoardDocument>.Filter.Eq(d => d.Id, boardId.Value);
        var update = Builders<BoardDocument>.Update.Push(
            d => d.Memberships, new MembershipDocument { UserId = userId, Role = MembershipRole.Member.ToString(), Muted = false });

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task SetMembershipMutedAsync(BoardId boardId, Guid userId, bool muted, CancellationToken cancellationToken)
    {
        var filter = Builders<BoardDocument>.Filter.Eq(d => d.Id, boardId.Value)
            & Builders<BoardDocument>.Filter.ElemMatch(d => d.Memberships, m => m.UserId == userId);
        var update = Builders<BoardDocument>.Update.Set("Memberships.$.Muted", muted);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}
