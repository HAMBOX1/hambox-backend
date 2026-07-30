using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Analytics.GetSupportStatistics;

public sealed record GetSupportStatisticsQuery(DateTimeOffset? DateFrom, DateTimeOffset? DateTo)
    : IRequest<Result<SupportStatisticsDto>>;
