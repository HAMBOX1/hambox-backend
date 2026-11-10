using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Points.GetPointsHistory;

public sealed record GetPointsHistoryQuery(
    int PageNumber,
    int PageSize,
    string? Type) : IRequest<Result<PagedResult<PointsTransactionDto>>>;

internal sealed class GetPointsHistoryQueryHandler(ICommerceDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetPointsHistoryQuery, Result<PagedResult<PointsTransactionDto>>>
{
    public async Task<Result<PagedResult<PointsTransactionDto>>> Handle(
        GetPointsHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<PagedResult<PointsTransactionDto>>(CommerceErrors.AuthenticationRequired);
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = db.PointsTransactions.Where(t => t.UserId == userId);

        if (!string.IsNullOrWhiteSpace(request.Type) &&
            Enum.TryParse<PointsTransactionType>(request.Type, ignoreCase: true, out var type))
        {
            query = query.Where(t => t.Type == type);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(t => t.OccurredOnUtc)
            .ThenByDescending(t => t.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(t => new PointsTransactionDto(
                t.Id, t.Type.ToString(), t.Points, t.OrderId, t.OrderNumber, t.Description, t.OccurredOnUtc))
            .ToList();

        return Result.Success(new PagedResult<PointsTransactionDto>(items, pageNumber, pageSize, totalCount));
    }
}
