using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.ExportProducts;

internal sealed class ExportProductsQueryHandler : IRequestHandler<ExportProductsQuery, Result<ExportProductsDto>>
{
    private readonly ICatalogDbContext _db;

    public ExportProductsQueryHandler(ICatalogDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ExportProductsDto>> Handle(ExportProductsQuery request, CancellationToken cancellationToken)
    {
        var selection = new BulkProductSelection(
            request.Request.ProductIds,
            request.Request.SearchTerm,
            request.Request.Status,
            request.Request.CategoryId,
            request.Request.SelectAllMatching);

        var resolved = await BulkProductSelectionResolver.ResolveAsync(_db, selection, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<ExportProductsDto>(resolved.Error);
        }

        var productIds = resolved.Value;

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.NameEn,
                p.Price,
                p.Status,
                CategoryName = _db.Categories.FirstOrDefault(c => c.Id == p.CategoryId)!.NameEn,
            })
            .ToListAsync(cancellationToken);

        var stockByProduct = await _db.ProductVariants
            .AsNoTracking()
            .Where(v => productIds.Contains(v.ProductId))
            .Join(
                _db.DigitalInventoryCodes.AsNoTracking().Where(c => c.Status == InventoryCodeStatus.Available),
                v => v.Id,
                c => c.VariantId,
                (v, c) => v.ProductId)
            .GroupBy(productId => productId)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProductId, x => x.Count, cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("Name,Category,Price,Status,AvailableStock");

        foreach (var product in products)
        {
            builder.AppendLine(
                $"{EscapeCsv(product.NameEn)},{EscapeCsv(product.CategoryName)},{product.Price},{product.Status},{stockByProduct.GetValueOrDefault(product.Id, 0)}");
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Result.Success(new ExportProductsDto(
            $"products-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv",
            "text/csv",
            bytes));
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
