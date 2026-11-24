using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Features.Archive;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class AdminCustomerArchiveEndpoints
{
    private const long MaxFileBytes = 10 * 1024 * 1024;

    public static void MapAdminCustomerArchiveEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/admin/customer-archive")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Customer Archive")
            .HasApiVersion(1)
            .RequireAuthorization();

        // A dry run: validates and reconciles the file, writes nothing.
        group.MapPost("preview", (IFormFile file, ISender sender) => ImportAsync(file, commit: false, sender))
            .DisableAntiforgery()
            .WithName("PreviewCustomerArchive")
            .RequirePermission(PermissionConstants.Customers.Import);

        group.MapPost("import", (IFormFile file, ISender sender) => ImportAsync(file, commit: true, sender))
            .DisableAntiforgery()
            .WithName("ImportCustomerArchive")
            .RequirePermission(PermissionConstants.Customers.Import);

        group.MapGet("records", async Task<IResult> (
                [FromQuery] string? search, [FromQuery] string? status, [FromQuery] int pageNumber, [FromQuery] int pageSize, ISender sender) =>
                MapResult(await sender.Send(new ListArchiveRecordsQuery(search, status, pageNumber <= 0 ? 1 : pageNumber, pageSize <= 0 ? 25 : pageSize))))
            .WithName("ListCustomerArchiveRecords")
            .RequirePermission(PermissionConstants.Customers.Import);

        group.MapPost("records/{id:guid}/link", async Task<IResult> (Guid id, LinkRequest body, ISender sender) =>
                MapResult(await sender.Send(new LinkArchiveRecordCommand(id, body.UserId, body.Reason))))
            .WithName("LinkCustomerArchiveRecord")
            .RequirePermission(PermissionConstants.Customers.Import);
    }

    private static async Task<IResult> ImportAsync(IFormFile file, bool commit, ISender sender)
    {
        if (file is null || file.Length == 0 || file.Length > MaxFileBytes)
        {
            return Results.BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "Upload a file of up to 10 MB.", Status = StatusCodes.Status400BadRequest });
        }

        await using var stream = file.OpenReadStream();
        return MapResult(await sender.Send(new ImportArchiveCommand(stream, file.FileName, commit)));
    }

    private static IResult MapResult<T>(Result<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest,
            });

    internal sealed record LinkRequest(string UserId, string Reason);
}
