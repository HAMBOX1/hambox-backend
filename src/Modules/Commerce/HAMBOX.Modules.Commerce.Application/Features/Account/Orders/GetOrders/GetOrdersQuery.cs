using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Orders.GetOrders;

public sealed record GetOrdersQuery(int PageNumber, int PageSize) : IRequest<Result<PagedResult<Contracts.Account.OrderSummaryDto>>>;

internal sealed class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, Result<PagedResult<Contracts.Account.OrderSummaryDto>>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetOrdersQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedResult<Contracts.Account.OrderSummaryDto>>> Handle(
        GetOrdersQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<PagedResult<Contracts.Account.OrderSummaryDto>>(CommerceErrors.AuthenticationRequired);
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _commerceDbContext.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == _currentUserService.UserId)
            .OrderByDescending(o => o.CreatedOnUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // One representative image per order (its first line item) — a single batch lookup
        // covers every order on the page, not one query per order.
        var firstProductIdByOrder = orders.ToDictionary(
            o => o.Id,
            o => o.Items.Select(i => i.ProductId).FirstOrDefault(id => id.HasValue));
        var imageUrls = await ProductPrimaryImageResolver.GetPrimaryImageUrlsAsync(
            _catalogDbContext,
            firstProductIdByOrder.Values.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList(),
            cancellationToken);

        var summaries = orders
            .Select(order =>
            {
                var firstProductId = firstProductIdByOrder[order.Id];
                var imageUrl = firstProductId.HasValue ? imageUrls.GetValueOrDefault(firstProductId.Value) : null;
                return AccountMapper.ToOrderSummaryDto(order, imageUrl);
            })
            .ToList();

        return Result.Success(new PagedResult<Contracts.Account.OrderSummaryDto>(
            summaries,
            pageNumber,
            pageSize,
            totalCount));
    }
}
