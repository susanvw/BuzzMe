namespace BuzzMe.Domain.Users;

/// <summary>Declared in Domain, implemented in Infrastructure — only what Sprint 8's use cases need.</summary>
public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken);

    /// <summary>IMPLEMENTATION_SPEC.md §1's uniqueness invariant — checked before Provision and before any Email/Phone change in UpdateProfile. <paramref name="excludingUserId"/> lets UpdateProfile check uniqueness against everyone except the requester's own existing record.</summary>
    Task<bool> ExistsWithEmailOrPhoneAsync(string? email, string? phone, UserId? excludingUserId, CancellationToken cancellationToken);

    /// <summary>A full replace of the User's mutable profile fields (DisplayName/PhotoUrl/Email/Phone) — UpdateProfile changes more than one field together.</summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken);
}
