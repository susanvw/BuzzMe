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
        PasswordHash = user.PasswordHash,
        PersonalBoardId = user.PersonalBoardId?.Value,
        Status = user.Status.ToCode(),
        CreatedAt = user.CreatedAt,
        VerificationCode = user.VerificationCode,
        VerificationCodeExpiresAt = user.VerificationCodeExpiresAt,
        PasswordResetTokenHash = user.PasswordResetTokenHash,
        PasswordResetTokenExpiresAt = user.PasswordResetTokenExpiresAt,
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
            document.PasswordHash,
            document.PersonalBoardId is { } personalBoardId ? new BoardId(personalBoardId) : null,
            status,
            document.CreatedAt,
            document.VerificationCode,
            document.VerificationCodeExpiresAt,
            document.PasswordResetTokenHash,
            document.PasswordResetTokenExpiresAt,
            document.Version);
    }
}
