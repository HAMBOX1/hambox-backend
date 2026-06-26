using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.GetProductReviews;

public sealed record GetProductReviewsQuery(Guid ProductId) : IRequest<Result<IReadOnlyList<ProductReviewDto>>>;

internal sealed class GetProductReviewsQueryHandler
    : IRequestHandler<GetProductReviewsQuery, Result<IReadOnlyList<ProductReviewDto>>>
{
    private readonly ICommerceDbContext _commerceDbContext;

    public GetProductReviewsQueryHandler(ICommerceDbContext commerceDbContext)
    {
        _commerceDbContext = commerceDbContext;
    }

    public async Task<Result<IReadOnlyList<ProductReviewDto>>> Handle(
        GetProductReviewsQuery request,
        CancellationToken cancellationToken)
    {
        var reviews = await _commerceDbContext.ProductReviews
            .Where(r => r.ProductId == request.ProductId)
            .OrderByDescending(r => r.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ProductReviewDto>>(
            reviews.Select(AccountMapper.ToProductReviewDto).ToList());
    }
}
