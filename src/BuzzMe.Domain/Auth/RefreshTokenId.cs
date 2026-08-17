namespace BuzzMe.Domain.Auth;

/// <summary>A RefreshToken's internal identity — same pattern as BoardId/InvitationId. Distinct from the bearer token value itself, which is never stored — only its hash (see RefreshToken's own doc comment).</summary>
public readonly record struct RefreshTokenId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
