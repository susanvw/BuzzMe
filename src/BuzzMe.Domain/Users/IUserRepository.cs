namespace BuzzMe.Domain.Users;

/// <summary>Declared in Domain, implemented in Infrastructure — only what Sprint 8's use cases need.</summary>
public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken);

    /// <summary>IMPLEMENTATION_SPEC.md §1's uniqueness invariant — checked before Register and before any Email/Phone change in UpdateProfile. <paramref name="excludingUserId"/> lets UpdateProfile check uniqueness against everyone except the requester's own existing record.</summary>
    Task<bool> ExistsWithEmailOrPhoneAsync(string? email, string? phone, UserId? excludingUserId, CancellationToken cancellationToken);

    /// <summary>Login/VerifyAccount/ForgotPassword all resolve a User by whichever of email/phone the request carries — the same OR-across-both-fields shape as ExistsWithEmailOrPhoneAsync, just returning the match instead of a boolean.</summary>
    Task<User?> GetByEmailOrPhoneAsync(string? email, string? phone, CancellationToken cancellationToken);

    /// <summary>ResetPassword resolves the User a `{ token, newPassword }` request belongs to by the hash of its bearer token — never the plaintext, matching PasswordResetTokenHash's own at-rest discipline.</summary>
    Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>A full replace of the User's mutable fields — every command in this bounded context (UpdateProfile, Verify, ResetPassword, ...) changes more than one field together.</summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken);
}
