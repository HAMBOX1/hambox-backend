using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.GetProductReviews;

public sealed record GetProductReviewsQuery(Guid ProductId) : IRequest<Result<IReadOnlyList<ProductReviewDto>>>;

internal sealed class GetProductReviewsQueryHandler(
    ICommerceDbContext commerceDbContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetProductReviewsQuery, Result<IReadOnlyList<ProductReviewDto>>>
{
    public async Task<Result<IReadOnlyList<ProductReviewDto>>> Handle(
        GetProductReviewsQuery request,
        CancellationToken cancellationToken)
    {
        // Hidden (moderated) reviews never appear on the storefront.
        var reviews = await commerceDbContext.ProductReviews
            .Where(r => r.ProductId == request.ProductId && !r.IsHidden)
            .OrderByDescending(r => r.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        var ids = reviews.Select(r => r.Id).ToList();
        var votes = await commerceDbContext.ReviewVotes
            .Where(v => ids.Contains(v.ReviewId))
            .ToListAsync(cancellationToken);

        var me = currentUser.IsAuthenticated ? currentUser.UserId : null;

        var dtos = reviews.Select(r =>
        {
            var own = votes.Where(v => v.ReviewId == r.Id).ToList();
            var mine = me is null ? null : own.FirstOrDefault(v => v.UserId == me);
            return AccountMapper.ToProductReviewDto(r) with
            {
                // Other shoppers' identifiers are not part of the public page.
                UserId = r.UserId == me ? r.UserId : string.Empty,
                HelpfulCount = own.Count(v => v.IsHelpful),
                NotHelpfulCount = own.Count(v => !v.IsHelpful),
                MyVote = mine is null ? null : mine.IsHelpful ? "helpful" : "not_helpful",
            };
        }).ToList();

        return Result.Success<IReadOnlyList<ProductReviewDto>>(dtos);
    }
}
