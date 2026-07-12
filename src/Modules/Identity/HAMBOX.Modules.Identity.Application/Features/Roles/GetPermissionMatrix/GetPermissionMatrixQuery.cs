using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.GetPermissionMatrix;

public sealed record GetPermissionMatrixQuery : IRequest<Result<IReadOnlyCollection<PermissionGroupDto>>>;
