using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.DeleteReview;

public sealed record DeleteReviewCommand(Guid ReviewId) : IRequest<Result>;

internal sealed class DeleteReviewCommandHandler : IRequestHandler<DeleteReviewCommand, Result>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public DeleteReviewCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(DeleteReviewCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure(CommerceErrors.AuthenticationRequired);
        }

        var review = await _commerceDbContext.ProductReviews
            .FirstOrDefaultAsync(
                r => r.Id == request.ReviewId && r.UserId == _currentUserService.UserId,
                cancellationToken);

        if (review is null)
        {
            return Result.Failure(CommerceErrors.ReviewNotFound);
        }

        _commerceDbContext.ProductReviews.Remove(review);
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
