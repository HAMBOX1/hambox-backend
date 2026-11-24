using System.Globalization;
using System.Text;
using Asp.Versioning.Builder;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Features.Customers;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Identity.Presentation.Endpoints;

/// <summary>Admin Customers screen: a searchable list with each customer's store activity, and a CSV export of the same rows.</summary>
internal static class CustomerEndpoints
{
    private const int MaxExportRows = 20_000;

    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/admin/customers")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Customers")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            [FromQuery] string? status,
            [FromQuery] bool? member,
            [FromQuery] DateTimeOffset? registeredFrom,
            [FromQuery] DateTimeOffset? registeredTo,
            [FromQuery] string? rank,
            ISender sender,
            CancellationToken ct) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 25 : Math.Min(pageSize, 200);
            var result = await sender.Send(new GetCustomersQuery(pageNumber, pageSize, searchTerm, status, member, registeredFrom, registeredTo, rank), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error.Description);
        })
        .WithName("GetAdminCustomers")
        .RequirePermission(PermissionConstants.Security.ManageUsers);

        group.MapGet("{id:guid}", async Task<IResult> (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCustomer360Query(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error.Description);
        })
        .WithName("GetAdminCustomer360")
        .RequirePermission(PermissionConstants.Security.ManageUsers);

        group.MapGet("export", async Task<IResult> (
            [FromQuery] string? searchTerm,
            [FromQuery] string? status,
            [FromQuery] bool? member,
            [FromQuery] DateTimeOffset? registeredFrom,
            [FromQuery] DateTimeOffset? registeredTo,
            [FromQuery] string? rank,
            [FromQuery] string? format,
            [FromQuery] string? columns,
            System.Security.Claims.ClaimsPrincipal user,
            ILoggerFactory loggers,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCustomersQuery(1, MaxExportRows, searchTerm, status, member, registeredFrom, registeredTo, rank), ct);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error.Description);
            }

            var selected = CustomerExportColumns.Resolve(columns);
            var xlsx = string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase);

            // Who pulled which customer data, when, with what filters — the audit trail the brief asks for.
            loggers.CreateLogger("CustomerExport").LogInformation(
                "Customer export by {User}: {Rows} rows, format {Format}, columns [{Columns}], filters search={Search} status={Status} member={Member} rank={Rank}.",
                user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, result.Value.Items.Count, xlsx ? "xlsx" : "csv",
                string.Join(',', selected.Select(c => c.Key)), searchTerm, status, member, rank);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            if (xlsx)
            {
                return Results.File(
                    CustomerExportColumns.ToXlsx(result.Value.Items, selected),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"customers-{stamp}.xlsx");
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(CustomerExportColumns.ToCsv(result.Value.Items, selected))).ToArray();
            return Results.File(bytes, "text/csv; charset=utf-8", $"customers-{stamp}.csv");
        })
        .WithName("ExportAdminCustomers")
        .RequirePermission(PermissionConstants.Customers.Export);
    }
}
