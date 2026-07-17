using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedUsers;

/// <summary>
/// Lists users currently in a restricted status (Suspended/Blocked/Banned), optionally filtered
/// by a specific status and/or a search term over email/name.
/// </summary>
public sealed record GetBlockedUsersQuery(
    int PageNumber,
    int PageSize,
    string? SearchTerm,
    string? Status) : IRequest<Result<PagedResult<BlockedUserListItemDto>>>;
