using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Promotions.AddPromotionTargets;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Promotions;

/// <summary>The product page's "Add promo" / bulk "Apply promo code": attach products or categories to an existing code.</summary>
public sealed class AddPromotionTargetsTests
{
    private static async Task<(AddPromotionTargetsCommandHandler Handler, HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext Db, Promotion Promotion)> SetupAsync()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var promotion = Promotion.Create("Summer code", null, PromotionType.CouponCode, DiscountType.Percentage, 10m);
        promotion.AddCouponCode("SUMMER10", false, null, null, null);
        promotion.Publish();
        db.Promotions.Add(promotion);
        await db.SaveChangesAsync();
        return (new AddPromotionTargetsCommandHandler(db, new FakeCurrentUserService("admin-1")), db, promotion);
    }

    private static AddPromotionTargetsCommand Cmd(Guid promotionId, IReadOnlyList<Guid>? products = null, IReadOnlyList<Guid>? categories = null) =>
        new(promotionId, new AddPromotionTargetsRequest(products, categories));

    [Fact]
    public async Task AddsProductsAndCategories_AsTargets_AndReportsTheCounts()
    {
        var (handler, db, promotion) = await SetupAsync();
        var products = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var category = Guid.NewGuid();

        var result = await handler.Handle(Cmd(promotion.Id, products, [category]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.ProductsAdded);
        Assert.Equal(1, result.Value.CategoriesAdded);
        Assert.Equal(0, result.Value.AlreadyTargeted);
        var stored = await db.PromotionTargets.Where(t => t.PromotionId == promotion.Id).ToListAsync();
        Assert.Equal(4, stored.Count);
        Assert.Equal(3, stored.Count(t => t.TargetType == PromotionTargetType.Product));
    }

    [Fact]
    public async Task RepeatingTheSameProducts_AddsNothingNew_SoItIsSafeToClickTwice()
    {
        var (handler, db, promotion) = await SetupAsync();
        var product = Guid.NewGuid();
        await handler.Handle(Cmd(promotion.Id, [product]), CancellationToken.None);

        var again = await handler.Handle(Cmd(promotion.Id, [product, Guid.NewGuid()]), CancellationToken.None);

        Assert.Equal(1, again.Value.ProductsAdded);
        Assert.Equal(1, again.Value.AlreadyTargeted);
        Assert.Equal(2, await db.PromotionTargets.CountAsync(t => t.PromotionId == promotion.Id));
    }

    [Fact]
    public async Task DuplicatesInTheRequest_AreCountedOnce()
    {
        var (handler, db, promotion) = await SetupAsync();
        var product = Guid.NewGuid();

        var result = await handler.Handle(Cmd(promotion.Id, [product, product, product]), CancellationToken.None);

        Assert.Equal(1, result.Value.ProductsAdded);
        Assert.Equal(1, await db.PromotionTargets.CountAsync(t => t.PromotionId == promotion.Id));
    }

    [Fact]
    public async Task NothingToAdd_UnknownPromotion_AndOversizedRequests_AreRefused()
    {
        var (handler, _, promotion) = await SetupAsync();

        var empty = await handler.Handle(Cmd(promotion.Id, [], []), CancellationToken.None);
        var onlyEmptyGuids = await handler.Handle(Cmd(promotion.Id, [Guid.Empty]), CancellationToken.None);
        var unknown = await handler.Handle(Cmd(Guid.NewGuid(), [Guid.NewGuid()]), CancellationToken.None);
        var tooMany = await handler.Handle(
            Cmd(promotion.Id, Enumerable.Range(0, AddPromotionTargetsCommandHandler.MaxTargetsPerRequest + 1).Select(_ => Guid.NewGuid()).ToList()),
            CancellationToken.None);

        Assert.Equal(CommerceErrors.PromotionTargetsEmpty.Code, empty.Error.Code);
        Assert.Equal(CommerceErrors.PromotionTargetsEmpty.Code, onlyEmptyGuids.Error.Code);
        Assert.Equal(CommerceErrors.PromotionNotFound.Code, unknown.Error.Code);
        Assert.Equal(CommerceErrors.PromotionTargetsTooMany.Code, tooMany.Error.Code);
    }

    [Fact]
    public async Task EveryChange_IsWrittenToThePromotionAuditTrail_ButAnUnchangedRepeatIsNot()
    {
        var (handler, db, promotion) = await SetupAsync();
        var product = Guid.NewGuid();

        await handler.Handle(Cmd(promotion.Id, [product]), CancellationToken.None);
        await handler.Handle(Cmd(promotion.Id, [product]), CancellationToken.None);

        var audit = Assert.Single(db.PromotionAuditLogs.Where(l => l.PromotionId == promotion.Id && l.Action == PromotionAuditAction.Updated));
        Assert.Equal("admin-1", audit.PerformedByUserId);
    }

    [Fact]
    public void AddTargetsIfMissing_ReturnsOnlyTheNewOnes()
    {
        var promotion = Promotion.Create("P", null, PromotionType.CouponCode, DiscountType.Percentage, 5m);
        var existing = Guid.NewGuid();
        promotion.AddTarget(PromotionTargetType.Product, existing);

        var added = promotion.AddTargetsIfMissing([
            (PromotionTargetType.Product, existing),
            (PromotionTargetType.Product, Guid.NewGuid()),
            (PromotionTargetType.Category, existing), // same id but a different kind of target counts as new
        ]);

        Assert.Equal(2, added.Count);
        Assert.Equal(3, promotion.Targets.Count);
    }
}
