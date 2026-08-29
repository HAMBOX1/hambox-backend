using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.OtpEvents;

public sealed record GetCustomerOtpEventsQuery(
    int PageNumber,
    int PageSize,
    Guid? UserId = null,
    string? Purpose = null,
    string? Status = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null) : IRequest<Result<PagedResult<CustomerOtpEventDto>>>;
