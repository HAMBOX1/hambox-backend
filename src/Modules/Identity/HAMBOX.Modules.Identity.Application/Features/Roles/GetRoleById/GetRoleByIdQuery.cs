using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.GetRoleById;

public sealed record GetRoleByIdQuery(Guid RoleId) : IRequest<Result<RoleDetailDto>>;
