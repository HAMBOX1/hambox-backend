using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Memberships.Models;
using HAMBOX.Modules.Commerce.Application.Promotions.Models;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Promotions;

/// <summary>
/// Builds promotion evaluation context from cart state.
/// </summary>
internal static class PromotionContextFactory
{
    public static IReadOnlyList<PromotionCartLine> BuildLines(
        IEnumerable<CartItem> items,
        IReadOnlyDictionary<Guid, Product> products) =>
        items.Select(item =>
        {
            products.TryGetValue(item.ProductId, out var product);
            return new PromotionCartLine(
                item.ProductId,
                product?.CategoryId,
                item.Quantity,
                item.UnitPrice);
        }).ToList();

    public static async Task<PromotionEvaluationContext> CreateAsync(
        ICommerceDbContext dbContext,
        IMembershipEngine membershipEngine,
        ShoppingCart cart,
        IReadOnlyDictionary<Guid, Product> products,
        bool isAuthenticated,
        string? userId,
        string? countryCode,
        CancellationToken cancellationToken)
    {
        var isFirstPurchase = userId is null
            ? true
            : !await dbContext.Orders.AnyAsync(o => o.UserId == userId, cancellationToken);

        var membership = isAuthenticated && userId is not null
            ? await membershipEngine.ResolveAsync(userId, cancellationToken)
            : MembershipSnapshot.None;

        return new PromotionEvaluationContext(
            BuildLines(cart.Items, products),
            userId,
            countryCode,
            isAuthenticated,
            isFirstPurchase,
            cart.AppliedCouponCode,
            membership,
            DateTime.UtcNow);
    }
}
