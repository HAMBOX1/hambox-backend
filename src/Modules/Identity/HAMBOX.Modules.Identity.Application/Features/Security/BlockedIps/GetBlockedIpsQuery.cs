using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedIps;

public sealed record GetBlockedIpsQuery(
    int PageNumber,
    int PageSize,
    string? SearchTerm) : IRequest<Result<PagedResult<BlockedIpDto>>>;
