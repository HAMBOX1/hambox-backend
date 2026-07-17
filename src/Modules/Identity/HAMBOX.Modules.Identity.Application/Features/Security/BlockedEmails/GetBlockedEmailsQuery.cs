using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedEmails;

public sealed record GetBlockedEmailsQuery(
    int PageNumber,
    int PageSize,
    string? SearchTerm) : IRequest<Result<PagedResult<BlockedEmailDto>>>;
