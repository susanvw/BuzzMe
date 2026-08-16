using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Users;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Users.Mappers;

/// <summary>The one place User (Domain) and UserDocument (Mongo) translate into each other — DEVELOPMENT_GUIDE.md §4.</summary>
internal static class UserMapper
{
    public static UserDocument ToDocument(User user) => new()
    {
        Id = user.Id.Value,
        Email = user.Email,
        Phone = user.Phone,
        DisplayName = user.DisplayName.Value,
        PhotoUrl = user.PhotoUrl,
        PersonalBoardId = user.PersonalBoardId.Value,
        Status = user.Status.ToCode(),
        CreatedAt = user.CreatedAt,
        Version = user.Version,
    };

    public static User ToDomain(UserDocument document)
    {
        if (!UserStatusCodes.TryParse(document.Status, out var status))
            throw new InvalidOperationException($"Stored User {document.Id} has an unrecognized status code '{document.Status}'.");

        return User.Rehydrate(
            new UserId(document.Id),
            document.Email,
            document.Phone,
            new DisplayName(document.DisplayName),
            document.PhotoUrl,
            new BoardId(document.PersonalBoardId),
            status,
            document.CreatedAt,
            document.Version);
    }
}
