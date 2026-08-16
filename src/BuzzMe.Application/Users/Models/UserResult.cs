using BuzzMe.Domain.Users;

namespace BuzzMe.Application.Users.Models;

/// <summary>The Application-layer shape of a User — API_CONTRACT.md §3's User resource (self only): `id, displayName, photoUrl, email, phone, status, personalBoardId`.</summary>
public sealed record UserResult(Guid Id, string DisplayName, string? PhotoUrl, string? Email, string? Phone, string Status, Guid PersonalBoardId)
{
    public static UserResult FromDomain(User user) => new(
        user.Id.Value,
        user.DisplayName.Value,
        user.PhotoUrl,
        user.Email,
        user.Phone,
        user.Status.ToCode(),
        user.PersonalBoardId.Value);
}
