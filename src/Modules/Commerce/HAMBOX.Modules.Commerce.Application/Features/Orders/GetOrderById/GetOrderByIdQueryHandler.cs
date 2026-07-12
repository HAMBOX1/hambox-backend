using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.GetOrderById;

internal sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Result<Contracts.Account.OrderDetailDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetOrderByIdQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Contracts.Account.OrderDetailDto>> Handle(
        GetOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<Contracts.Account.OrderDetailDto>(CommerceErrors.AuthenticationRequired);
        }

        var order = await _commerceDbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(
                o => o.Id == request.OrderId && o.UserId == _currentUserService.UserId,
                cancellationToken);

        if (order is null)
        {
            return Result.Failure<Contracts.Account.OrderDetailDto>(CommerceErrors.OrderNotFound);
        }

        var licenseKeys = await _commerceDbContext.OrderLicenseKeys
            .Where(k => k.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        var productIds = order.Items.Select(i => i.ProductId).ToList();
        var reviews = await _commerceDbContext.ProductReviews
            .Where(r => r.UserId == _currentUserService.UserId && productIds.Contains(r.ProductId))
            .ToDictionaryAsync(r => r.ProductId, cancellationToken);

        string? invoiceUrl = null;
        var supportUrl = $"/support/orders/{order.OrderNumber}";

        return Result.Success(CommerceMapper.ToOrderDetailDto(
            order,
            licenseKeys,
            reviews,
            invoiceUrl,
            supportUrl));
    }
}
