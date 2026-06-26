using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.UpdateReview;

public sealed record UpdateReviewCommand(Guid ReviewId, int Rating, string Comment) : IRequest<Result<ProductReviewDto>>;

internal sealed class UpdateReviewCommandHandler : IRequestHandler<UpdateReviewCommand, Result<ProductReviewDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public UpdateReviewCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ProductReviewDto>> Handle(
        UpdateReviewCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<ProductReviewDto>(CommerceErrors.AuthenticationRequired);
        }

        var review = await _commerceDbContext.ProductReviews
            .FirstOrDefaultAsync(
                r => r.Id == request.ReviewId && r.UserId == _currentUserService.UserId,
                cancellationToken);

        if (review is null)
        {
            return Result.Failure<ProductReviewDto>(CommerceErrors.ReviewNotFound);
        }

        review.Update(request.Rating, request.Comment);
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(AccountMapper.ToProductReviewDto(review));
    }
}
