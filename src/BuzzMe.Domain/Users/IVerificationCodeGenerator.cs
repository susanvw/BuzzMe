namespace BuzzMe.Domain.Users;

/// <summary>
/// Generates the short, human-typeable code VerifyAccount checks (IMPLEMENTATION_SPEC.md §2
/// — "a valid, unexpired verification code exists for this account"). Deliberately a
/// separate abstraction from ISecureTokenGenerator: a code delivered over email/SMS must be
/// short enough to type back, which is the opposite property from a 256-bit bearer token.
/// </summary>
public interface IVerificationCodeGenerator
{
    string NewCode();
}
