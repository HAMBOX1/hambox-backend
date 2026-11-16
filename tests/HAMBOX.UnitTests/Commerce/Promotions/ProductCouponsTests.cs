using HAMBOX.Modules.Commerce.Application.Features.Promotions.ProductCoupons;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Commerce.Promotions;

/// <summary>"With coupon code: X" on a product page: only codes the admin chose to show, running now, for this product.</summary>
public sealed class ProductCouponsTests
{
    private static Promotion Code(string code, bool show, bool publish = true, DateTime? ends = null, params (PromotionTargetType Type, Guid Id)[] targets)
    {
        var promotion = Promotion.Create("Promo " + code, null, PromotionType.CouponCode, DiscountType.Percentage, 20m, null, ends);
        promotion.AddCouponCode(code, false, null, null, null);
        foreach (var (type, id) in targets)
        {
            promotion.AddTarget(type, id);
        }

        promotion.SetShowOnProductPages(show);
        if (publish)
        {
            promotion.Publish();
        }

        return promotion;
    }

    private static async Task<IReadOnlyList<ProductCouponDto>> QueryAsync(IEnumerable<Promotion> promotions, Guid productId, Guid? categoryId = null)
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        db.Promotions.AddRange(promotions);
        await db.SaveChangesAsync();
        var result = await new GetProductCouponsQueryHandler(db).Handle(new GetProductCouponsQuery(productId, categoryId), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact]
    public async Task ShowsACodeThatTargetsTheProduct_WithItsDiscount()
    {
        var product = Guid.NewGuid();

        var shown = await QueryAsync([Code("IAMSMART2", true, targets: (PromotionTargetType.Product, product))], product);

        var coupon = Assert.Single(shown);
        Assert.Equal("IAMSMART2", coupon.Code);
        Assert.Equal("Percentage", coupon.DiscountType);
        Assert.Equal(20m, coupon.DiscountValue);
    }

    [Fact]
    public async Task NeverShowsACodeTheAdminDidNotChooseToShow()
    {
        var product = Guid.NewGuid();

        var shown = await QueryAsync([Code("PRIVATE10", false, targets: (PromotionTargetType.Product, product))], product);

        Assert.Empty(shown);
    }

    [Fact]
    public async Task LeavesOutCodesForOtherProductsAndUnpublishedOrEndedOnes()
    {
        var product = Guid.NewGuid();
        var other = Guid.NewGuid();

        var shown = await QueryAsync(
            [
                Code("OTHER", true, targets: (PromotionTargetType.Product, other)),
                Code("DRAFT", true, publish: false, targets: (PromotionTargetType.Product, product)),
                Code("ENDED", true, ends: DateTime.UtcNow.AddDays(-1), targets: (PromotionTargetType.Product, product)),
            ],
            product);

        Assert.Empty(shown);
    }

    [Fact]
    public async Task ACategoryTargetMatchesProductsInThatCategory_AndAStoreWideCodeMatchesEveryProduct()
    {
        var product = Guid.NewGuid();
        var category = Guid.NewGuid();

        var shown = await QueryAsync(
            [
                Code("CATEGORY5", true, targets: (PromotionTargetType.Category, category)),
                Code("EVERYTHING", true),
            ],
            product,
            category);

        Assert.Equal(["CATEGORY5", "EVERYTHING"], shown.Select(c => c.Code).Order().ToArray());
    }
}
