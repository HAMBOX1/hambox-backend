using System.Security.Cryptography;
using Asp.Versioning.Builder;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Features.Filters;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Catalog.Presentation.Endpoints;

/// <summary>Excel / CSV export and import of filters and of the filter tags on products.</summary>
internal static class FilterSheetEndpoints
{
    private const long MaxUploadBytes = 20 * 1024 * 1024;

    public static void MapFilterSheetEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/filters")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Filters")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("export/definitions", async Task<IResult> ([FromQuery] string? format, ISender sender, IFilterSheetCodec codec) =>
                File(await sender.Send(new ExportFilterDefinitionsQuery()), codec, format, "filters-definitions"))
            .RequirePermission(PermissionConstants.Catalog.Categories.View);

        group.MapGet("export/products", async Task<IResult> (
                [FromQuery] string? format,
                [FromQuery] Guid? categoryId,
                [FromQuery] string? productIds,
                [FromQuery] bool template,
                ISender sender,
                IFilterSheetCodec codec) =>
            {
                var ids = string.IsNullOrWhiteSpace(productIds)
                    ? null
                    : productIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
                return File(await sender.Send(new ExportProductFiltersQuery(categoryId, ids, template)), codec, format, template ? "filters-template" : "products-filters");
            })
            .RequirePermission(PermissionConstants.Catalog.Products.View);

        group.MapPost("import/products", async Task<IResult> (
                IFormFile file,
                [FromForm] FilterAssignMode mode,
                [FromForm] bool allowNewValues,
                [FromForm] bool apply,
                ISender sender,
                IFilterSheetCodec codec) =>
            await ImportAsync(file, codec, sheet => new ImportProductFiltersCommand(sheet.Sheet, file.FileName, sheet.Hash, mode, allowNewValues, apply), sender))
            .RequirePermission(PermissionConstants.Catalog.Products.Edit)
            .DisableAntiforgery();

        group.MapPost("import/definitions", async Task<IResult> (
                IFormFile file,
                [FromForm] bool apply,
                ISender sender,
                IFilterSheetCodec codec) =>
            await ImportAsync(file, codec, sheet => new ImportFilterDefinitionsCommand(sheet.Sheet, file.FileName, sheet.Hash, apply), sender))
            .RequirePermission(PermissionConstants.Catalog.Categories.Edit)
            .DisableAntiforgery();

        group.MapGet("import/logs", async Task<IResult> (ISender sender) => Map(await sender.Send(new GetFilterImportLogsQuery())))
            .RequirePermission(PermissionConstants.Catalog.Products.View);

        group.MapPost("import/logs/{id:guid}/undo", async Task<IResult> (Guid id, ISender sender) => Map(await sender.Send(new UndoFilterImportCommand(id))))
            .RequirePermission(PermissionConstants.Catalog.Products.Edit);
    }

    private sealed record ParsedSheet(FilterSheet Sheet, string Hash);

    private static async Task<IResult> ImportAsync(
        IFormFile file,
        IFilterSheetCodec codec,
        Func<ParsedSheet, IRequest<Result<FilterImportPreviewDto>>> toCommand,
        ISender sender)
    {
        if (file is null || file.Length <= 0)
        {
            return Problem("A file is required.");
        }

        if (file.Length > MaxUploadBytes)
        {
            return Problem("The file is too large (the limit is 20 MB).");
        }

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var hash = Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
        buffer.Position = 0;

        FilterSheet sheet;
        try
        {
            sheet = codec.Read(buffer, file.FileName);
        }
        catch (InvalidDataException ex)
        {
            return Problem(ex.Message);
        }

        return Map(await sender.Send(toCommand(new ParsedSheet(sheet, hash))));
    }

    private static IResult File(Result<FilterSheet> result, IFilterSheetCodec codec, string? format, string baseName)
    {
        if (!result.IsSuccess)
        {
            return Problem(result.Error.Description);
        }

        var csv = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);
        return Results.File(
            codec.Write(result.Value, csv ? "csv" : "xlsx"),
            csv ? "text/csv; charset=utf-8" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{baseName}-{DateTime.UtcNow:yyyyMMdd-HHmm}.{(csv ? "csv" : "xlsx")}");
    }

    private static IResult Problem(string detail) =>
        TypedResults.BadRequest(new ProblemDetails { Title = "Bad Request", Detail = detail, Status = StatusCodes.Status400BadRequest });

    private static IResult Map(Result result) =>
        result.IsSuccess ? TypedResults.Ok() : Problem(result.Error.Description);

    private static IResult Map<T>(Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : Problem(result.Error.Description);
}
