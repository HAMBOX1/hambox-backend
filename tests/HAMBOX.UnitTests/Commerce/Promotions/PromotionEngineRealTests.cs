using HAMBOX.Modules.Commerce.Application.Memberships.Models;
using HAMBOX.Modules.Commerce.Application.Promotions;
using HAMBOX.Modules.Commerce.Application.Promotions.Evaluators;
using HAMBOX.Modules.Commerce.Application.Promotions.Models;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Commerce.Promotions;

/// <summary>
/// The REAL promotion engine with the REAL evaluators and a real (in-memory) database — no engine fakes — covering
/// the audit R12 items: a discount that expires after N redemptions, per-product discounts and promo codes, and the
/// limits and windows around them.
/// </summary>
public sealed class PromotionEngineRealTests
{
    private static readonly Guid ProductA = Guid.NewGuid();
    private static readonly Guid ProductB = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private static PromotionEngine CreateEngine(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext db) =>
        new(
            db,
            [
                new AutomaticPromotionEvaluator(),
                new MembershipPromotionEvaluator(),
                new CategoryPromotionEvaluator(),
                new ProductPromotionEvaluator(),
                new FlashSalePromotionEvaluator(),
                new CouponPromotionEvaluator(),
            ],
            new FakeCommerceSettingsProvider());

    private static PromotionEvaluationContext Cart(string? userId = "user-1", string? coupon = null, DateTime? at = null) =>
        new(
            [
                new PromotionCartLine(ProductA, null, 1, 50m),
                new PromotionCartLine(ProductB, null, 1, 30m),
            ],
            userId,
            "US",
            IsAuthenticated: userId is not null,
            IsFirstPurchase: false,
            coupon,
            MembershipSnapshot.None,
            at ?? Now,
            TaxRateOverridePercent: 0m);

    private static Promotion ProductDiscount(decimal percent = 20m, Guid? product = null, string? usageLimit = null)
    {
        var promotion = Promotion.Create("Product deal", null, PromotionType.Product, DiscountType.Percentage, percent);
        promotion.AddTarget(PromotionTargetType.Product, product ?? ProductA);
        if (usageLimit is not null)
        {
            promotion.AddCondition(PromotionConditionType.UsageLimit, usageLimit);
        }

        promotion.Publish();
        return promotion;
    }

    private static async Task SaveAsync(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext db, params Promotion[] promotions)
    {
        db.Promotions.AddRange(promotions);
        await db.SaveChangesAsync();
    }

    private static PromotionRedemption Redemption(Promotion promotion, string userId) =>
        PromotionRedemption.Create(promotion.Id, null, userId, Guid.NewGuid(), 10m, promotion.Name);

    [Fact]
    public async Task ProductDiscount_OnlyDiscountsTheTargetedProduct()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        await SaveAsync(db, ProductDiscount(20m, ProductA));

        var result = await CreateEngine(db).EvaluateAsync(Cart());

        Assert.Equal(80m, result.Subtotal);
        Assert.Equal(10m, result.TotalDiscount); // 20% of the 50.00 line only, not of the whole 80.00 cart
        Assert.Equal(70m, result.Total);
        Assert.Single(result.AppliedPromotions);
    }

    [Fact]
    public async Task QuantityLimitedDiscount_StopsApplyingOnceItsRedemptionsAreUsedUp()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var promotion = ProductDiscount(20m, ProductA, usageLimit: "2");
        await SaveAsync(db, promotion);
        var engine = CreateEngine(db);

        var first = await engine.EvaluateAsync(Cart());
        Assert.Equal(10m, first.TotalDiscount);

        db.PromotionRedemptions.Add(Redemption(promotion, "someone-else"));
        await db.SaveChangesAsync();
        var second = await engine.EvaluateAsync(Cart());
        Assert.Equal(10m, second.TotalDiscount); // 1 of 2 used — still on

        db.PromotionRedemptions.Add(Redemption(promotion, "another-one"));
        await db.SaveChangesAsync();
        var third = await engine.EvaluateAsync(Cart());
        Assert.Equal(0m, third.TotalDiscount); // 2 of 2 used — the discount has expired
        Assert.Empty(third.AppliedPromotions);
    }

    [Fact]
    public async Task PerUserLimit_StopsThatCustomerOnly()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var promotion = ProductDiscount(20m, ProductA);
        promotion.AddCondition(PromotionConditionType.PerUserLimit, "1");
        await SaveAsync(db, promotion);
        db.PromotionRedemptions.Add(Redemption(promotion, "user-1"));
        await db.SaveChangesAsync();
        var engine = CreateEngine(db);

        Assert.Equal(0m, (await engine.EvaluateAsync(Cart("user-1"))).TotalDiscount);
        Assert.Equal(10m, (await engine.EvaluateAsync(Cart("user-2"))).TotalDiscount);
    }

    [Fact]
    public async Task PromoCode_ForOneProduct_DiscountsOnlyThatProduct()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var promotion = Promotion.Create("Code for A", null, PromotionType.CouponCode, DiscountType.Percentage, 10m);
        promotion.AddTarget(PromotionTargetType.Product, ProductA);
        promotion.AddCouponCode("ONLYA", isSingleUse: false, maxUses: null, perUserMaxUses: null, expiresOnUtc: null);
        promotion.Publish();
        await SaveAsync(db, promotion);

        var result = await CreateEngine(db).EvaluateAsync(Cart(coupon: "ONLYA"));

        Assert.Empty(result.ValidationErrors);
        Assert.Equal(5m, result.TotalDiscount); // 10% of product A (50.00), product B untouched
        var applied = Assert.Single(result.AppliedPromotions);
        Assert.Equal("ONLYA", applied.CouponCode);
    }

    [Fact]
    public async Task PromoCode_WithoutTargets_DiscountsTheWholeCart()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var promotion = Promotion.Create("Everything", null, PromotionType.CouponCode, DiscountType.Percentage, 10m);
        promotion.AddCouponCode("ALL10", false, null, null, null);
        promotion.Publish();
        await SaveAsync(db, promotion);

        var result = await CreateEngine(db).EvaluateAsync(Cart(coupon: "all10"));

        Assert.Equal(8m, result.TotalDiscount);
    }

    [Fact]
    public async Task UnknownPromoCode_IsReported_AndDiscountsNothing()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        await SaveAsync(db, ProductDiscount());

        var result = await CreateEngine(db).EvaluateAsync(Cart(coupon: "NOPE"));

        Assert.NotEmpty(result.ValidationErrors);
        Assert.Equal(10m, result.TotalDiscount); // only the automatic product deal, never the bogus code
        Assert.DoesNotContain(result.AppliedPromotions, a => a.CouponCode is not null);
    }

    [Fact]
    public async Task FixedAmountDiscount_NeverTakesMoreThanTheTargetedLines()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var promotion = Promotion.Create("Big voucher", null, PromotionType.Product, DiscountType.FixedAmount, 500m);
        promotion.AddTarget(PromotionTargetType.Product, ProductB);
        promotion.Publish();
        await SaveAsync(db, promotion);

        var result = await CreateEngine(db).EvaluateAsync(Cart());

        Assert.Equal(30m, result.TotalDiscount);
        Assert.True(result.Total >= 50m);
    }

    [Fact]
    public async Task MinimumOrderAmount_IsEnforced()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var promotion = ProductDiscount(20m, ProductA);
        promotion.AddCondition(PromotionConditionType.MinOrderAmount, "100");
        await SaveAsync(db, promotion);

        var result = await CreateEngine(db).EvaluateAsync(Cart());

        Assert.Equal(0m, result.TotalDiscount); // 80.00 cart is under the 100.00 minimum
    }

    [Fact]
    public async Task ExpiredAndNotYetStartedPromotions_DoNotApply()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var ended = Promotion.Create("Ended", null, PromotionType.Product, DiscountType.Percentage, 20m,
            startDateUtc: Now.AddDays(-10), endDateUtc: Now.AddDays(-1));
        ended.AddTarget(PromotionTargetType.Product, ProductA);
        ended.Publish();
        var future = Promotion.Create("Future", null, PromotionType.Product, DiscountType.Percentage, 20m,
            startDateUtc: Now.AddDays(1), endDateUtc: Now.AddDays(10));
        future.AddTarget(PromotionTargetType.Product, ProductA);
        future.Publish();
        await SaveAsync(db, ended, future);

        var result = await CreateEngine(db).EvaluateAsync(Cart());

        Assert.Equal(0m, result.TotalDiscount);
    }

    [Fact]
    public async Task UnpublishedPromotion_NeverApplies()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var draft = Promotion.Create("Draft", null, PromotionType.Product, DiscountType.Percentage, 20m);
        draft.AddTarget(PromotionTargetType.Product, ProductA);
        await SaveAsync(db, draft);

        var result = await CreateEngine(db).EvaluateAsync(Cart());

        Assert.Equal(0m, result.TotalDiscount);
    }
}
