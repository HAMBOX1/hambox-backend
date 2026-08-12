using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Library;

public sealed record GetCustomerLibraryQuery(
    int PageNumber,
    int PageSize,
    string? SearchTerm,
    string? DeliveryStatus,
    string? Sort,
    string? Group) : IRequest<Result<PagedResult<CustomerLibraryItemDto>>>;

internal sealed class GetCustomerLibraryQueryHandler
    : IRequestHandler<GetCustomerLibraryQuery, Result<PagedResult<CustomerLibraryItemDto>>>
{
    private static readonly TimeSpan RecentPurchaseWindow = TimeSpan.FromDays(30);

    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetCustomerLibraryQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedResult<CustomerLibraryItemDto>>> Handle(
        GetCustomerLibraryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<PagedResult<CustomerLibraryItemDto>>(CommerceErrors.AuthenticationRequired);
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var recentCutoff = DateTimeOffset.UtcNow.Subtract(RecentPurchaseWindow);

        var licenseRows = await (
            from key in _commerceDbContext.OrderLicenseKeys.AsNoTracking()
            join order in _commerceDbContext.Orders.AsNoTracking() on key.OrderId equals order.Id
            where order.UserId == _currentUserService.UserId
            select new LibraryRow(
                key.Id,
                key.OrderId,
                key.OrderItemId,
                key.ProductId,
                key.ProductVariantId,
                key.LicenseKey,
                order.OrderNumber,
                order.Status,
                order.CreatedOnUtc))
            .ToListAsync(cancellationToken);

        if (licenseRows.Count == 0)
        {
            return Result.Success(new PagedResult<CustomerLibraryItemDto>(
                Array.Empty<CustomerLibraryItemDto>(),
                pageNumber,
                pageSize,
                0));
        }

        var productIds = licenseRows.Select(r => r.ProductId).Distinct().ToList();
        var variantIds = licenseRows
            .Where(r => r.ProductVariantId.HasValue)
            .Select(r => r.ProductVariantId!.Value)
            .Distinct()
            .ToList();

        var products = await _catalogDbContext.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var imageUrls = await ProductPrimaryImageResolver.GetPrimaryImageUrlsAsync(
            _catalogDbContext,
            productIds,
            cancellationToken);

        var publishedInstructionsProductIds = await _catalogDbContext.ProductInstructions
            .AsNoTracking()
            .Where(i => productIds.Contains(i.ProductId) && i.IsPublished)
            .Select(i => i.ProductId)
            .ToHashSetAsync(cancellationToken);

        // Historical purchases must keep showing platform/edition/region even after the variant
        // is archived or soft-deleted — IgnoreQueryFilters() bypasses the global !IsDeleted filter
        // for this lookup only. This never affects storefront purchasability: that's gated
        // separately by Status/IsVisible in the storefront product-configuration query.
        var variants = variantIds.Count > 0
            ? await _catalogDbContext.ProductVariants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(v => v.SelectedOptions)
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, cancellationToken)
            : new Dictionary<Guid, HAMBOX.Modules.Catalog.Domain.Inventory.ProductVariant>();

        var optionGroups = await _catalogDbContext.ProductOptionGroups
            .AsNoTracking()
            .Include(g => g.Options)
            .Where(g => productIds.Contains(g.ProductId))
            .ToListAsync(cancellationToken);

        var optionToGroupKey = optionGroups
            .SelectMany(g => g.Options.Select(o => new { o.Id, GroupKey = g.Key }))
            .ToDictionary(x => x.Id, x => x.GroupKey, comparer: EqualityComparer<Guid>.Default);

        var optionLabels = optionGroups
            .SelectMany(g => g.Options.Select(o => new { o.Id, o.Label }))
            .ToDictionary(x => x.Id, x => x.Label);

        var items = licenseRows.Select(row =>
        {
            products.TryGetValue(row.ProductId, out var product);
            imageUrls.TryGetValue(row.ProductId, out var imageUrl);
            variants.TryGetValue(row.ProductVariantId ?? Guid.Empty, out var variant);

            var (platform, edition, region) = ResolveVariantAttributes(
                variant,
                optionToGroupKey,
                optionLabels);

            var isDelivered = row.OrderStatus == OrderStatus.Completed;

            var deliveryStatus = isDelivered && !string.IsNullOrWhiteSpace(row.LicenseKey)
                ? "Delivered"
                : "Pending";

            var isRecent = row.PurchaseDate >= recentCutoff;
            var supportUrl = $"/support/orders/{row.OrderNumber}";

            return new CustomerLibraryItemDto(
                row.Id,
                row.OrderId,
                row.OrderItemId,
                row.ProductId,
                row.ProductVariantId,
                product?.NameEn ?? "Unknown Product",
                imageUrl,
                platform,
                edition,
                region,
                row.PurchaseDate,
                row.OrderNumber,
                row.OrderStatus.ToString(),
                deliveryStatus,
                AdminOrderMapper.MaskLicenseKey(row.LicenseKey),
                isDelivered && !string.IsNullOrWhiteSpace(row.LicenseKey),
                BuildRedeemInstructions(platform, product?.DescriptionEn),
                null,
                supportUrl,
                isRecent,
                publishedInstructionsProductIds.Contains(row.ProductId));
        }).ToList();

        items = ApplyFilters(items, request.SearchTerm, request.DeliveryStatus, request.Group);
        items = ApplySort(items, request.Sort);

        var totalCount = items.Count;
        var page = items
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result.Success(new PagedResult<CustomerLibraryItemDto>(
            page,
            pageNumber,
            pageSize,
            totalCount));
    }

    private static List<CustomerLibraryItemDto> ApplyFilters(
        List<CustomerLibraryItemDto> items,
        string? searchTerm,
        string? deliveryStatus,
        string? group)
    {
        IEnumerable<CustomerLibraryItemDto> query = items;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(i =>
                i.ProductNameEn.Contains(term, StringComparison.OrdinalIgnoreCase)
                || i.OrderNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (i.Platform?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (i.Edition?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (i.Region?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(deliveryStatus))
        {
            query = query.Where(i =>
                string.Equals(i.DeliveryStatus, deliveryStatus.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(group, "recent", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.IsRecentPurchase);
        }
        else if (string.Equals(group, "older", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => !i.IsRecentPurchase);
        }

        return query.ToList();
    }

    private static List<CustomerLibraryItemDto> ApplySort(List<CustomerLibraryItemDto> items, string? sort)
    {
        return (sort?.ToLowerInvariant()) switch
        {
            "oldest" => items.OrderBy(i => i.PurchaseDate).ToList(),
            "name" => items.OrderBy(i => i.ProductNameEn, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => items.OrderByDescending(i => i.PurchaseDate).ToList(),
        };
    }

    private static (string? Platform, string? Edition, string? Region) ResolveVariantAttributes(
        HAMBOX.Modules.Catalog.Domain.Inventory.ProductVariant? variant,
        IReadOnlyDictionary<Guid, string> optionToGroupKey,
        IReadOnlyDictionary<Guid, string> optionLabels)
    {
        if (variant is null)
        {
            return (null, null, null);
        }

        string? platform = null;
        string? edition = null;
        string? region = null;

        foreach (var selected in variant.SelectedOptions)
        {
            if (!optionToGroupKey.TryGetValue(selected.OptionId, out var groupKey))
            {
                continue;
            }

            if (!optionLabels.TryGetValue(selected.OptionId, out var label))
            {
                continue;
            }

            switch (groupKey.ToLowerInvariant())
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

        return (platform, edition, region);
    }

    private static string? BuildRedeemInstructions(string? platform, string? descriptionEn)
    {
        var guide = ResolvePlatformGuide(platform);
        if (string.IsNullOrWhiteSpace(descriptionEn))
        {
            return guide;
        }

        var trimmed = descriptionEn.Trim();
        var summary = trimmed.Length <= 280 ? trimmed : $"{trimmed[..277]}...";
        return $"{summary}\n\n{guide}";
    }

    private static string ResolvePlatformGuide(string? platform)
    {
        var key = platform?.Trim().ToLowerInvariant() ?? string.Empty;

        if (key.Contains("steam") || key.Contains("pc"))
        {
            return "Steam: Open Steam → Games → Activate a Product on Steam → enter your key.";
        }

        if (key.Contains("playstation") || key.Contains("psn") || key.Contains("ps5") || key.Contains("ps4"))
        {
            return "PlayStation: Settings → Users and Accounts → Redeem Codes → enter your voucher.";
        }

        if (key.Contains("xbox") || key.Contains("microsoft"))
        {
            return "Xbox: Open Microsoft Store / Xbox → Redeem Code → enter your 25-character code.";
        }

        if (key.Contains("nintendo") || key.Contains("switch"))
        {
            return "Nintendo: Nintendo eShop → Enter Code → confirm and download.";
        }

        if (key.Contains("ea") || key.Contains("origin"))
        {
            return "EA: Open EA app → Redeem Product Code → paste your key.";
        }

        return "Open the official storefront for your platform and region, choose Redeem / Activate Code, and enter your key.";
    }

    private sealed record LibraryRow(
        Guid Id,
        Guid OrderId,
        Guid OrderItemId,
        Guid ProductId,
        Guid? ProductVariantId,
        string LicenseKey,
        string OrderNumber,
        OrderStatus OrderStatus,
        DateTimeOffset PurchaseDate);
}
