using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Devices;

public sealed record GetTrustedDevicesQuery(
    int PageNumber,
    int PageSize,
    Guid? UserId = null,
    bool? IsTrusted = null,
    bool? IsBlocked = null,
    string? SearchTerm = null) : IRequest<Result<PagedResult<TrustedDeviceDto>>>;
