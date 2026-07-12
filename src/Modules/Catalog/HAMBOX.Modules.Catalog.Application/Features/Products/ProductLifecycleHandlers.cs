using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products;

public sealed record PublishProductCommand(Guid ProductId) : IRequest<Result>;
public sealed record DeactivateProductCommand(Guid ProductId) : IRequest<Result>;
public sealed record ArchiveProductCommand(Guid ProductId) : IRequest<Result>;
public sealed record RestoreProductCommand(Guid ProductId) : IRequest<Result>;
public sealed record DuplicateProductCommand(Guid ProductId, string? NameSuffix) : IRequest<Result<Guid>>;

internal sealed class PublishProductCommandHandler : IRequestHandler<PublishProductCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public PublishProductCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(PublishProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure(CatalogErrors.ProductNotFound);
        }

        try
        {
            product.Activate();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(new HAMBOX.SharedKernel.Errors.Error(CatalogErrors.InvalidProductStatusTransition.Code, ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class DeactivateProductCommandHandler : IRequestHandler<DeactivateProductCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public DeactivateProductCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(DeactivateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure(CatalogErrors.ProductNotFound);
        }

        try
        {
            product.Deactivate();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(new HAMBOX.SharedKernel.Errors.Error(CatalogErrors.InvalidProductStatusTransition.Code, ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class ArchiveProductCommandHandler : IRequestHandler<ArchiveProductCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public ArchiveProductCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure(CatalogErrors.ProductNotFound);
        }

        try
        {
            product.Archive();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(new HAMBOX.SharedKernel.Errors.Error(CatalogErrors.InvalidProductStatusTransition.Code, ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class RestoreProductCommandHandler : IRequestHandler<RestoreProductCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public RestoreProductCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(RestoreProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure(CatalogErrors.ProductNotFound);
        }

        try
        {
            product.Restore();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(new HAMBOX.SharedKernel.Errors.Error(CatalogErrors.InvalidProductStatusTransition.Code, ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class DuplicateProductCommandHandler : IRequestHandler<DuplicateProductCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DuplicateProductCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(DuplicateProductCommand request, CancellationToken cancellationToken)
    {
        var source = await _db.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (source is null)
        {
            return Result.Failure<Guid>(CatalogErrors.ProductNotFound);
        }

        var suffix = string.IsNullOrWhiteSpace(request.NameSuffix) ? " (Copy)" : request.NameSuffix.Trim();
        var duplicate = Product.Create(
            source.NameAr + suffix,
            source.NameEn + suffix,
            source.DescriptionAr,
            source.DescriptionEn,
            source.Price,
            source.CategoryId);

        foreach (var image in source.Images.OrderBy(i => i.DisplayOrder))
        {
            duplicate.AddImage(
                image.Url,
                image.StorageKey,
                image.FileName,
                image.ContentType,
                image.FileSizeBytes,
                image.DisplayOrder,
                image.IsPrimary);
        }

        _db.Products.Add(duplicate);

        var optionGroups = await _db.ProductOptionGroups
            .AsNoTracking()
            .Include(g => g.Options)
            .Where(g => g.ProductId == request.ProductId)
            .OrderBy(g => g.SortOrder)
            .ToListAsync(cancellationToken);

        var optionIdMap = new Dictionary<Guid, Guid>();

        foreach (var group in optionGroups)
        {
            var newGroup = ProductOptionGroup.Create(duplicate.Id, group.Key, group.DisplayName, group.SortOrder, group.IsRequired);
            _db.ProductOptionGroups.Add(newGroup);

            foreach (var option in group.Options.OrderBy(o => o.SortOrder))
            {
                var newOption = ProductOption.Create(newGroup.Id, option.Value, option.Label, option.SortOrder);
                _db.ProductOptions.Add(newOption);
                optionIdMap[option.Id] = newOption.Id;
            }
        }

        var variants = await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.SelectedOptions)
            .Where(v => v.ProductId == request.ProductId && !v.IsDeleted)
            .OrderBy(v => v.SortOrder)
            .ToListAsync(cancellationToken);

        foreach (var variant in variants)
        {
            var duplicateSku = BuildDuplicateSku(variant.Sku, "-COPY");
            var newVariant = ProductVariant.Create(
                duplicate.Id,
                duplicateSku,
                variant.PlanId,
                variant.PriceOverride,
                variant.ComparePrice,
                variant.SortOrder,
                variant.LowStockThreshold);

            newVariant.Update(
                duplicateSku,
                variant.PlanId,
                variant.PriceOverride,
                variant.ComparePrice,
                variant.SortOrder,
                ProductVariantStatus.Draft,
                false,
                variant.MembershipPlanId,
                variant.LowStockThreshold);

            var mappedOptionIds = variant.SelectedOptions
                .Select(o => optionIdMap.TryGetValue(o.OptionId, out var mapped) ? mapped : o.OptionId)
                .Where(id => optionIdMap.ContainsValue(id) || variant.SelectedOptions.Count == 0)
                .ToList();

            if (mappedOptionIds.Count > 0)
            {
                newVariant.SetOptions(mappedOptionIds);
            }

            _db.ProductVariants.Add(newVariant);
        }

        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.VariantCreated,
            productId: duplicate.Id,
            performedByUserId: _currentUser.UserId,
            details: $"Duplicated from product {request.ProductId}"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(duplicate.Id);
    }

    private static string BuildDuplicateSku(string sourceSku, string suffix)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var candidate = $"{sourceSku}{suffix}-{token}";
        return candidate.Length <= 100 ? candidate : candidate[..100];
    }
}
