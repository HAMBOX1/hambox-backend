using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.Admin;

/// <summary>Moderator list of reviews. <paramref name="Hidden"/>: null = all, true = only hidden, false = only visible.</summary>
public sealed record GetAdminReviewsQuery(bool? Hidden, Guid? ProductId, int Take = 100)
    : IRequest<Result<IReadOnlyList<AdminReviewDto>>>;

public sealed record HideReviewCommand(Guid ReviewId, string Reason) : IRequest<Result<AdminReviewDto>>;

public sealed record UnhideReviewCommand(Guid ReviewId) : IRequest<Result<AdminReviewDto>>;

internal static class AdminReviewProjection
{
    public static async Task<AdminReviewDto?> LoadAsync(ICommerceDbContext db, Guid reviewId, CancellationToken ct)
    {
        var r = await db.ProductReviews.FirstOrDefaultAsync(x => x.Id == reviewId, ct);
        if (r is null)
        {
            return null;
        }

        var votes = await db.ReviewVotes.Where(v => v.ReviewId == reviewId).ToListAsync(ct);
        return new AdminReviewDto(
            r.Id, r.ProductId, r.UserId, r.OrderId, r.Rating, r.Comment,
            votes.Count(v => v.IsHelpful), votes.Count(v => !v.IsHelpful),
            r.IsHidden, r.HiddenReason, r.HiddenByUserId, r.HiddenOnUtc, r.CreatedOnUtc);
    }
}

internal sealed class GetAdminReviewsQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetAdminReviewsQuery, Result<IReadOnlyList<AdminReviewDto>>>
{
    public async Task<Result<IReadOnlyList<AdminReviewDto>>> Handle(GetAdminReviewsQuery request, CancellationToken cancellationToken)
    {
        var query = db.ProductReviews.AsQueryable();
        if (request.Hidden is bool hidden)
        {
            query = query.Where(r => r.IsHidden == hidden);
        }

        if (request.ProductId is Guid productId)
        {
            query = query.Where(r => r.ProductId == productId);
        }

        var reviews = await query
            .OrderByDescending(r => r.CreatedOnUtc)
            .Take(Math.Clamp(request.Take, 1, 200))
            .ToListAsync(cancellationToken);

        var ids = reviews.Select(r => r.Id).ToList();
        var votes = await db.ReviewVotes.Where(v => ids.Contains(v.ReviewId)).ToListAsync(cancellationToken);

        IReadOnlyList<AdminReviewDto> dtos = reviews.Select(r =>
        {
            var own = votes.Where(v => v.ReviewId == r.Id).ToList();
            return new AdminReviewDto(
                r.Id, r.ProductId, r.UserId, r.OrderId, r.Rating, r.Comment,
                own.Count(v => v.IsHelpful), own.Count(v => !v.IsHelpful),
                r.IsHidden, r.HiddenReason, r.HiddenByUserId, r.HiddenOnUtc, r.CreatedOnUtc);
        }).ToList();

        return Result.Success(dtos);
    }
}

internal sealed class HideReviewCommandHandler(ICommerceDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<HideReviewCommand, Result<AdminReviewDto>>
{
    public async Task<Result<AdminReviewDto>> Handle(HideReviewCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Failure<AdminReviewDto>(CommerceErrors.ActionReasonRequired);
        }

        var review = await db.ProductReviews.FirstOrDefaultAsync(r => r.Id == request.ReviewId, cancellationToken);
        if (review is null)
        {
            return Result.Failure<AdminReviewDto>(CommerceErrors.ReviewNotFound);
        }

        review.Hide(currentUser.UserId ?? "system", request.Reason.Trim());
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success((await AdminReviewProjection.LoadAsync(db, review.Id, cancellationToken))!);
    }
}

internal sealed class UnhideReviewCommandHandler(ICommerceDbContext db)
    : IRequestHandler<UnhideReviewCommand, Result<AdminReviewDto>>
{
    public async Task<Result<AdminReviewDto>> Handle(UnhideReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await db.ProductReviews.FirstOrDefaultAsync(r => r.Id == request.ReviewId, cancellationToken);
        if (review is null)
        {
            return Result.Failure<AdminReviewDto>(CommerceErrors.ReviewNotFound);
        }

        review.Unhide();
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success((await AdminReviewProjection.LoadAsync(db, review.Id, cancellationToken))!);
    }
}
