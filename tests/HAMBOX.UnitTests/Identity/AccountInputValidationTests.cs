using HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.CreateAlertSubscription;
using HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.CreateReview;
using HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.UpdateReview;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Identity.Application.Features.UpdateProfile;

namespace HAMBOX.UnitTests.Identity;

/// <summary>
/// The customer-facing inputs that used to reach the domain unchecked (a 500) or accept junk: profile text, review
/// rating/comment and the alert type.
/// </summary>
public sealed class AccountInputValidationTests
{
    private static bool ProfileIsValid(string first, string last, string? phone) =>
        new UpdateProfileCommandValidator().Validate(new UpdateProfileCommand(first, last, phone, "en", "USD")).IsValid;

    [Theory]
    [InlineData("Ahmed", "Ali", null, true)]
    [InlineData("أحمد", "محمد", "", true)]
    [InlineData("Test", "User", "+20 100 000 0002", true)]
    [InlineData("Test", "User", "(555) 123-4567", true)]
    [InlineData("<b>x</b>", "User", null, false)]
    [InlineData("Test", "a>b", null, false)]
    [InlineData("Test", "User", "abc-not-a-phone", false)]
    [InlineData("Test", "User", "12345", false)]
    [InlineData("", "User", null, false)]
    public void Profile_NamesWithoutMarkup_AndRealPhoneNumbers_AreRequired(string first, string last, string? phone, bool valid) =>
        Assert.Equal(valid, ProfileIsValid(first, last, phone));

    [Theory]
    [InlineData(1, "ok", true)]
    [InlineData(5, "ok", true)]
    [InlineData(0, "ok", false)]
    [InlineData(6, "ok", false)]
    [InlineData(-1, "ok", false)]
    [InlineData(5, "", false)]
    [InlineData(5, "   ", false)]
    public void Review_RatingAndComment_AreCheckedBeforeTheDomainThrows(int rating, string comment, bool valid)
    {
        Assert.Equal(valid, new CreateReviewCommandValidator().Validate(new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), rating, comment)).IsValid);
        Assert.Equal(valid, new UpdateReviewCommandValidator().Validate(new UpdateReviewCommand(Guid.NewGuid(), rating, comment)).IsValid);
    }

    [Fact]
    public void Review_CommentOver2000Characters_IsRejected()
    {
        var tooLong = new string('a', 2001);

        Assert.False(new CreateReviewCommandValidator().Validate(new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 5, tooLong)).IsValid);
        Assert.True(new CreateReviewCommandValidator().Validate(new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 5, new string('a', 2000))).IsValid);
    }

    [Theory]
    [InlineData(CustomerAlertType.BackInStock, true)]
    [InlineData(CustomerAlertType.PriceDrop, true)]
    [InlineData((CustomerAlertType)99, false)]
    public void Alert_TypeMustBeADefinedValue(CustomerAlertType type, bool valid) =>
        Assert.Equal(valid, new CreateAlertSubscriptionCommandValidator().Validate(new CreateAlertSubscriptionCommand(Guid.NewGuid(), type, null)).IsValid);
}
