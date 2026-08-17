using BuzzMe.Application.Boards.Models;
using BuzzMe.Application.Common;
using BuzzMe.Contracts.V1.Boards;
using BuzzMe.Contracts.V1.Common;

namespace BuzzMe.Api.Mapping;

/// <summary>Application → Contracts mapping for Boards — extension methods, not a generic mapper (DEVELOPMENT_GUIDE.md §3).</summary>
public static class BoardMapping
{
    public static BoardResponse ToResponse(this BoardResult result) =>
        new(result.Id, result.Name, result.OwnerUserId, result.CreatedAt);

    public static ApiListResponse<BoardResponse> ToListResponse(this PagedResult<BoardResult> paged) =>
        ApiListResponse<BoardResponse>.Ok(
            paged.Items.Select(item => item.ToResponse()).ToList(),
            new PaginationInfo(paged.NextCursor));

    public static MembershipResponse ToResponse(this MembershipResult result) =>
        new(result.BoardId, result.UserId, result.DisplayName, result.PhotoUrl, result.Role, result.Muted, result.JoinedAt);

    public static ApiListResponse<MembershipResponse> ToListResponse(this PagedResult<MembershipResult> paged) =>
        ApiListResponse<MembershipResponse>.Ok(
            paged.Items.Select(item => item.ToResponse()).ToList(),
            new PaginationInfo(paged.NextCursor));

    public static LeaveBoardResponse ToResponse(this LeaveBoardResult result) => new(result.ReassignedOwnerUserId);
}
