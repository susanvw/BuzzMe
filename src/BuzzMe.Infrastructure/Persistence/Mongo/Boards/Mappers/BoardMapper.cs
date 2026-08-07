using BuzzMe.Domain.Boards;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Boards.Mappers;

/// <summary>The one place Board (Domain) and BoardDocument (Mongo) translate into each other — DEVELOPMENT_GUIDE.md §4.</summary>
internal static class BoardMapper
{
    public static BoardDocument ToDocument(Board board) => new()
    {
        Id = board.Id.Value,
        Name = board.Name.Value,
        CreatedAt = board.CreatedAt,
        Memberships = board.Memberships.Select(ToDocument).ToList(),
        Version = board.Version,
    };

    private static MembershipDocument ToDocument(Membership membership) => new()
    {
        UserId = membership.UserId,
        Role = membership.Role.ToString(),
    };

    public static Board ToDomain(BoardDocument document) => Board.Rehydrate(
        new BoardId(document.Id),
        new BoardName(document.Name),
        document.CreatedAt,
        document.Memberships.Select(ToDomain),
        document.Version);

    private static Membership ToDomain(MembershipDocument document) =>
        new(document.UserId, Enum.Parse<MembershipRole>(document.Role));
}
