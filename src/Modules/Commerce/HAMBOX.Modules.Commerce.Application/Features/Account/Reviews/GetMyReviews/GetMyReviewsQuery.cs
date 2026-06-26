using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.GetMyReviews;

public sealed record GetMyReviewsQuery() : IRequest<Result<IReadOnlyList<ProductReviewDto>>>;

internal sealed class GetMyReviewsQueryHandler : IRequestHandler<GetMyReviewsQuery, Result<IReadOnlyList<ProductReviewDto>>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetMyReviewsQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<ProductReviewDto>>> Handle(
        GetMyReviewsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<IReadOnlyList<ProductReviewDto>>(CommerceErrors.AuthenticationRequired);
        }

        var reviews = await _commerceDbContext.ProductReviews
            .Where(r => r.UserId == _currentUserService.UserId)
            .OrderByDescending(r => r.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ProductReviewDto>>(
            reviews.Select(AccountMapper.ToProductReviewDto).ToList());
    }
}
