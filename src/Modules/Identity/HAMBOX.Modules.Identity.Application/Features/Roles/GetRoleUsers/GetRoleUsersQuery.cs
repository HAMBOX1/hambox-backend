using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.GetRoleUsers;

public sealed record GetRoleUsersQuery(
    Guid RoleId,
    int PageNumber,
    int PageSize,
    string? SearchTerm) : IRequest<Result<PagedResult<UserSearchResultDto>>>;
