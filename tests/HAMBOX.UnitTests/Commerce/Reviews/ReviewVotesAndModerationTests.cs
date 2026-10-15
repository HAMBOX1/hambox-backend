using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.Admin;
using HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.GetProductReviews;
using HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.VoteOnReview;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Reviews;

/// <summary>Audit R50: helpful / not-helpful votes (one per shopper) and admin hide / unhide of reviews.</summary>
public sealed class ReviewVotesAndModerationTests
{
    private static readonly Guid ProductId = Guid.NewGuid();

    private static async Task<ProductReview> SeedReviewAsync(CommerceDbContext db, string author = "author-1")
    {
        var review = ProductReview.Create(author, ProductId, Guid.NewGuid(), 5, "Great code, instant delivery");
        db.ProductReviews.Add(review);
        await db.SaveChangesAsync();
        return review;
    }

    private static Task<HAMBOX.SharedKernel.Results.Result<ReviewVoteSummaryDto>> VoteAsync(
        CommerceDbContext db, string userId, Guid reviewId, string? vote) =>
        new VoteOnReviewCommandHandler(db, new FakeCurrentUserService(userId))
            .Handle(new VoteOnReviewCommand(reviewId, vote), CancellationToken.None);

    [Fact]
    public async Task AVote_IsCounted_AndRepeatingItNeverInflatesTheCount()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var review = await SeedReviewAsync(db);

        await VoteAsync(db, "buyer-1", review.Id, "helpful");
        var again = await VoteAsync(db, "buyer-1", review.Id, "helpful");

        Assert.Equal(1, again.Value.HelpfulCount);
        Assert.Equal(0, again.Value.NotHelpfulCount);
        Assert.Equal("helpful", again.Value.MyVote);
        Assert.Equal(1, await db.ReviewVotes.CountAsync());
    }

    [Fact]
    public async Task ChangingAVote_MovesItBetweenTheCounts()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var review = await SeedReviewAsync(db);
        await VoteAsync(db, "buyer-1", review.Id, "helpful");
        await VoteAsync(db, "buyer-2", review.Id, "helpful");

        var changed = await VoteAsync(db, "buyer-1", review.Id, "not_helpful");

        Assert.Equal(1, changed.Value.HelpfulCount);
        Assert.Equal(1, changed.Value.NotHelpfulCount);
        Assert.Equal("not_helpful", changed.Value.MyVote);
    }

    [Fact]
    public async Task RemovingAVote_TakesItBack()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var review = await SeedReviewAsync(db);
        await VoteAsync(db, "buyer-1", review.Id, "helpful");

        var removed = await VoteAsync(db, "buyer-1", review.Id, null);

        Assert.Equal(0, removed.Value.HelpfulCount);
        Assert.Null(removed.Value.MyVote);
    }

    [Fact]
    public async Task TheAuthor_CannotVoteOnTheirOwnReview()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var review = await SeedReviewAsync(db);

        var result = await VoteAsync(db, "author-1", review.Id, "helpful");

        Assert.Equal(CommerceErrors.CannotVoteOnOwnReview.Code, result.Error.Code);
    }

    [Fact]
    public async Task AnAnonymousVisitor_CannotVote_AndAnUnknownVoteIsRefused()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var review = await SeedReviewAsync(db);

        var anonymous = await new VoteOnReviewCommandHandler(db, new FakeCurrentUserService(null))
            .Handle(new VoteOnReviewCommand(review.Id, "helpful"), CancellationToken.None);
        var bogus = await VoteAsync(db, "buyer-1", review.Id, "love_it");

        Assert.Equal(CommerceErrors.AuthenticationRequired.Code, anonymous.Error.Code);
        Assert.Equal(CommerceErrors.ReviewVoteInvalid.Code, bogus.Error.Code);
    }

    [Fact]
    public async Task HidingNeedsAReason_AndHiddenReviewsLeaveTheStorefront()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var review = await SeedReviewAsync(db);
        var hide = new HideReviewCommandHandler(db, new FakeCurrentUserService("admin-1"));
        var storefront = new GetProductReviewsQueryHandler(db, new FakeCurrentUserService("shopper-1"));

        var noReason = await hide.Handle(new HideReviewCommand(review.Id, " "), CancellationToken.None);
        Assert.Equal(CommerceErrors.ActionReasonRequired.Code, noReason.Error.Code);
        Assert.Single((await storefront.Handle(new GetProductReviewsQuery(ProductId), CancellationToken.None)).Value);

        var hidden = await hide.Handle(new HideReviewCommand(review.Id, "Contains a phone number"), CancellationToken.None);
        Assert.True(hidden.IsSuccess);
        Assert.True(hidden.Value.IsHidden);
        Assert.Equal("admin-1", hidden.Value.HiddenByUserId);
        Assert.Empty((await storefront.Handle(new GetProductReviewsQuery(ProductId), CancellationToken.None)).Value);

        // A hidden review cannot be voted on, but moderators still see it.
        var vote = await VoteAsync(db, "buyer-1", review.Id, "helpful");
        Assert.Equal(CommerceErrors.ReviewNotFound.Code, vote.Error.Code);
        var adminList = await new GetAdminReviewsQueryHandler(db)
            .Handle(new GetAdminReviewsQuery(Hidden: true, ProductId: null), CancellationToken.None);
        Assert.Single(adminList.Value);

        var restored = await new UnhideReviewCommandHandler(db)
            .Handle(new UnhideReviewCommand(review.Id), CancellationToken.None);
        Assert.False(restored.Value.IsHidden);
        Assert.Single((await storefront.Handle(new GetProductReviewsQuery(ProductId), CancellationToken.None)).Value);
    }

    [Fact]
    public async Task TheStorefront_ShowsCounts_AndMyVote_ButNotOtherShoppersIds()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var review = await SeedReviewAsync(db);
        await VoteAsync(db, "buyer-1", review.Id, "helpful");
        await VoteAsync(db, "buyer-2", review.Id, "not_helpful");

        var asBuyer = await new GetProductReviewsQueryHandler(db, new FakeCurrentUserService("buyer-1"))
            .Handle(new GetProductReviewsQuery(ProductId), CancellationToken.None);

        var dto = Assert.Single(asBuyer.Value);
        Assert.Equal(1, dto.HelpfulCount);
        Assert.Equal(1, dto.NotHelpfulCount);
        Assert.Equal("helpful", dto.MyVote);
        Assert.Equal(string.Empty, dto.UserId);
    }
}
