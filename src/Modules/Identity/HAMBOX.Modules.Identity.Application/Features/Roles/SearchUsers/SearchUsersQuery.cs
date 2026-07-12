using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.SearchUsers;

public sealed record SearchUsersQuery(
    int PageNumber,
    int PageSize,
    string? SearchTerm,
    Guid? ExcludeRoleId) : IRequest<Result<PagedResult<UserSearchResultDto>>>;
