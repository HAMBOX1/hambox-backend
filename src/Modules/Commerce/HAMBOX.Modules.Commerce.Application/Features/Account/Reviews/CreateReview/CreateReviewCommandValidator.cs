using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.CreateReview;

/// <summary>
/// Refuses a bad rating/comment as a 400 up front; the domain guards behind them throw, which would surface as a 500.
/// </summary>
public sealed class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).NotEmpty().MaximumLength(2000);
    }
}
