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
            ISender sender,
            CancellationToken ct) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 25 : Math.Min(pageSize, 200);
            var result = await sender.Send(new GetCustomersQuery(pageNumber, pageSize, searchTerm, status, member, registeredFrom, registeredTo), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error.Description);
        })
        .WithName("GetAdminCustomers")
        .RequirePermission(PermissionConstants.Security.ManageUsers);

        group.MapGet("export", async Task<IResult> (
            [FromQuery] string? searchTerm,
            [FromQuery] string? status,
            [FromQuery] bool? member,
            [FromQuery] DateTimeOffset? registeredFrom,
            [FromQuery] DateTimeOffset? registeredTo,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCustomersQuery(1, MaxExportRows, searchTerm, status, member, registeredFrom, registeredTo), ct);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error.Description);
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ToCsv(result.Value.Items))).ToArray();
            return Results.File(bytes, "text/csv; charset=utf-8", $"customers-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        })
        .WithName("ExportAdminCustomers")
        .RequirePermission(PermissionConstants.Security.ManageUsers);
    }

    /// <summary>Only account and activity columns: never a password hash, token or card detail.</summary>
    internal static string ToCsv(IEnumerable<CustomerListItemDto> customers)
    {
        var builder = new StringBuilder();
        builder.AppendLine("CustomerId,Email,FirstName,LastName,Phone,Status,EmailConfirmed,Language,Currency,RegisteredOnUtc,Orders,PaidOrders,PaidSpendUsd,LastOrderOnUtc,MembershipPlan,MembershipStatus,MembershipExpiresOnUtc,Points,ReferralCode");
        foreach (var c in customers)
        {
            builder.AppendLine(string.Join(',',
                Cell(c.Id.ToString()), Cell(c.Email), Cell(c.FirstName), Cell(c.LastName),
                // A leading apostrophe keeps spreadsheet apps from turning a phone number into a number (dropping the + or a leading zero).
                Cell(string.IsNullOrWhiteSpace(c.PhoneNumber) ? string.Empty : "'" + c.PhoneNumber, protectFormula: false),
                Cell(c.Status), Cell(c.EmailConfirmed ? "TRUE" : "FALSE"), Cell(c.PreferredLanguage), Cell(c.PreferredCurrency),
                Cell(c.RegisteredOnUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                c.OrderCount, c.PaidOrderCount, c.PaidSpendUsd.ToString("0.00", CultureInfo.InvariantCulture),
                Cell(c.LastOrderOnUtc?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty),
                Cell(c.MembershipPlan ?? string.Empty), Cell(c.MembershipStatus ?? string.Empty),
                Cell(c.MembershipExpiresOnUtc?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty),
                c.PointsBalance, Cell(c.ReferralCode ?? string.Empty)));
        }

        return builder.ToString();
    }

    private static string Cell(string value, bool protectFormula = true)
    {
        // A cell that starts with = + - @ would run as a formula when opened in Excel: neutralise it.
        if (protectFormula && value.Length > 0 && "=+-@".Contains(value[0]))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
