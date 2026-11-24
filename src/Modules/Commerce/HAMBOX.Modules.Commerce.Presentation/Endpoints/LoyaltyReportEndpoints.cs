using System.Globalization;
using System.Text;
using Asp.Versioning.Builder;
using ClosedXML.Excel;
using HAMBOX.Modules.Commerce.Application.Features.Loyalty;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

/// <summary>Downloadable loyalty reports (Stars, ELITE, referrals, redemptions, B2B), as CSV or Excel, for a chosen period.</summary>
internal static class LoyaltyReportEndpoints
{
    public static void MapLoyaltyReportEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/admin/loyalty-reports")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Loyalty Reports")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("{kind}", async Task<IResult> (
                string kind,
                [FromQuery] DateTimeOffset? from,
                [FromQuery] DateTimeOffset? to,
                [FromQuery] string? format,
                System.Security.Claims.ClaimsPrincipal user,
                ILoggerFactory loggers,
                LoyaltyReportService reports,
                CancellationToken ct) =>
            {
                var report = await reports.BuildAsync(kind, from, to, ct);
                if (report is null)
                {
                    return Results.NotFound();
                }

                var xlsx = string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase);
                loggers.CreateLogger("LoyaltyReport").LogInformation(
                    "Loyalty report {Kind} by {User}: {Rows} rows, {Format}.", report.Name,
                    user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, report.Rows.Count, xlsx ? "xlsx" : "csv");

                var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
                return xlsx
                    ? Results.File(ToXlsx(report), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{report.Name}-{stamp}.xlsx")
                    : Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ToCsv(report))).ToArray(), "text/csv; charset=utf-8", $"{report.Name}-{stamp}.csv");
            })
            .WithName("DownloadLoyaltyReport")
            .RequirePermission(PermissionConstants.Reports.Export);
    }

    private static string ToCsv(TabularReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', report.Headers.Select(c => Cell(c))));
        foreach (var row in report.Rows)
        {
            builder.AppendLine(string.Join(',', row.Select(c => Cell(c))));
        }

        return builder.ToString();
    }

    private static byte[] ToXlsx(TabularReport report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(report.Name.Length > 31 ? report.Name[..31] : report.Name);
        for (var c = 0; c < report.Headers.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = report.Headers[c];
            sheet.Cell(1, c + 1).Style.Font.Bold = true;
        }

        for (var r = 0; r < report.Rows.Count; r++)
        {
            for (var c = 0; c < report.Rows[r].Count; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                cell.SetValue(report.Rows[r][c]);
                cell.Style.NumberFormat.Format = "@";
            }
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // A cell starting with = + - @ would run as a formula in Excel: neutralise it. (Negative Stars are plain numbers, so a lone leading minus on digits stays.)
    private static string Cell(string value)
    {
        if (value.Length > 0 && "=+@".Contains(value[0]))
        {
            value = "'" + value;
        }
        else if (value.Length > 1 && value[0] == '-' && !char.IsDigit(value[1]))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
