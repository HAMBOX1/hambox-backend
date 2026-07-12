using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.GetRoles;

public sealed record GetRolesQuery(
    int PageNumber,
    int PageSize,
    string? SearchTerm) : IRequest<Result<PagedResult<RoleListItemDto>>>;
