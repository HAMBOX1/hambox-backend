using System.Text.RegularExpressions;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.Modules.Suppliers.Application.Errors;
using HAMBOX.Modules.Suppliers.Application.Services;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Suppliers.Application.Features.Suppliers;

/// <summary>
/// The task-first "Map Products" workspace's backing queries/command — deliberately kept separate from
/// <see cref="SupplierHandlers"/> (CRUD-style types) since this is a distinct feature area: turning
/// "which HAMBOX products can this supplier fulfill, and what's the best guess at matching them" into
/// data, rather than managing <see cref="SupplierProductMapping"/> records one at a time.
/// </summary>

// ─── Queries: Map Products workspace ──────────────────────────

public sealed record GetSupplierMappingCandidatesQuery(
    Guid SupplierId, string? Search, string? StatusFilter, int Page, int PageSize)
    : IRequest<Result<PagedResult<SupplierMappingCandidateDto>>>;

/// <summary>
/// One row per eligible (active, visible, not-deleted) HAMBOX product variant, joined against this
/// supplier's own mappings only — resolving each variant's most specific active mapping (variant-specific
/// beats product-wide), the same precedence <c>GetSupplierFulfillmentChainQueryHandler</c> already
/// implements for the storefront-facing chain view. Pagination/search/status-filter happen in memory
/// after composing the DTOs, since "mapped vs unmapped" is a computed property, not a DB column — the
/// per-supplier candidate set is a bounded, human-curated product catalog, not a scale that needs a
/// database-side filter.
/// </summary>
internal sealed class GetSupplierMappingCandidatesQueryHandler(ISuppliersDbContext dbContext, ICatalogDbContext catalogDbContext)
    : IRequestHandler<GetSupplierMappingCandidatesQuery, Result<PagedResult<SupplierMappingCandidateDto>>>
{
    public async Task<Result<PagedResult<SupplierMappingCandidateDto>>> Handle(
        GetSupplierMappingCandidatesQuery request, CancellationToken cancellationToken)
    {
        var supplierExists = await dbContext.Suppliers.AsNoTracking().AnyAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (!supplierExists)
        {
            return Result.Failure<PagedResult<SupplierMappingCandidateDto>>(SupplierErrors.NotFound);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var eligible = await SupplierMappingCandidateSource.LoadEligibleVariantsAsync(catalogDbContext, request.Search, cancellationToken);

        var mappings = await dbContext.SupplierProductMappings.AsNoTracking()
            .Where(m => m.SupplierId == request.SupplierId && m.Status == SupplierMappingStatus.Active)
            .ToListAsync(cancellationToken);
        var mappingByVariant = mappings.Where(m => m.InternalProductVariantId != null)
            .ToDictionary(m => m.InternalProductVariantId!.Value);
        var mappingByProduct = mappings.Where(m => m.InternalProductVariantId == null)
            .ToDictionary(m => m.InternalProductId);

        var mappingIds = mappings.Select(m => m.Id).ToList();
        var availabilityByMapping = await dbContext.SupplierProductAvailabilities.AsNoTracking()
            .Where(a => mappingIds.Contains(a.SupplierProductMappingId))
            .ToDictionaryAsync(a => a.SupplierProductMappingId, cancellationToken);

        var optionLabels = await SupplierVariantLabelResolver.GetOptionLabelsAsync(
            catalogDbContext, eligible.Select(e => e.VariantId), cancellationToken);

        var candidates = eligible.Select(e =>
        {
            var mapping = mappingByVariant.TryGetValue(e.VariantId, out var vm)
                ? vm
                : mappingByProduct.GetValueOrDefault(e.ProductId);

            var availabilityState = mapping is not null && availabilityByMapping.TryGetValue(mapping.Id, out var availability)
                ? availability.AvailabilityState.ToString()
                : null;

            var displayName = optionLabels.GetValueOrDefault(e.VariantId) is { Length: > 0 } label ? label : e.VariantSku;

            return new SupplierMappingCandidateDto(
                e.ProductId, e.ProductName, e.CategoryName,
                e.VariantId, displayName, e.VariantSku,
                mapping?.Id, mapping?.ExternalProductId, mapping?.ExternalName,
                mapping?.BuyingPrice, mapping?.Currency, availabilityState);
        }).ToList();

        if (!string.IsNullOrWhiteSpace(request.StatusFilter) && !string.Equals(request.StatusFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            var wantMapped = string.Equals(request.StatusFilter, "mapped", StringComparison.OrdinalIgnoreCase);
            candidates = candidates.Where(c => (c.ExistingMappingId is not null) == wantMapped).ToList();
        }

        var totalCount = candidates.Count;
        var pageItems = candidates.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result.Success(new PagedResult<SupplierMappingCandidateDto>(pageItems, page, pageSize, totalCount));
    }
}

public sealed record GetSupplierMappingCandidatesSummaryQuery(Guid SupplierId)
    : IRequest<Result<SupplierMappingCandidatesSummaryDto>>;

/// <summary>Count-only companion to <see cref="GetSupplierMappingCandidatesQuery"/> — feeds both the Map
/// Products header stat cards and the supplier detail page's Fulfillment Health block, from the exact
/// same eligibility/mapping rules, so the two pages can never disagree.</summary>
internal sealed class GetSupplierMappingCandidatesSummaryQueryHandler(ISuppliersDbContext dbContext, ICatalogDbContext catalogDbContext)
    : IRequestHandler<GetSupplierMappingCandidatesSummaryQuery, Result<SupplierMappingCandidatesSummaryDto>>
{
    public async Task<Result<SupplierMappingCandidatesSummaryDto>> Handle(
        GetSupplierMappingCandidatesSummaryQuery request, CancellationToken cancellationToken)
    {
        var supplierExists = await dbContext.Suppliers.AsNoTracking().AnyAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (!supplierExists)
        {
            return Result.Failure<SupplierMappingCandidatesSummaryDto>(SupplierErrors.NotFound);
        }

        var eligible = await SupplierMappingCandidateSource.LoadEligibleVariantsAsync(catalogDbContext, search: null, cancellationToken);

        var mappings = await dbContext.SupplierProductMappings.AsNoTracking()
            .Where(m => m.SupplierId == request.SupplierId && m.Status == SupplierMappingStatus.Active)
            .Select(m => new { m.InternalProductId, m.InternalProductVariantId })
            .ToListAsync(cancellationToken);
        var variantMapped = mappings.Where(m => m.InternalProductVariantId != null)
            .Select(m => m.InternalProductVariantId!.Value).ToHashSet();
        var productWideMapped = mappings.Where(m => m.InternalProductVariantId == null)
            .Select(m => m.InternalProductId).ToHashSet();

        var mappedCount = eligible.Count(e => variantMapped.Contains(e.VariantId) || productWideMapped.Contains(e.ProductId));
        var totalCount = eligible.Count;

        return Result.Success(new SupplierMappingCandidatesSummaryDto(totalCount, mappedCount, totalCount - mappedCount));
    }
}

public sealed record SuggestionCandidate(Guid ProductId, Guid VariantId);

public sealed record SuggestSupplierMappingsQuery(Guid SupplierId, IReadOnlyList<SuggestionCandidate> Candidates)
    : IRequest<Result<IReadOnlyList<SupplierMappingSuggestionDto>>>;

/// <summary>
/// Live, on-demand auto-matching — Bamboo (and any future catalog-backed provider) has no bulk catalog
/// export, only a live paged search, so this calls <see cref="ISupplierProvider.SearchCatalogAsync"/>
/// once per candidate (bounded parallelism, never serially) rather than maintaining a local catalog
/// cache. Nothing here ever creates a mapping — it only scores candidates so the UI can offer
/// High/Medium/no-match actions; a low/no-confidence result is never auto-applied.
/// </summary>
internal sealed class SuggestSupplierMappingsQueryHandler(
    ISuppliersDbContext dbContext, ICatalogDbContext catalogDbContext, ISupplierProviderRegistry registry)
    : IRequestHandler<SuggestSupplierMappingsQuery, Result<IReadOnlyList<SupplierMappingSuggestionDto>>>
{
    private const int HighConfidenceThreshold = 80;
    private const int MediumConfidenceThreshold = 50;

    public async Task<Result<IReadOnlyList<SupplierMappingSuggestionDto>>> Handle(
        SuggestSupplierMappingsQuery request, CancellationToken cancellationToken)
    {
        if (request.Candidates.Count == 0)
        {
            return Result.Success<IReadOnlyList<SupplierMappingSuggestionDto>>([]);
        }

        var supplier = await dbContext.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<IReadOnlyList<SupplierMappingSuggestionDto>>(SupplierErrors.NotFound);
        }

        var providerResult = registry.Resolve(supplier.ProviderType);
        if (providerResult.IsFailure)
        {
            return Result.Success<IReadOnlyList<SupplierMappingSuggestionDto>>(NoMatchFor(request.Candidates));
        }

        var context = new SupplierProviderContext(
            supplier.Id, supplier.Code, supplier.BaseUrl,
            new SupplierProviderCredentials(
                supplier.ApiKey, supplier.ApiSecret, supplier.Username, supplier.Password, supplier.BearerToken, supplier.OAuthSettingsJson),
            supplier.SettingsJson);

        var productIds = request.Candidates.Select(c => c.ProductId).Distinct().ToList();
        var productNames = await catalogDbContext.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.NameEn })
            .ToDictionaryAsync(p => p.Id, p => p.NameEn, cancellationToken);

        var optionLabels = await SupplierVariantLabelResolver.GetOptionLabelsAsync(
            catalogDbContext, request.Candidates.Select(c => c.VariantId), cancellationToken);

        var provider = providerResult.Value;
        var results = new SupplierMappingSuggestionDto[request.Candidates.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, request.Candidates.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
            async (index, ct) =>
            {
                var candidate = request.Candidates[index];
                var searchTerm = $"{productNames.GetValueOrDefault(candidate.ProductId, string.Empty)} {optionLabels.GetValueOrDefault(candidate.VariantId, string.Empty)}".Trim();

                SupplierCatalogSearchResult searchResult;
                try
                {
                    searchResult = await provider.SearchCatalogAsync(new SupplierCatalogQuery(searchTerm, 1, 5), context, ct);
                }
                catch
                {
                    // Best-effort matching — a transient failure for one candidate must never fail the
                    // whole batch the "Find Matches" click is driving.
                    results[index] = NoMatch(candidate);
                    return;
                }

                if (!searchResult.IsSuccess || searchResult.Items.Count == 0 || string.IsNullOrWhiteSpace(searchTerm))
                {
                    results[index] = NoMatch(candidate);
                    return;
                }

                var best = searchResult.Items
                    .Select(item => (Item: item, Score: SupplierMatchScorer.Score(searchTerm, $"{item.Name} {item.BrandName}")))
                    .OrderByDescending(x => x.Score)
                    .First();

                var tier = best.Score >= HighConfidenceThreshold
                    ? "High"
                    : best.Score >= MediumConfidenceThreshold ? "Medium" : "None";

                results[index] = new SupplierMappingSuggestionDto(
                    candidate.ProductId, candidate.VariantId,
                    tier == "None" ? null : SupplierMapper.ToCatalogItemDto(best.Item),
                    tier == "None" ? 0 : best.Score,
                    tier);
            });

        return Result.Success<IReadOnlyList<SupplierMappingSuggestionDto>>(results);
    }

    private static SupplierMappingSuggestionDto NoMatch(SuggestionCandidate candidate) =>
        new(candidate.ProductId, candidate.VariantId, null, 0, "None");

    private static IReadOnlyList<SupplierMappingSuggestionDto> NoMatchFor(IReadOnlyList<SuggestionCandidate> candidates) =>
        candidates.Select(NoMatch).ToList();
}

// ─── Command: bulk mapping confirmation ────────────────────────

public sealed record BulkCreateSupplierMappingsCommand(Guid SupplierId, IReadOnlyList<CreateSupplierMappingRequest> Requests)
    : IRequest<Result<BulkCreateSupplierMappingsResultDto>>;

/// <summary>
/// The "Confirm N Mappings" bulk action. Reuses <see cref="SupplierMappingCreator.TryCreate"/> — the
/// exact same validation/duplicate-check <see cref="CreateSupplierMappingCommandHandler"/> uses for a
/// single mapping — per request, so there is one implementation of "is this mapping valid to create,"
/// not a second copy. Partial success is allowed: one invalid row (a duplicate, most commonly) never
/// blocks the rest of the batch from committing, and callers get both lists back to show "N of M created."
/// </summary>
internal sealed class BulkCreateSupplierMappingsCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<BulkCreateSupplierMappingsCommand, Result<BulkCreateSupplierMappingsResultDto>>
{
    public async Task<Result<BulkCreateSupplierMappingsResultDto>> Handle(
        BulkCreateSupplierMappingsCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<BulkCreateSupplierMappingsResultDto>(SupplierErrors.NotFound);
        }

        if (!supplier.IsEnabled)
        {
            return Result.Failure<BulkCreateSupplierMappingsResultDto>(SupplierErrors.SupplierDisabled);
        }

        var claimedInBatch = new HashSet<(Guid ProductId, Guid? VariantId)>();
        var created = new List<Guid>();
        var failures = new List<BulkMappingFailureDto>();

        foreach (var itemRequest in request.Requests)
        {
            var creationResult = await SupplierMappingCreator.TryCreate(dbContext, request.SupplierId, itemRequest, claimedInBatch, cancellationToken);
            if (creationResult.IsFailure)
            {
                failures.Add(new BulkMappingFailureDto(
                    itemRequest.InternalProductId, itemRequest.InternalProductVariantId,
                    creationResult.Error.Code, creationResult.Error.Description));
                continue;
            }

            dbContext.SupplierProductMappings.Add(creationResult.Value);
            SupplierAuditWriter.Record(dbContext, request.SupplierId, SupplierAuditAction.MappingCreated, currentUser.UserId);
            created.Add(creationResult.Value.Id);
        }

        if (created.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new BulkCreateSupplierMappingsResultDto(created, failures));
    }
}

// ─── Query: cross-supplier status for the product list ────────

public sealed record GetSupplierMappingStatusForProductsQuery(IReadOnlyList<Guid>? ProductIds)
    : IRequest<Result<IReadOnlyDictionary<Guid, ProductSupplierMappingStatusDto>>>;

/// <summary>
/// Supplier-agnostic — scans mappings across every supplier for the given products (or, when
/// <see cref="GetSupplierMappingStatusForProductsQuery.ProductIds"/> is null, every eligible product,
/// used only to resolve the product list's "Supplier Mapping" filter to a matching id-set before handing
/// it to Catalog's own paged query). Pure DB read (Catalog + Suppliers + the availability cache) — never
/// calls a provider live, so it stays safe to run for the whole catalog.
/// </summary>
internal sealed class GetSupplierMappingStatusForProductsQueryHandler(ISuppliersDbContext dbContext, ICatalogDbContext catalogDbContext, ISupplierProviderRegistry registry)
    : IRequestHandler<GetSupplierMappingStatusForProductsQuery, Result<IReadOnlyDictionary<Guid, ProductSupplierMappingStatusDto>>>
{
    public async Task<Result<IReadOnlyDictionary<Guid, ProductSupplierMappingStatusDto>>> Handle(
        GetSupplierMappingStatusForProductsQuery request, CancellationToken cancellationToken)
    {
        var variantsQuery = catalogDbContext.ProductVariants.AsNoTracking()
            .Where(v => !v.IsDeleted && v.Status == ProductVariantStatus.Active && v.IsVisible);

        if (request.ProductIds is { Count: > 0 })
        {
            variantsQuery = variantsQuery.Where(v => request.ProductIds.Contains(v.ProductId));
        }

        var variants = await variantsQuery.Select(v => new { v.Id, v.ProductId }).ToListAsync(cancellationToken);
        if (variants.Count == 0)
        {
            return Result.Success<IReadOnlyDictionary<Guid, ProductSupplierMappingStatusDto>>(
                new Dictionary<Guid, ProductSupplierMappingStatusDto>());
        }

        var productIds = variants.Select(v => v.ProductId).Distinct().ToList();
        var variantsByProduct = variants.GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.Select(v => v.Id).ToHashSet());

        var suppliers = await dbContext.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.Id, cancellationToken);
        var registeredProviderTypes = registry.GetRegisteredProviderTypes();

        var mappings = await dbContext.SupplierProductMappings.AsNoTracking()
            .Where(m => m.Status == SupplierMappingStatus.Active && productIds.Contains(m.InternalProductId))
            .OrderBy(m => m.Priority)
            .ToListAsync(cancellationToken);

        var mappingIds = mappings.Select(m => m.Id).ToList();
        var availabilityByMapping = await dbContext.SupplierProductAvailabilities.AsNoTracking()
            .Where(a => mappingIds.Contains(a.SupplierProductMappingId))
            .ToDictionaryAsync(a => a.SupplierProductMappingId, cancellationToken);

        var result = new Dictionary<Guid, ProductSupplierMappingStatusDto>();

        foreach (var productId in productIds)
        {
            var productVariantIds = variantsByProduct[productId];
            var productMappings = mappings.Where(m => m.InternalProductId == productId).ToList();

            var coveredVariantIds = productVariantIds
                .Where(variantId => productMappings.Any(m => m.InternalProductVariantId == variantId || m.InternalProductVariantId == null))
                .ToHashSet();

            var mappedCount = coveredVariantIds.Count;
            var totalCount = productVariantIds.Count;

            string status;
            if (mappedCount == 0)
            {
                status = "Unmapped";
            }
            else
            {
                var coveringMappings = productMappings
                    .Where(m => m.InternalProductVariantId == null || coveredVariantIds.Contains(m.InternalProductVariantId!.Value))
                    .ToList();

                var anyReady = coveringMappings.Any(m => suppliers.TryGetValue(m.SupplierId, out var s)
                    && s.IsEnabled && s.HasCredentialsConfigured && registeredProviderTypes.Contains(s.ProviderType));

                var allUnavailable = coveringMappings.Count > 0 && coveringMappings.All(m =>
                    availabilityByMapping.TryGetValue(m.Id, out var availability)
                    && availability.AvailabilityState == HAMBOX.Modules.Suppliers.Domain.Suppliers.SupplierAvailabilityState.Unavailable);

                status = !anyReady
                    ? "MappingError"
                    : allUnavailable
                        ? "SupplierUnavailable"
                        : mappedCount == totalCount ? "FullyMapped" : "PartiallyMapped";
            }

            var primarySupplierName = productMappings
                .Where(m => suppliers.ContainsKey(m.SupplierId))
                .OrderBy(m => m.Priority)
                .Select(m => suppliers[m.SupplierId].Name)
                .FirstOrDefault();

            result[productId] = new ProductSupplierMappingStatusDto(status, mappedCount, totalCount, primarySupplierName);
        }

        return Result.Success<IReadOnlyDictionary<Guid, ProductSupplierMappingStatusDto>>(result);
    }
}

// ─── Query: per-variant mapping breakdown for one product ─────

public sealed record GetProductVariantSupplierMappingsQuery(Guid ProductId)
    : IRequest<Result<IReadOnlyList<ProductVariantSupplierMappingDto>>>;

/// <summary>
/// The product-centric mapping drawer's and the product edit page's Supplier Fulfillment card's shared
/// data source — for one product, every eligible variant next to its resolved mapping across ANY
/// supplier (variant-specific mapping wins over product-wide, same precedence
/// <see cref="GetSupplierFulfillmentChainQueryHandler"/> already uses for one variant at a time — this
/// applies it across every variant of one product instead).
/// </summary>
internal sealed class GetProductVariantSupplierMappingsQueryHandler(ISuppliersDbContext dbContext, ICatalogDbContext catalogDbContext)
    : IRequestHandler<GetProductVariantSupplierMappingsQuery, Result<IReadOnlyList<ProductVariantSupplierMappingDto>>>
{
    public async Task<Result<IReadOnlyList<ProductVariantSupplierMappingDto>>> Handle(
        GetProductVariantSupplierMappingsQuery request, CancellationToken cancellationToken)
    {
        var variants = await catalogDbContext.ProductVariants.AsNoTracking()
            .Where(v => v.ProductId == request.ProductId && !v.IsDeleted && v.Status == ProductVariantStatus.Active && v.IsVisible)
            .OrderBy(v => v.SortOrder)
            .Select(v => new { v.Id, v.Sku })
            .ToListAsync(cancellationToken);

        if (variants.Count == 0)
        {
            return Result.Success<IReadOnlyList<ProductVariantSupplierMappingDto>>([]);
        }

        var optionLabels = await SupplierVariantLabelResolver.GetOptionLabelsAsync(
            catalogDbContext, variants.Select(v => v.Id), cancellationToken);

        var mappings = await dbContext.SupplierProductMappings.AsNoTracking()
            .Where(m => m.InternalProductId == request.ProductId && m.Status == SupplierMappingStatus.Active)
            .ToListAsync(cancellationToken);
        var mappingByVariant = mappings.Where(m => m.InternalProductVariantId != null)
            .ToDictionary(m => m.InternalProductVariantId!.Value);
        var productWideMapping = mappings.FirstOrDefault(m => m.InternalProductVariantId == null);

        var supplierIds = mappings.Select(m => m.SupplierId).Distinct().ToList();
        var suppliers = supplierIds.Count == 0
            ? []
            : await dbContext.Suppliers.AsNoTracking()
                .Where(s => supplierIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, cancellationToken);

        var mappingIds = mappings.Select(m => m.Id).ToList();
        var availabilityByMapping = mappingIds.Count == 0
            ? []
            : await dbContext.SupplierProductAvailabilities.AsNoTracking()
                .Where(a => mappingIds.Contains(a.SupplierProductMappingId))
                .ToDictionaryAsync(a => a.SupplierProductMappingId, cancellationToken);

        var result = variants.Select(v =>
        {
            var mapping = mappingByVariant.TryGetValue(v.Id, out var vm) ? vm : productWideMapping;
            var displayName = optionLabels.GetValueOrDefault(v.Id) is { Length: > 0 } label ? label : v.Sku;

            if (mapping is null)
            {
                return new ProductVariantSupplierMappingDto(
                    v.Id, displayName, v.Sku, null, null, null, null, null, null, null, null, null, "Unmapped");
            }

            var availabilityState = availabilityByMapping.TryGetValue(mapping.Id, out var availability)
                ? availability.AvailabilityState.ToString()
                : null;
            var supplierName = suppliers.TryGetValue(mapping.SupplierId, out var supplier) ? supplier.Name : null;

            return new ProductVariantSupplierMappingDto(
                v.Id, displayName, v.Sku, mapping.Id, mapping.SupplierId, supplierName,
                mapping.ExternalProductId, mapping.ExternalName, mapping.BuyingPrice, mapping.Currency, mapping.Priority,
                availabilityState, "Mapped");
        }).ToList();

        return Result.Success<IReadOnlyList<ProductVariantSupplierMappingDto>>(result);
    }
}

// ─── Shared helpers ─────────────────────────────────────────────

/// <summary>
/// One eligible (active, visible, not-deleted) product/variant pair, with just enough display data for
/// the Map Products table — the single source of "what counts as eligible for mapping" for both
/// <see cref="GetSupplierMappingCandidatesQueryHandler"/> and <see cref="GetSupplierMappingCandidatesSummaryQueryHandler"/>.
/// </summary>
internal sealed record EligibleCandidateVariant(Guid ProductId, string ProductName, string CategoryName, Guid VariantId, string VariantSku);

internal static class SupplierMappingCandidateSource
{
    public static async Task<IReadOnlyList<EligibleCandidateVariant>> LoadEligibleVariantsAsync(
        ICatalogDbContext catalogDbContext, string? search, CancellationToken cancellationToken)
    {
        var query =
            from variant in catalogDbContext.ProductVariants.AsNoTracking()
            join product in catalogDbContext.Products.AsNoTracking() on variant.ProductId equals product.Id
            where !variant.IsDeleted && variant.Status == ProductVariantStatus.Active && variant.IsVisible
                  && !product.IsDeleted && product.Status == ProductStatus.Active
            select new { Variant = variant, Product = product };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Product.NameEn.Contains(term) || x.Variant.Sku.Contains(term));
        }

        var rows = await query
            .OrderBy(x => x.Product.NameEn).ThenBy(x => x.Variant.SortOrder)
            .Select(x => new { x.Product.Id, x.Product.NameEn, x.Product.CategoryId, VariantId = x.Variant.Id, x.Variant.Sku })
            .ToListAsync(cancellationToken);

        var categoryIds = rows.Select(r => r.CategoryId).Distinct().ToList();
        var categoryNames = await catalogDbContext.Categories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameEn, cancellationToken);

        return rows.Select(r => new EligibleCandidateVariant(
            r.Id, r.NameEn, categoryNames.GetValueOrDefault(r.CategoryId, string.Empty), r.VariantId, r.Sku)).ToList();
    }
}

/// <summary>
/// Composes a human-readable label for a variant from its selected option values only (e.g. "Global",
/// "Canada") — never the SKU, which is usually an internal code a supplier's own catalog never echoes
/// back, and would otherwise pollute <see cref="SupplierMatchScorer"/>'s token overlap with noise. Empty
/// for a variant with no options; callers that need a display fallback (the Map Products table) fall
/// back to the SKU themselves, while live auto-matching deliberately does not. There is no persisted
/// "display name" column on <c>ProductVariant</c>; this is the one place that composes it for this
/// module, rather than duplicating the join per call site.
/// </summary>
internal static class SupplierVariantLabelResolver
{
    public static async Task<Dictionary<Guid, string>> GetOptionLabelsAsync(
        ICatalogDbContext catalogDbContext, IEnumerable<Guid> variantIds, CancellationToken cancellationToken)
    {
        var ids = variantIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var optionLabels = await (
            from variantOption in catalogDbContext.ProductVariantOptions.AsNoTracking()
            join option in catalogDbContext.ProductOptions.AsNoTracking() on variantOption.OptionId equals option.Id
            where ids.Contains(variantOption.VariantId)
            select new { variantOption.VariantId, option.Label }).ToListAsync(cancellationToken);

        return optionLabels
            .GroupBy(r => r.VariantId)
            .ToDictionary(g => g.Key, g => string.Join(" ", g.Select(r => r.Label)));
    }
}

/// <summary>
/// The single implementation of "is this mapping valid to create" — used by both
/// <see cref="CreateSupplierMappingCommandHandler"/> (one at a time) and
/// <see cref="BulkCreateSupplierMappingsCommandHandler"/> (a batch), so there is never a second, drifting
/// copy of the duplicate-check/creation rule. Does not save — the caller owns the transaction/SaveChanges
/// so a bulk caller can commit the whole batch in one round trip.
/// </summary>
internal static class SupplierMappingCreator
{
    public static async Task<Result<SupplierProductMapping>> TryCreate(
        ISuppliersDbContext dbContext,
        Guid supplierId,
        CreateSupplierMappingRequest request,
        HashSet<(Guid ProductId, Guid? VariantId)> claimedInBatch,
        CancellationToken cancellationToken)
    {
        var key = (request.InternalProductId, request.InternalProductVariantId);
        if (!claimedInBatch.Add(key))
        {
            return Result.Failure<SupplierProductMapping>(SupplierErrors.MappingAlreadyExists);
        }

        // NULL InternalProductVariantId is compared with `==` deliberately — see
        // CreateSupplierMappingCommandHandler's original remark: this must match the exact
        // "product-wide vs this-specific-variant" pair the DB unique index enforces.
        var duplicateExists = await dbContext.SupplierProductMappings.AsNoTracking()
            .AnyAsync(m => m.SupplierId == supplierId
                && m.InternalProductId == request.InternalProductId
                && m.InternalProductVariantId == request.InternalProductVariantId, cancellationToken);
        if (duplicateExists)
        {
            return Result.Failure<SupplierProductMapping>(SupplierErrors.MappingAlreadyExists);
        }

        var mapping = SupplierProductMapping.Create(
            supplierId, request.InternalProductId, request.ExternalProductId, request.ExternalSku, request.ExternalName,
            request.BuyingPrice, request.Currency, request.Priority, request.InternalProductVariantId);

        return Result.Success(mapping);
    }
}

/// <summary>
/// Normalized-token overlap (Jaccard similarity) between two free-text labels, 0-100. Deliberately the
/// simplest correct approach — no fuzzy-matching dependency — since it only has to separate "obviously
/// the same product" from "obviously not," not rank subtly different candidates.
/// </summary>
internal static class SupplierMatchScorer
{
    private static readonly Regex TokenPattern = new(@"[a-z0-9]+", RegexOptions.Compiled);

    public static int Score(string left, string right)
    {
        var a = Tokenize(left);
        var b = Tokenize(right);
        if (a.Count == 0 || b.Count == 0)
        {
            return 0;
        }

        var intersection = a.Intersect(b).Count();
        var union = a.Union(b).Count();
        return union == 0 ? 0 : (int)Math.Round(intersection * 100.0 / union);
    }

    private static HashSet<string> Tokenize(string text) =>
        TokenPattern.Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => t.Length > 1)
            .ToHashSet();
}
