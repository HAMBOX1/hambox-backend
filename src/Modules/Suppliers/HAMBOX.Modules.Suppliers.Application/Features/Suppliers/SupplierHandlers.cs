using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.Modules.Suppliers.Application.Errors;
using HAMBOX.Modules.Suppliers.Application.Services;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Suppliers.Application.Features.Suppliers;

// ─── Queries ───────────────────────────────────────────────────

public sealed record GetSuppliersQuery(string? Search, string? Status, int Page, int PageSize)
    : IRequest<Result<PagedResult<SupplierListItemDto>>>;

internal sealed class GetSuppliersQueryHandler(ISuppliersDbContext dbContext)
    : IRequestHandler<GetSuppliersQuery, Result<PagedResult<SupplierListItemDto>>>
{
    public async Task<Result<PagedResult<SupplierListItemDto>>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = dbContext.Suppliers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(s => s.Name.Contains(search) || s.Code.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<SupplierStatus>(request.Status, true, out var status))
        {
            query = query.Where(s => s.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(s => s.Priority)
            .ThenBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => SupplierMapper.ToListItem(s))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<SupplierListItemDto>(items, page, pageSize, totalCount));
    }
}

public sealed record GetSupplierByIdQuery(Guid Id) : IRequest<Result<SupplierDetailDto>>;

internal sealed class GetSupplierByIdQueryHandler(ISuppliersDbContext dbContext)
    : IRequestHandler<GetSupplierByIdQuery, Result<SupplierDetailDto>>
{
    public async Task<Result<SupplierDetailDto>> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        return supplier is null
            ? Result.Failure<SupplierDetailDto>(SupplierErrors.NotFound)
            : Result.Success(SupplierMapper.ToDetail(supplier));
    }
}

public sealed record GetSupplierProviderTypesQuery : IRequest<Result<IReadOnlyCollection<string>>>;

internal sealed class GetSupplierProviderTypesQueryHandler(ISupplierProviderRegistry registry)
    : IRequestHandler<GetSupplierProviderTypesQuery, Result<IReadOnlyCollection<string>>>
{
    public Task<Result<IReadOnlyCollection<string>>> Handle(GetSupplierProviderTypesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(registry.GetRegisteredProviderTypes()));
}

/// <summary>
/// Admin-facing "why is/isn't this product/variant automatically fulfillable" view — every eligible
/// mapping across every supplier for the given product (and, when supplied, variant), safe-metadata
/// only, ordered exactly the way <c>Commerce.Application.Services.FulfillmentRouter</c>'s chain
/// resolution orders them (variant-specific mappings before product-wide, then <c>Priority</c>).
/// Duplicated here (not called via Commerce) because Suppliers has no reference to Commerce — see the
/// module's own architecture notes. Returns every mapping with <c>MappingStatus == Active</c>, not only
/// the winner, so an admin can see the whole chain including not-ready entries and why they're skipped.
/// </summary>
public sealed record GetSupplierFulfillmentChainQuery(Guid ProductId, Guid? VariantId)
    : IRequest<Result<IReadOnlyList<SupplierFulfillmentChainCandidateDto>>>;

internal sealed class GetSupplierFulfillmentChainQueryHandler(ISuppliersDbContext dbContext, ISupplierProviderRegistry registry)
    : IRequestHandler<GetSupplierFulfillmentChainQuery, Result<IReadOnlyList<SupplierFulfillmentChainCandidateDto>>>
{
    public async Task<Result<IReadOnlyList<SupplierFulfillmentChainCandidateDto>>> Handle(
        GetSupplierFulfillmentChainQuery request, CancellationToken cancellationToken)
    {
        var registeredProviderTypes = registry.GetRegisteredProviderTypes();

        var rows = await (
            from mapping in dbContext.SupplierProductMappings.AsNoTracking()
            join supplier in dbContext.Suppliers.AsNoTracking() on mapping.SupplierId equals supplier.Id
            where mapping.InternalProductId == request.ProductId
                  && (mapping.InternalProductVariantId == request.VariantId || mapping.InternalProductVariantId == null)
                  && mapping.Status == SupplierMappingStatus.Active
            select new { Mapping = mapping, Supplier = supplier })
            .ToListAsync(cancellationToken);

        var ordered = rows
            .OrderBy(r => r.Mapping.InternalProductVariantId is null ? 1 : 0)
            .ThenBy(r => r.Mapping.Priority)
            .Select(r => SupplierMapper.ToFulfillmentChainCandidate(r.Mapping, r.Supplier, registeredProviderTypes.Contains(r.Supplier.ProviderType)))
            .ToList();

        return Result.Success<IReadOnlyList<SupplierFulfillmentChainCandidateDto>>(ordered);
    }
}

public sealed record GetSupplierMappingsQuery(Guid SupplierId) : IRequest<Result<IReadOnlyList<SupplierMappingDto>>>;

/// <summary>
/// Enriches each mapping with its HAMBOX product name / variant SKU by reading directly from
/// <see cref="ICatalogDbContext"/> — the same established cross-module DbContext pattern Commerce
/// already uses for Catalog reads (e.g. cart/inventory). Display-only: the mapping's persisted
/// <c>InternalProductId</c>/<c>InternalProductVariantId</c> are the source of truth, this query never
/// writes anything, and a product/variant that can no longer be found simply yields a null name rather
/// than failing the whole list.
/// </summary>
internal sealed class GetSupplierMappingsQueryHandler(ISuppliersDbContext dbContext, ICatalogDbContext catalogDbContext)
    : IRequestHandler<GetSupplierMappingsQuery, Result<IReadOnlyList<SupplierMappingDto>>>
{
    public async Task<Result<IReadOnlyList<SupplierMappingDto>>> Handle(GetSupplierMappingsQuery request, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Suppliers.AsNoTracking().AnyAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<IReadOnlyList<SupplierMappingDto>>(SupplierErrors.NotFound);
        }

        var mappings = await dbContext.SupplierProductMappings.AsNoTracking()
            .Where(m => m.SupplierId == request.SupplierId)
            .OrderBy(m => m.Priority)
            .ToListAsync(cancellationToken);

        var productIds = mappings.Select(m => m.InternalProductId).Distinct().ToList();
        var variantIds = mappings.Where(m => m.InternalProductVariantId.HasValue)
            .Select(m => m.InternalProductVariantId!.Value).Distinct().ToList();

        var productNames = await catalogDbContext.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.NameEn })
            .ToDictionaryAsync(p => p.Id, p => p.NameEn, cancellationToken);

        var variantSkus = await catalogDbContext.ProductVariants.AsNoTracking()
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.Sku })
            .ToDictionaryAsync(v => v.Id, v => v.Sku, cancellationToken);

        var mappingIds = mappings.Select(m => m.Id).ToList();
        var availabilityByMapping = await dbContext.SupplierProductAvailabilities.AsNoTracking()
            .Where(a => mappingIds.Contains(a.SupplierProductMappingId))
            .ToDictionaryAsync(a => a.SupplierProductMappingId, cancellationToken);

        var result = mappings
            .Select(m => SupplierMapper.ToMappingDto(
                m,
                productNames.GetValueOrDefault(m.InternalProductId),
                m.InternalProductVariantId.HasValue ? variantSkus.GetValueOrDefault(m.InternalProductVariantId.Value) : null,
                availabilityByMapping.GetValueOrDefault(m.Id)))
            .ToList();

        return Result.Success<IReadOnlyList<SupplierMappingDto>>(result);
    }
}

/// <summary>
/// Admin-facing supplier catalog browse/search for the product-mapping picker — resolves the supplier's
/// provider exactly like <see cref="TestSupplierConnectionCommandHandler"/> does, so credentials only
/// ever flow server-side into <see cref="ISupplierProvider.SearchCatalogAsync"/>, never to the caller.
/// </summary>
public sealed record SearchSupplierCatalogQuery(Guid SupplierId, string? SearchTerm, int Page, int PageSize)
    : IRequest<Result<SupplierCatalogSearchResultDto>>;

internal sealed class SearchSupplierCatalogQueryHandler(ISuppliersDbContext dbContext, ISupplierProviderRegistry registry)
    : IRequestHandler<SearchSupplierCatalogQuery, Result<SupplierCatalogSearchResultDto>>
{
    public async Task<Result<SupplierCatalogSearchResultDto>> Handle(SearchSupplierCatalogQuery request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<SupplierCatalogSearchResultDto>(SupplierErrors.NotFound);
        }

        var providerResult = registry.Resolve(supplier.ProviderType);
        if (providerResult.IsFailure)
        {
            return Result.Failure<SupplierCatalogSearchResultDto>(providerResult.Error);
        }

        var context = new SupplierProviderContext(
            supplier.Id,
            supplier.Code,
            supplier.BaseUrl,
            new SupplierProviderCredentials(
                supplier.ApiKey, supplier.ApiSecret, supplier.Username, supplier.Password,
                supplier.BearerToken, supplier.OAuthSettingsJson),
            supplier.SettingsJson);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 50 ? 20 : request.PageSize;

        var searchResult = await providerResult.Value.SearchCatalogAsync(
            new SupplierCatalogQuery(request.SearchTerm, page, pageSize), context, cancellationToken);

        var items = searchResult.Items.Select(SupplierMapper.ToCatalogItemDto).ToList();
        return Result.Success(new SupplierCatalogSearchResultDto(searchResult.IsSuccess, items, searchResult.Message));
    }
}

// ─── Commands: Supplier ────────────────────────────────────────

public sealed record CreateSupplierCommand(CreateSupplierRequest Request) : IRequest<Result<Guid>>;

internal sealed class CreateSupplierCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<CreateSupplierCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;

        var codeExists = await dbContext.Suppliers.AsNoTracking()
            .AnyAsync(s => s.Code == r.Code.Trim().ToUpper(), cancellationToken);
        if (codeExists)
        {
            return Result.Failure<Guid>(SupplierErrors.CodeAlreadyExists);
        }

        if (!Enum.TryParse<SupplierAuthenticationType>(r.AuthenticationType, true, out var authType))
        {
            authType = SupplierAuthenticationType.None;
        }

        var supplier = Supplier.Create(r.Name, r.Code, r.ProviderType, authType, r.BaseUrl, r.Priority);
        supplier.UpdateDetails(
            r.Name, r.ProviderType, authType, r.BaseUrl,
            r.SupportsInventorySync, r.SupportsPriceSync, r.SupportsReservations, r.SupportsOrderStatus, r.SupportsWebhooks);

        dbContext.Suppliers.Add(supplier);
        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.Created, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(supplier.Id);
    }
}

public sealed record UpdateSupplierCommand(Guid Id, UpdateSupplierRequest Request) : IRequest<Result>;

internal sealed class UpdateSupplierCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateSupplierCommand, Result>
{
    public async Task<Result> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        var r = request.Request;
        if (!Enum.TryParse<SupplierAuthenticationType>(r.AuthenticationType, true, out var authType))
        {
            authType = SupplierAuthenticationType.None;
        }

        supplier.UpdateDetails(
            r.Name, r.ProviderType, authType, r.BaseUrl,
            r.SupportsInventorySync, r.SupportsPriceSync, r.SupportsReservations, r.SupportsOrderStatus, r.SupportsWebhooks);

        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.Updated, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record UpdateSupplierCredentialsCommand(Guid Id, UpdateSupplierCredentialsRequest Request) : IRequest<Result>;

internal sealed class UpdateSupplierCredentialsCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateSupplierCredentialsCommand, Result>
{
    public async Task<Result> Handle(UpdateSupplierCredentialsCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        var r = request.Request;
        supplier.UpdateCredentials(r.ApiKey, r.ApiSecret, r.Username, r.Password, r.BearerToken, r.OAuthSettingsJson);

        // Record which fields were touched, never the values — credentials are encrypted at rest and
        // must never appear in an audit log.
        var changedFields =
            $"{{\"apiKeyChanged\":{HasValue(r.ApiKey)},\"apiSecretChanged\":{HasValue(r.ApiSecret)}," +
            $"\"usernameChanged\":{HasValue(r.Username)},\"passwordChanged\":{HasValue(r.Password)}," +
            $"\"bearerTokenChanged\":{HasValue(r.BearerToken)},\"oAuthSettingsChanged\":{HasValue(r.OAuthSettingsJson)}}}";

        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.CredentialsUpdated, currentUser.UserId, changedFields);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static string HasValue(string? value) => (!string.IsNullOrWhiteSpace(value)).ToString().ToLowerInvariant();
}

public sealed record UpdateSupplierSettingsCommand(Guid Id, UpdateSupplierSettingsRequest Request) : IRequest<Result>;

internal sealed class UpdateSupplierSettingsCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateSupplierSettingsCommand, Result>
{
    public async Task<Result> Handle(UpdateSupplierSettingsCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        supplier.UpdateSettings(request.Request.SettingsJson);

        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.Updated, currentUser.UserId, "{\"field\":\"settings\"}");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record UpdateSupplierPriorityCommand(Guid Id, int Priority) : IRequest<Result>;

internal sealed class UpdateSupplierPriorityCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateSupplierPriorityCommand, Result>
{
    public async Task<Result> Handle(UpdateSupplierPriorityCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        supplier.UpdatePriority(request.Priority);

        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.PriorityChanged, currentUser.UserId, $"{{\"priority\":{request.Priority}}}");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record EnableSupplierCommand(Guid Id) : IRequest<Result>;

internal sealed class EnableSupplierCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<EnableSupplierCommand, Result>
{
    public async Task<Result> Handle(EnableSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        supplier.Enable();
        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.Enabled, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DisableSupplierCommand(Guid Id) : IRequest<Result>;

internal sealed class DisableSupplierCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DisableSupplierCommand, Result>
{
    public async Task<Result> Handle(DisableSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        supplier.Disable();
        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.Disabled, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteSupplierCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteSupplierCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DeleteSupplierCommand, Result>
{
    public async Task<Result> Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        supplier.Delete();
        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.Deleted, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record TestSupplierConnectionCommand(Guid Id) : IRequest<Result<SupplierTestConnectionResultDto>>;

internal sealed class TestSupplierConnectionCommandHandler(
    ISuppliersDbContext dbContext,
    ISupplierProviderRegistry registry,
    ICurrentUserService currentUser)
    : IRequestHandler<TestSupplierConnectionCommand, Result<SupplierTestConnectionResultDto>>
{
    public async Task<Result<SupplierTestConnectionResultDto>> Handle(TestSupplierConnectionCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<SupplierTestConnectionResultDto>(SupplierErrors.NotFound);
        }

        var providerResult = registry.Resolve(supplier.ProviderType);
        if (providerResult.IsFailure)
        {
            return Result.Failure<SupplierTestConnectionResultDto>(providerResult.Error);
        }

        var context = new SupplierProviderContext(
            supplier.Id,
            supplier.Code,
            supplier.BaseUrl,
            new SupplierProviderCredentials(
                supplier.ApiKey, supplier.ApiSecret, supplier.Username, supplier.Password,
                supplier.BearerToken, supplier.OAuthSettingsJson),
            supplier.SettingsJson);

        var testResult = await providerResult.Value.TestConnectionAsync(context, cancellationToken);

        SupplierAuditWriter.Record(
            dbContext, supplier.Id, SupplierAuditAction.ConnectionTested, currentUser.UserId,
            $"{{\"success\":{testResult.IsSuccess.ToString().ToLowerInvariant()}}}");
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new SupplierTestConnectionResultDto(testResult.IsSuccess, testResult.Message));
    }
}

// ─── Commands: Product Mapping ─────────────────────────────────

public sealed record CreateSupplierMappingCommand(Guid SupplierId, CreateSupplierMappingRequest Request) : IRequest<Result<Guid>>;

internal sealed class CreateSupplierMappingCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<CreateSupplierMappingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateSupplierMappingCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<Guid>(SupplierErrors.NotFound);
        }

        // A disabled supplier is not "selected" for anything new — existing mappings created before it
        // was disabled are untouched, this only guards the creation of new ones.
        if (!supplier.IsEnabled)
        {
            return Result.Failure<Guid>(SupplierErrors.SupplierDisabled);
        }

        var r = request.Request;

        // NULL InternalProductVariantId is compared with `==` here (not `.Value ==`) deliberately —
        // this must match the exact "product-wide vs this-specific-variant" pair the DB unique index
        // enforces, so two NULLs correctly collide as a duplicate product-wide mapping while two
        // different non-null variant ids never do.
        var duplicateExists = await dbContext.SupplierProductMappings.AsNoTracking()
            .AnyAsync(m => m.SupplierId == request.SupplierId
                && m.InternalProductId == r.InternalProductId
                && m.InternalProductVariantId == r.InternalProductVariantId, cancellationToken);
        if (duplicateExists)
        {
            return Result.Failure<Guid>(SupplierErrors.MappingAlreadyExists);
        }

        var mapping = SupplierProductMapping.Create(
            request.SupplierId, r.InternalProductId, r.ExternalProductId, r.ExternalSku, r.ExternalName, r.BuyingPrice, r.Currency, r.Priority,
            r.InternalProductVariantId);

        dbContext.SupplierProductMappings.Add(mapping);
        SupplierAuditWriter.Record(dbContext, request.SupplierId, SupplierAuditAction.MappingCreated, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(mapping.Id);
    }
}

public sealed record UpdateSupplierMappingCommand(Guid SupplierId, Guid MappingId, UpdateSupplierMappingRequest Request) : IRequest<Result>;

internal sealed class UpdateSupplierMappingCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateSupplierMappingCommand, Result>
{
    public async Task<Result> Handle(UpdateSupplierMappingCommand request, CancellationToken cancellationToken)
    {
        var mapping = await dbContext.SupplierProductMappings
            .FirstOrDefaultAsync(m => m.Id == request.MappingId && m.SupplierId == request.SupplierId, cancellationToken);
        if (mapping is null)
        {
            return Result.Failure(SupplierErrors.MappingNotFound);
        }

        var r = request.Request;
        if (!Enum.TryParse<SupplierMappingStatus>(r.Status, true, out var status))
        {
            status = SupplierMappingStatus.Active;
        }

        mapping.Update(r.ExternalProductId, r.ExternalSku, r.ExternalName, r.BuyingPrice, r.Currency, r.Priority, status);

        SupplierAuditWriter.Record(dbContext, request.SupplierId, SupplierAuditAction.MappingUpdated, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// Priority-only mutation for the admin fulfillment-chain reorder UI, which may need to persist a new
/// order across mappings belonging to different suppliers in one drag-drop action — deliberately does
/// not require (and cannot accidentally corrupt) any of the mapping's other fields.
/// </summary>
public sealed record UpdateSupplierMappingPriorityCommand(Guid SupplierId, Guid MappingId, int Priority) : IRequest<Result>;

internal sealed class UpdateSupplierMappingPriorityCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateSupplierMappingPriorityCommand, Result>
{
    public async Task<Result> Handle(UpdateSupplierMappingPriorityCommand request, CancellationToken cancellationToken)
    {
        var mapping = await dbContext.SupplierProductMappings
            .FirstOrDefaultAsync(m => m.Id == request.MappingId && m.SupplierId == request.SupplierId, cancellationToken);
        if (mapping is null)
        {
            return Result.Failure(SupplierErrors.MappingNotFound);
        }

        mapping.UpdatePriority(request.Priority);

        SupplierAuditWriter.Record(dbContext, request.SupplierId, SupplierAuditAction.MappingUpdated, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteSupplierMappingCommand(Guid SupplierId, Guid MappingId) : IRequest<Result>;

internal sealed class DeleteSupplierMappingCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DeleteSupplierMappingCommand, Result>
{
    public async Task<Result> Handle(DeleteSupplierMappingCommand request, CancellationToken cancellationToken)
    {
        var mapping = await dbContext.SupplierProductMappings
            .FirstOrDefaultAsync(m => m.Id == request.MappingId && m.SupplierId == request.SupplierId, cancellationToken);
        if (mapping is null)
        {
            return Result.Failure(SupplierErrors.MappingNotFound);
        }

        dbContext.SupplierProductMappings.Remove(mapping);
        SupplierAuditWriter.Record(dbContext, request.SupplierId, SupplierAuditAction.MappingDeleted, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
