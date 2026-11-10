using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.UpdateReview;

/// <summary>Same limits as creating a review, so an edit can never hit the domain guards and fail with a 500.</summary>
public sealed class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).NotEmpty().MaximumLength(2000);
    }
}
