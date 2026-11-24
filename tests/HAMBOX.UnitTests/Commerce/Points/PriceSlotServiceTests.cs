using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Memberships.Models;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Points;

public sealed class PriceSlotServiceTests
{
    private sealed class SlotEngine(string? slots) : IMembershipEngine
    {
        public Task<MembershipSnapshot> ResolveAsync(string? userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(MembershipSnapshot.None with
            {
                SubscriptionId = Guid.NewGuid(),
                PlanId = Guid.NewGuid(),
                Benefits = slots is null ? [] : [new ResolvedMembershipBenefitDto("PriceSlots", slots, "Price slots")],
            });

        public Task ProcessExpirationsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private const string User = "u1";

    private readonly (Modules.Commerce.Infrastructure.Persistence.CommerceDbContext Commerce, Modules.Catalog.Infrastructure.Persistence.CatalogDbContext Catalog) _db =
        CommerceTestDbContextFactory.Create();

    private PriceSlotService Service(string? slots) => new(_db.Commerce, _db.Catalog, new SlotEngine(slots));

    private async Task<Order> OrderAsync(ProductVariant variant, int quantity, decimal unitPrice, bool failed = false)
    {
        var order = Order.Create(
            User, $"ORD-{Guid.NewGuid():N}", "b@example.com", "US", "Development", unitPrice * quantity, 0m, 0m, unitPrice * quantity,
            [(variant.ProductId, "Game", quantity, unitPrice, (Guid?)variant.Id, (string?)variant.Sku, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);
        if (failed)
        {
            order.MarkFailed();
        }

        _db.Commerce.Orders.Add(order);
        await _db.Commerce.SaveChangesAsync();
        return order;
    }

    private async Task<ProductVariant> VariantAsync()
    {
        var variant = ProductVariant.Create(Guid.NewGuid(), "SKU-1", null, 10m);
        variant.SetMemberPrice(7m);
        _db.Catalog.ProductVariants.Add(variant);
        await _db.Catalog.SaveChangesAsync();
        return variant;
    }

    [Fact]
    public async Task A_plan_without_the_slot_benefit_has_no_limit()
    {
        Assert.Null(await Service(null).GetRemainingAsync(User, default));
    }

    [Fact]
    public async Task Slots_are_used_by_member_priced_units_and_freed_by_failed_orders()
    {
        var variant = await VariantAsync();
        var service = Service("5");
        Assert.Equal(5, await service.GetRemainingAsync(User, default));

        var order = await OrderAsync(variant, 3, 7m);
        await service.RecordAsync(order, default);
        await _db.Commerce.SaveChangesAsync();
        Assert.Equal(2, await service.GetRemainingAsync(User, default));

        var normal = await OrderAsync(variant, 2, 10m);
        await service.RecordAsync(normal, default);
        await _db.Commerce.SaveChangesAsync();
        Assert.Equal(2, await service.GetRemainingAsync(User, default));

        order.MarkFailed();
        await _db.Commerce.SaveChangesAsync();
        Assert.Equal(5, await service.GetRemainingAsync(User, default));
    }
}
