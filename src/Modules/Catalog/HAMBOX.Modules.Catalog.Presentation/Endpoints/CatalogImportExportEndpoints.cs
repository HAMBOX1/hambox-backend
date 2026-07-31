using Asp.Versioning.Builder;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.ExecuteCatalogImport;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.ExportCatalog;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetCatalogPackageDownload;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetCatalogPackageJob;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetCatalogPackageJobs;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetImportLookups;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetImportTemplate;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.UploadCatalogImport;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.ValidateCatalogImport;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Catalog.Presentation.Endpoints;

/// <summary>
/// Catalog Import/Export — the "migrate/backup a whole catalog" wizard's backend. Export is a
/// background job (any format, any scope); import is upload -> validate -> execute -> summary,
/// never applied synchronously on upload.
/// </summary>
internal static class CatalogImportExportEndpoints
{
    public static void MapCatalogImportExportEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/catalog")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Catalog Import/Export")
            .HasApiVersion(1);

        group.MapPost("/export", async (
            [FromBody] ExportCatalogRequest body,
            ISender sender) => await Send(sender, new ExportCatalogCommand(
                new CatalogExportScope(body.ProductIds, body.SearchTerm, body.Status, body.CategoryId, body.SelectAllMatching, body.ExportEntireCatalog),
                body.Format,
                body.Options,
                body.EncryptCodes,
                body.PasswordProtectPackage,
                body.PackagePassword)))
            .WithName("ExportCatalog")
            .RequirePermission(PermissionConstants.Catalog.Products.Export);

        group.MapGet("/import/templates/{entityType}", async (
            CatalogImportEntityType entityType,
            ISender sender) =>
        {
            var result = await sender.Send(new GetImportTemplateQuery(entityType));
            return result.IsSuccess
                ? Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
                : Results.BadRequest(Problem(result));
        })
        .WithName("GetCatalogImportTemplate")
        .RequirePermission(PermissionConstants.Catalog.Products.Import);

        group.MapPost("/import/upload", async (
            IFormFile file,
            [FromForm] CatalogImportEntityType entityType,
            ISender sender) =>
        {
            if (file.Length <= 0)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Bad Request",
                    Detail = "A file is required.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            await using var stream = file.OpenReadStream();
            var result = await sender.Send(new UploadCatalogImportCommand(stream, file.FileName, file.ContentType, file.Length, entityType));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(Problem(result));
        })
        .WithName("UploadCatalogImport")
        .RequirePermission(PermissionConstants.Catalog.Products.Import)
        .DisableAntiforgery();

        group.MapPost("/import/{uploadId:guid}/validate", async (
            Guid uploadId,
            [FromBody] ValidateCatalogImportRequest? body,
            ISender sender) => await Send(sender, new ValidateCatalogImportQuery(
                uploadId,
                body?.PackagePassword,
                body?.SkuStrategy ?? CatalogSkuStrategy.UseImportedSku,
                body?.Corrections)))
            .WithName("ValidateCatalogImport")
            .RequirePermission(PermissionConstants.Catalog.Products.Import);

        group.MapPost("/import/{uploadId:guid}/execute", async (
            Guid uploadId,
            [FromBody] ExecuteCatalogImportRequest body,
            ISender sender) => await Send(sender, new ExecuteCatalogImportCommand(
                uploadId, body.Strategy, body.Options, body.PackagePassword,
                body.SkuStrategy, body.Corrections, body.RowOverrides)))
            .WithName("ExecuteCatalogImport")
            .RequirePermission(PermissionConstants.Catalog.Products.Import);

        group.MapGet("/import/lookups", async (ISender sender) =>
            await Send(sender, new GetCatalogImportLookupsQuery()))
            .WithName("GetCatalogImportLookups")
            .RequirePermission(PermissionConstants.Catalog.Products.Import);

        group.MapGet("/import-export/jobs", async (
            [FromQuery] CatalogPackageDirection? direction,
            [FromQuery] CatalogPackageJobStatus? status,
            [FromQuery] string? search,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? sort,
            ISender sender) => await Send(sender, new GetCatalogPackageJobsQuery(direction, status, search, page, pageSize, sort)))
            .WithName("GetCatalogPackageJobs")
            .RequirePermission(PermissionConstants.Catalog.Products.View);

        group.MapGet("/import-export/jobs/{jobId:guid}", async (
            Guid jobId,
            ISender sender) => await Send(sender, new GetCatalogPackageJobQuery(jobId)))
            .WithName("GetCatalogPackageJob")
            .RequirePermission(PermissionConstants.Catalog.Products.View);

        group.MapGet("/import-export/jobs/{jobId:guid}/download", async (
            Guid jobId,
            ISender sender) =>
        {
            var result = await sender.Send(new GetCatalogPackageDownloadQuery(jobId));
            return result.IsSuccess
                ? Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
                : Results.BadRequest(Problem(result));
        })
        .WithName("DownloadCatalogPackage")
        .RequirePermission(PermissionConstants.Catalog.Products.View);
    }

    private static async Task<IResult> Send<T>(ISender sender, IRequest<Result<T>> request)
    {
        var result = await sender.Send(request);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(Problem(result));
    }

    private static ProblemDetails Problem(Result result) => new()
    {
        Title = "Bad Request",
        Detail = result.Error.Description,
        Type = result.Error.Code,
        Status = StatusCodes.Status400BadRequest,
    };
}

internal sealed record ExportCatalogRequest(
    IReadOnlyList<Guid>? ProductIds,
    string? SearchTerm,
    ProductStatus? Status,
    Guid? CategoryId,
    bool SelectAllMatching,
    bool ExportEntireCatalog,
    CatalogPackageFormat Format,
    CatalogPackageOptions Options,
    bool EncryptCodes,
    bool PasswordProtectPackage,
    string? PackagePassword);

internal sealed record ValidateCatalogImportRequest(
    string? PackagePassword,
    CatalogSkuStrategy SkuStrategy = CatalogSkuStrategy.UseImportedSku,
    IReadOnlyList<CatalogImportCorrection>? Corrections = null);

internal sealed record ExecuteCatalogImportRequest(
    CatalogDuplicateStrategy Strategy,
    CatalogPackageOptions Options,
    string? PackagePassword,
    CatalogSkuStrategy SkuStrategy = CatalogSkuStrategy.UseImportedSku,
    IReadOnlyList<CatalogImportCorrection>? Corrections = null,
    IReadOnlyList<CatalogImportRowOverride>? RowOverrides = null);
