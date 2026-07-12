using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Promotions;
using HAMBOX.Modules.Commerce.Application.Promotions.Models;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Carts;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Builds cart DTOs with promotion engine totals.
/// </summary>
internal sealed class CartResponseBuilder
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly IPromotionEngine _promotionEngine;
    private readonly IMembershipEngine _membershipEngine;
    private readonly ICurrentUserService _currentUserService;

    public CartResponseBuilder(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        IPromotionEngine promotionEngine,
        IMembershipEngine membershipEngine,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _promotionEngine = promotionEngine;
        _membershipEngine = membershipEngine;
        _currentUserService = currentUserService;
    }

    public async Task<CartDto> BuildAsync(
        ShoppingCart cart,
        string? guestSessionId,
        string? countryCode,
        CancellationToken cancellationToken)
    {
        var products = await LoadProductsAsync(cart, cancellationToken);
        var variants = await LoadVariantsAsync(cart, cancellationToken);
        var attributes = await LoadVariantAttributesAsync(cart, variants, cancellationToken);
        var context = await PromotionContextFactory.CreateAsync(
            _commerceDbContext,
            _membershipEngine,
            cart,
            products,
            _currentUserService.IsAuthenticated,
            _currentUserService.UserId,
            countryCode,
            cancellationToken);

        var evaluation = await _promotionEngine.EvaluateAsync(context, cancellationToken);
        return CommerceMapper.ToCartDto(cart, guestSessionId, products, variants, evaluation, attributes);
    }

    public async Task<(decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal TotalAmount, PromotionEvaluationResult Evaluation)>
        BuildOrderAmountsAsync(
            ShoppingCart cart,
            string? countryCode,
            CancellationToken cancellationToken)
    {
        var products = await LoadProductsAsync(cart, cancellationToken);
        var context = await PromotionContextFactory.CreateAsync(
            _commerceDbContext,
            _membershipEngine,
            cart,
            products,
            isAuthenticated: true,
            _currentUserService.UserId,
            countryCode,
            cancellationToken);

        var evaluation = await _promotionEngine.EvaluateAsync(context, cancellationToken);
        return (evaluation.Subtotal, evaluation.TotalDiscount, evaluation.Tax, evaluation.Total, evaluation);
    }

    private async Task<IReadOnlyDictionary<Guid, Product>> LoadProductsAsync(
        ShoppingCart cart,
        CancellationToken cancellationToken)
    {
        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, Product>();
        }

        return await _catalogDbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, ProductVariant>> LoadVariantsAsync(
        ShoppingCart cart,
        CancellationToken cancellationToken)
    {
        var variantIds = cart.Items
            .Where(i => i.ProductVariantId.HasValue)
            .Select(i => i.ProductVariantId!.Value)
            .Distinct()
            .ToList();

        if (variantIds.Count == 0)
        {
            return new Dictionary<Guid, ProductVariant>();
        }

        return await _catalogDbContext.ProductVariants
            .Include(v => v.SelectedOptions)
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, (string? Platform, string? Edition, string? Region, string? Summary)>>
        LoadVariantAttributesAsync(
            ShoppingCart cart,
            IReadOnlyDictionary<Guid, ProductVariant> variants,
            CancellationToken cancellationToken)
    {
        if (variants.Count == 0)
        {
            return new Dictionary<Guid, (string?, string?, string?, string?)>();
        }

        var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
        var optionGroups = await _catalogDbContext.ProductOptionGroups
            .AsNoTracking()
            .Include(g => g.Options)
            .Where(g => productIds.Contains(g.ProductId))
            .ToListAsync(cancellationToken);

        var optionToGroupKey = optionGroups
            .SelectMany(g => g.Options.Select(o => new { o.Id, GroupKey = g.Key, g.DisplayName }))
            .ToDictionary(x => x.Id, x => (x.GroupKey, x.DisplayName));

        var optionLabels = optionGroups
            .SelectMany(g => g.Options.Select(o => new { o.Id, o.Label }))
            .ToDictionary(x => x.Id, x => x.Label);

        var result = new Dictionary<Guid, (string?, string?, string?, string?)>();

        foreach (var (variantId, variant) in variants)
        {
            string? platform = null;
            string? edition = null;
            string? region = null;
            var summaryParts = new List<string>();

            foreach (var selected in variant.SelectedOptions)
            {
                if (!optionLabels.TryGetValue(selected.OptionId, out var label))
                {
                    continue;
                }

                summaryParts.Add(label);

                if (!optionToGroupKey.TryGetValue(selected.OptionId, out var group))
                {
                    continue;
                }

                switch (group.GroupKey.ToLowerInvariant())
                {
                    case "platform":
                        platform = label;
                        break;
                    case "edition":
                        edition = label;
                        break;
                    case "region":
                        region = label;
                        break;
                }
            }

            result[variantId] = (
                platform,
                edition,
                region,
                summaryParts.Count > 0 ? string.Join(" · ", summaryParts) : null);
        }

        return result;
    }
}
