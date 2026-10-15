using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.VoteOnReview;

/// <summary>Sets (or, with <c>Vote = null</c>, removes) the caller's helpful / not-helpful vote on a review.</summary>
public sealed record VoteOnReviewCommand(Guid ReviewId, string? Vote) : IRequest<Result<ReviewVoteSummaryDto>>;

public sealed record ReviewVoteSummaryDto(Guid ReviewId, int HelpfulCount, int NotHelpfulCount, string? MyVote);

internal sealed class VoteOnReviewCommandHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<VoteOnReviewCommand, Result<ReviewVoteSummaryDto>>
{
    public async Task<Result<ReviewVoteSummaryDto>> Handle(VoteOnReviewCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return Result.Failure<ReviewVoteSummaryDto>(CommerceErrors.AuthenticationRequired);
        }

        if (request.Vote is not (null or "helpful" or "not_helpful"))
        {
            return Result.Failure<ReviewVoteSummaryDto>(CommerceErrors.ReviewVoteInvalid);
        }

        bool? helpful = request.Vote is null ? null : request.Vote == "helpful";

        var userId = currentUser.UserId;
        var review = await db.ProductReviews.FirstOrDefaultAsync(r => r.Id == request.ReviewId && !r.IsHidden, cancellationToken);
        if (review is null)
        {
            return Result.Failure<ReviewVoteSummaryDto>(CommerceErrors.ReviewNotFound);
        }

        if (review.UserId == userId)
        {
            return Result.Failure<ReviewVoteSummaryDto>(CommerceErrors.CannotVoteOnOwnReview);
        }

        var existing = await db.ReviewVotes.FirstOrDefaultAsync(
            v => v.ReviewId == review.Id && v.UserId == userId, cancellationToken);

        if (helpful is null)
        {
            if (existing is not null)
            {
                db.ReviewVotes.Remove(existing);
            }
        }
        else if (existing is null)
        {
            db.ReviewVotes.Add(ReviewVote.Create(review.Id, userId, helpful.Value));
        }
        else
        {
            existing.Change(helpful.Value);
        }

        await db.SaveChangesAsync(cancellationToken);

        var all = await db.ReviewVotes.Where(v => v.ReviewId == review.Id).ToListAsync(cancellationToken);
        var mine = all.FirstOrDefault(v => v.UserId == userId);
        return Result.Success(new ReviewVoteSummaryDto(
            review.Id,
            all.Count(v => v.IsHelpful),
            all.Count(v => !v.IsHelpful),
            mine is null ? null : mine.IsHelpful ? "helpful" : "not_helpful"));
    }
}
