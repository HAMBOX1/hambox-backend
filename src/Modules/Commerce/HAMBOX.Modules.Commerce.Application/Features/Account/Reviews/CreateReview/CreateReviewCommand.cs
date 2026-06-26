using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.CreateReview;

public sealed record CreateReviewCommand(
    Guid ProductId,
    Guid OrderId,
    int Rating,
    string Comment) : IRequest<Result<ProductReviewDto>>;

internal sealed class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, Result<ProductReviewDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateReviewCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ProductReviewDto>> Handle(
        CreateReviewCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<ProductReviewDto>(CommerceErrors.AuthenticationRequired);
        }

        var existing = await _commerceDbContext.ProductReviews
            .AnyAsync(
                r => r.UserId == _currentUserService.UserId && r.ProductId == request.ProductId,
                cancellationToken);

        if (existing)
        {
            return Result.Failure<ProductReviewDto>(CommerceErrors.ReviewAlreadyExists);
        }

        var order = await _commerceDbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(
                o => o.Id == request.OrderId
                     && o.UserId == _currentUserService.UserId
                     && o.Status == OrderStatus.Completed,
                cancellationToken);

        if (order is null || order.Items.All(i => i.ProductId != request.ProductId))
        {
            return Result.Failure<ProductReviewDto>(CommerceErrors.ReviewNotVerifiedPurchase);
        }

        var review = ProductReview.Create(
            _currentUserService.UserId,
            request.ProductId,
            request.OrderId,
            request.Rating,
            request.Comment);

        _commerceDbContext.ProductReviews.Add(review);
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(AccountMapper.ToProductReviewDto(review));
    }
}
