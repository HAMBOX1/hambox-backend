using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>
/// One shopper's "helpful" / "not helpful" vote on a review. At most one per (review, user): voting again
/// changes the vote, it never adds another, so counts cannot be inflated by repeating the request.
/// </summary>
public sealed class ReviewVote : Entity
{
    private ReviewVote()
    {
    }

    private ReviewVote(Guid id, Guid reviewId, string userId, bool isHelpful)
        : base(id)
    {
        ReviewId = reviewId;
        UserId = userId;
        IsHelpful = isHelpful;
    }

    public Guid ReviewId { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public bool IsHelpful { get; private set; }

    public static ReviewVote Create(Guid reviewId, string userId, bool isHelpful)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return new ReviewVote(Guid.NewGuid(), reviewId, userId, isHelpful);
    }

    public void Change(bool isHelpful) => IsHelpful = isHelpful;
}
