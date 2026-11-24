using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using HAMBOX.Modules.Commerce.Application.Archive;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Reads the old archive from an .xlsx (first sheet = customers, an optional sheet named "Orders" = orders) or a .csv of
/// customers. Columns are matched by header name, case-insensitively, so the client's export needs no reformatting.
/// </summary>
internal sealed class LegacyArchiveParser : ILegacyArchiveParser
{
    public const int MaxRows = 20_000;

    private static readonly string[] IdHeaders = ["legacyid", "customerid", "id", "customer id", "legacy id"];
    private static readonly string[] EmailHeaders = ["email", "e-mail", "mail"];
    private static readonly string[] PhoneHeaders = ["phone", "mobile", "whatsapp", "phonenumber", "phone number"];
    private static readonly string[] NameHeaders = ["name", "fullname", "full name", "customername"];
    private static readonly string[] DateHeaders = ["registeredon", "registeredonutc", "createdon", "createdat", "registered", "signupdate", "date"];
    private static readonly string[] StarsHeaders = ["promisedstars", "stars", "points", "promised stars", "balance"];
    private static readonly string[] OrderIdHeaders = ["orderid", "legacyorderid", "order id", "ordernumber"];
    private static readonly string[] TotalHeaders = ["total", "totalusd", "amount", "totalamount"];
    private static readonly string[] CurrencyHeaders = ["currency"];
    private static readonly string[] StatusHeaders = ["status"];

    public ParsedArchive Parse(Stream content, string fileName)
    {
        var table = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? [ReadCsv(content)]
            : ReadWorkbook(content);

        var customers = new List<ArchiveCustomerRow>();
        var orders = new List<ArchiveOrderRow>();

        foreach (var (name, rows) in table)
        {
            if (rows.Count < 2)
            {
                continue;
            }

            var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
            var isOrders = name.Equals("orders", StringComparison.OrdinalIgnoreCase) || (Find(header, OrderIdHeaders) >= 0 && Find(header, EmailHeaders) < 0);
            for (var r = 1; r < rows.Count && r <= MaxRows; r++)
            {
                var row = rows[r];
                if (row.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                if (isOrders)
                {
                    orders.Add(new ArchiveOrderRow(
                        r + 1, Cell(row, header, OrderIdHeaders), Cell(row, header, IdHeaders), ParseDecimal(Cell(row, header, TotalHeaders)),
                        Cell(row, header, CurrencyHeaders), Cell(row, header, StatusHeaders), ParseDate(Cell(row, header, DateHeaders))));
                }
                else
                {
                    customers.Add(new ArchiveCustomerRow(
                        r + 1, Cell(row, header, IdHeaders), Cell(row, header, EmailHeaders), Cell(row, header, PhoneHeaders), Cell(row, header, NameHeaders),
                        ParseDate(Cell(row, header, DateHeaders)), (int)Math.Clamp(ParseDecimal(Cell(row, header, StarsHeaders)), 0m, int.MaxValue)));
                }
            }
        }

        return new ParsedArchive(customers, orders);
    }

    private static List<(string Name, List<string[]> Rows)> ReadWorkbook(Stream content)
    {
        using var workbook = new XLWorkbook(content);
        var result = new List<(string, List<string[]>)>();
        foreach (var sheet in workbook.Worksheets)
        {
            var used = sheet.RangeUsed();
            if (used is null)
            {
                continue;
            }

            var rows = new List<string[]>();
            var columns = used.ColumnCount();
            foreach (var row in used.Rows())
            {
                rows.Add(Enumerable.Range(1, columns).Select(c => row.Cell(c).GetFormattedString()).ToArray());
            }

            result.Add((sheet.Name, rows));
        }

        return result;
    }

    private static (string, List<string[]>) ReadCsv(Stream content)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var rows = new List<string[]>();
        var field = new StringBuilder();
        var current = new List<string>();
        var quoted = false;
        int ch;
        while ((ch = reader.Read()) != -1)
        {
            var c = (char)ch;
            if (quoted)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        field.Append('"');
                        reader.Read();
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"': quoted = true; break;
                case ',': current.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    current.Add(field.ToString());
                    field.Clear();
                    rows.Add([.. current]);
                    current.Clear();
                    if (rows.Count > MaxRows + 1)
                    {
                        return ("csv", rows);
                    }

                    break;
                default: field.Append(c); break;
            }
        }

        if (field.Length > 0 || current.Count > 0)
        {
            current.Add(field.ToString());
            rows.Add([.. current]);
        }

        return ("csv", rows);
    }

    private static int Find(List<string> header, string[] names) => header.FindIndex(h => names.Contains(h));

    private static string? Cell(string[] row, List<string> header, string[] names)
    {
        var index = Find(header, names);
        return index >= 0 && index < row.Length && !string.IsNullOrWhiteSpace(row[index]) ? row[index].Trim() : null;
    }

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;
}
