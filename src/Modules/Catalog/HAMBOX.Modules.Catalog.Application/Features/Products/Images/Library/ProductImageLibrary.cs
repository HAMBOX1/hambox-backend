using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Products.Images.UploadProductImage;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.Images.Library;

/// <summary>
/// One row of the admin image library: a product image (or, in "without images" mode, a product that
/// has none — then <see cref="ImageId"/> and <see cref="ImageUrl"/> are null).
/// </summary>
public sealed record ProductImageLibraryItemDto(
    Guid? ImageId,
    Guid ProductId,
    string ProductName,
    string CategoryName,
    string? ImageUrl,
    bool IsPrimary,
    int DisplayOrder,
    long FileSizeBytes);

public sealed record ProductImageLibraryPageDto(
    IReadOnlyList<ProductImageLibraryItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record GetProductImageLibraryQuery(
    string? SearchTerm,
    Guid? CategoryId,
    bool WithoutImagesOnly,
    int Page = 1,
    int PageSize = 48) : IRequest<Result<ProductImageLibraryPageDto>>;

internal sealed class GetProductImageLibraryQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetProductImageLibraryQuery, Result<ProductImageLibraryPageDto>>
{
    public async Task<Result<ProductImageLibraryPageDto>> Handle(
        GetProductImageLibraryQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var term = request.SearchTerm?.Trim();

        var products = db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(term))
        {
            products = products.Where(p => p.NameEn.Contains(term) || p.NameAr.Contains(term));
        }

        if (request.CategoryId is { } categoryId)
        {
            products = products.Where(p => p.CategoryId == categoryId);
        }

        if (request.WithoutImagesOnly)
        {
            var withoutImages = products.Where(p => !db.ProductImages.Any(i => i.ProductId == p.Id));
            var withoutTotal = await withoutImages.CountAsync(cancellationToken);
            var missing = await withoutImages
                .OrderBy(p => p.NameEn)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductImageLibraryItemDto(
                    null,
                    p.Id,
                    p.NameEn,
                    db.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.NameEn).FirstOrDefault() ?? string.Empty,
                    null,
                    false,
                    0,
                    0))
                .ToListAsync(cancellationToken);

            return Result.Success(new ProductImageLibraryPageDto(missing, withoutTotal, page, pageSize));
        }

        var rows = db.ProductImages.AsNoTracking()
            .Join(products, i => i.ProductId, p => p.Id, (i, p) => new { Image = i, Product = p });

        var total = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderBy(r => r.Product.NameEn)
            .ThenBy(r => r.Image.DisplayOrder)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ProductImageLibraryItemDto(
                r.Image.Id,
                r.Product.Id,
                r.Product.NameEn,
                db.Categories.Where(c => c.Id == r.Product.CategoryId).Select(c => c.NameEn).FirstOrDefault() ?? string.Empty,
                r.Image.Url,
                r.Image.IsPrimary,
                r.Image.DisplayOrder,
                r.Image.FileSizeBytes))
            .ToListAsync(cancellationToken);

        return Result.Success(new ProductImageLibraryPageDto(items, total, page, pageSize));
    }
}

/// <summary>
/// Swaps the file behind an existing product image in place — it keeps its position and primary flag — so the
/// admin can fix a picture from the library without re-ordering anything.
/// </summary>
public sealed record ReplaceProductImageCommand(
    Guid ProductId,
    Guid ImageId,
    Stream Content,
    string FileName,
    string ContentType,
    long FileSizeBytes) : IRequest<Result<ProductImageDto>>;

internal sealed class ReplaceProductImageCommandHandler(
    ICatalogDbContext dbContext,
    IFileStorage fileStorage) : IRequestHandler<ReplaceProductImageCommand, Result<ProductImageDto>>
{
    public async Task<Result<ProductImageDto>> Handle(
        ReplaceProductImageCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FileSizeBytes <= 0
            || request.FileSizeBytes > fileStorage.MaxFileSizeBytes
            || !fileStorage.IsAllowedContentType(request.ContentType))
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.InvalidProductImage);
        }

        if (dbContext is not DbContext context)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.ProductNotFound);
        }

        var product = await context.Set<Product>()
            .Include(entry => entry.Images)
            .FirstOrDefaultAsync(entry => entry.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.ProductNotFound);
        }

        var image = product.Images.FirstOrDefault(entry => entry.Id == request.ImageId);
        if (image is null)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.ProductImageNotFound);
        }

        StoredFileResult stored;
        try
        {
            stored = await fileStorage.SaveAsync(
                request.Content, request.FileName, request.ContentType, $"products/{request.ProductId}", cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.InvalidProductImage);
        }

        var oldKey = image.StorageKey;
        image.ReplaceFile(stored.PublicUrl, stored.StorageKey, stored.FileName, stored.ContentType, stored.FileSizeBytes);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await fileStorage.DeleteAsync(stored.StorageKey, cancellationToken);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldKey))
        {
            await fileStorage.DeleteAsync(oldKey, cancellationToken);
        }

        return Result.Success(CatalogMapper.ToProductImageDto(image));
    }
}

/// <summary>
/// Copies one product image onto other products (each gets its own independent file), so e.g. every Xbox gift
/// card can share a picture without opening each product. Products already at the image limit are skipped.
/// </summary>
public sealed record ApplyImageToProductsCommand(Guid SourceImageId, IReadOnlyList<Guid> ProductIds)
    : IRequest<Result<ApplyImageToProductsResultDto>>;

public sealed record ApplyImageToProductsResultDto(int Applied, int Skipped);

internal sealed class ApplyImageToProductsCommandHandler(
    ICatalogDbContext dbContext,
    IFileStorage fileStorage,
    ISender sender) : IRequestHandler<ApplyImageToProductsCommand, Result<ApplyImageToProductsResultDto>>
{
    private const int MaxTargets = 200;

    public async Task<Result<ApplyImageToProductsResultDto>> Handle(
        ApplyImageToProductsCommand request,
        CancellationToken cancellationToken)
    {
        var source = await dbContext.ProductImages
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.SourceImageId, cancellationToken);

        if (source is null)
        {
            return Result.Failure<ApplyImageToProductsResultDto>(CatalogErrors.ProductImageNotFound);
        }

        var targets = request.ProductIds
            .Where(id => id != source.ProductId)
            .Distinct()
            .Take(MaxTargets)
            .ToList();

        var applied = 0;
        var skipped = 0;

        foreach (var productId in targets)
        {
            try
            {
                await using var stream = await fileStorage.OpenReadAsync(source.StorageKey, cancellationToken);
                var result = await sender.Send(
                    new UploadProductImageCommand(productId, stream, source.FileName, source.ContentType, source.FileSizeBytes),
                    cancellationToken);

                if (result.IsSuccess)
                {
                    applied++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception ex) when (ex is IOException or FileNotFoundException or InvalidOperationException)
            {
                skipped++;
            }
        }

        return Result.Success(new ApplyImageToProductsResultDto(applied, skipped));
    }
}
