namespace BuzzMe.Domain.Invitations;

/// <summary>An Invitation's internal identity — same pattern as BoardId/ReminderId. Distinct from <see cref="InvitationToken"/>, the external, single-use credential the API addresses an Invitation by (API_CONTRACT.md §5's `{token}` path parameter).</summary>
public readonly record struct InvitationId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
