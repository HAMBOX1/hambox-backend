using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using HAMBOX.Modules.Identity.Application.Features.Customers;

namespace HAMBOX.Modules.Identity.Presentation.Endpoints;

/// <summary>
/// The columns a customer export may contain. Only account and activity data: never a password hash, token or card detail.
/// The caller picks a subset by key; unknown keys are ignored. Dates are written in one fixed, unambiguous format and phone
/// numbers as text so a spreadsheet never turns them into numbers.
/// </summary>
internal static class CustomerExportColumns
{
    internal sealed record Column(string Key, string Header, Func<CustomerListItemDto, string> Value, bool Text = false);

    private static string Date(DateTimeOffset? d) => d?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Date(DateTime? d) => d?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;

    public static readonly IReadOnlyList<Column> All =
    [
        new("customerId", "CustomerId", c => c.Id.ToString()),
        new("email", "Email", c => c.Email),
        new("firstName", "FirstName", c => c.FirstName),
        new("lastName", "LastName", c => c.LastName),
        new("phone", "Phone", c => c.PhoneNumber ?? string.Empty, Text: true),
        new("status", "Status", c => c.Status),
        new("emailConfirmed", "EmailConfirmed", c => c.EmailConfirmed ? "TRUE" : "FALSE"),
        new("language", "Language", c => c.PreferredLanguage),
        new("currency", "Currency", c => c.PreferredCurrency),
        new("registeredOnUtc", "RegisteredOnUtc", c => Date(c.RegisteredOnUtc)),
        new("orders", "Orders", c => c.OrderCount.ToString(CultureInfo.InvariantCulture)),
        new("paidOrders", "PaidOrders", c => c.PaidOrderCount.ToString(CultureInfo.InvariantCulture)),
        new("paidSpendUsd", "PaidSpendUsd", c => c.PaidSpendUsd.ToString("0.00", CultureInfo.InvariantCulture)),
        new("lastOrderOnUtc", "LastOrderOnUtc", c => Date(c.LastOrderOnUtc)),
        new("rank", "Rank", c => c.RankKey ?? string.Empty),
        new("membershipPlan", "EliteSubscription", c => c.MembershipPlan ?? string.Empty),
        new("membershipStatus", "EliteStatus", c => c.MembershipStatus ?? string.Empty),
        new("membershipExpiresOnUtc", "EliteExpiresOnUtc", c => Date(c.MembershipExpiresOnUtc)),
        new("stars", "StarsAvailable", c => c.PointsBalance.ToString(CultureInfo.InvariantCulture)),
        new("starsPending", "StarsPending", c => c.StarsPending.ToString(CultureInfo.InvariantCulture)),
        new("referralCode", "ReferralCode", c => c.ReferralCode ?? string.Empty),
    ];

    public static IReadOnlyList<Column> Resolve(string? keys)
    {
        if (string.IsNullOrWhiteSpace(keys))
        {
            return All;
        }

        var wanted = keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var picked = All.Where(c => wanted.Contains(c.Key)).ToList();
        return picked.Count == 0 ? All : picked;
    }

    public static string ToCsv(IEnumerable<CustomerListItemDto> customers, IReadOnlyList<Column> columns)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', columns.Select(c => c.Header)));
        foreach (var customer in customers)
        {
            builder.AppendLine(string.Join(',', columns.Select(c => Cell(c.Value(customer), c.Text))));
        }

        return builder.ToString();
    }

    public static byte[] ToXlsx(IEnumerable<CustomerListItemDto> customers, IReadOnlyList<Column> columns)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Customers");
        for (var i = 0; i < columns.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = columns[i].Header;
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var customer in customers)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                var cell = sheet.Cell(row, i + 1);
                var value = columns[i].Value(customer);
                // Always stored as text: neutralises formulas and keeps phone numbers and ids exact.
                cell.SetValue(value);
                cell.Style.NumberFormat.Format = "@";
            }

            row++;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // A cell that starts with = + - @ would run as a formula when opened in Excel: neutralise it.
    private static string Cell(string value, bool text)
    {
        if (text && value.Length > 0)
        {
            value = "'" + value;
        }
        else if (value.Length > 0 && "=+-@".Contains(value[0]))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
