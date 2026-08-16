using BuzzMe.Application.Users.Models;
using BuzzMe.Contracts.V1.Users;

namespace BuzzMe.Api.Mapping;

/// <summary>Application → Contracts mapping for Users — extension methods, not a generic mapper (DEVELOPMENT_GUIDE.md §3).</summary>
public static class UserMapping
{
    public static UserResponse ToResponse(this UserResult result) =>
        new(result.Id, result.DisplayName, result.PhotoUrl, result.Email, result.Phone, result.Status, result.PersonalBoardId);
}
