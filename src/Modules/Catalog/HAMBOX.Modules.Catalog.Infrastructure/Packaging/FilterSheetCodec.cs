using System.Text;
using ClosedXML.Excel;
using HAMBOX.Modules.Catalog.Application.Abstractions;

namespace HAMBOX.Modules.Catalog.Infrastructure.Packaging;

/// <summary>Excel (.xlsx) and CSV reading / writing for the filter sheets.</summary>
internal sealed class FilterSheetCodec : IFilterSheetCodec
{
    private const int MaxRows = 50_000;
    private const int MaxColumns = 60;

    public FilterSheet Read(Stream stream, string fileName)
    {
        var name = fileName.ToLowerInvariant();
        if (name.EndsWith(".xlsx", StringComparison.Ordinal))
        {
            return ReadExcel(stream);
        }

        if (name.EndsWith(".csv", StringComparison.Ordinal) || name.EndsWith(".txt", StringComparison.Ordinal))
        {
            return ReadCsv(stream);
        }

        throw new InvalidDataException("Upload an .xlsx or .csv file.");
    }

    public byte[] Write(FilterSheet sheet, string format) =>
        string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? WriteCsv(sheet) : WriteExcel(sheet);

    private static FilterSheet ReadExcel(Stream stream)
    {
        try
        {
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();
            var used = worksheet.RangeUsed();
            if (used is null)
            {
                return new FilterSheet([], []);
            }

            var columns = Math.Min(used.ColumnCount(), MaxColumns);
            var firstRow = used.FirstRow().RowNumber();
            var header = Enumerable.Range(1, columns).Select(c => worksheet.Cell(firstRow, c).GetString().Trim()).ToList();
            var rows = new List<IReadOnlyList<string>>();
            for (var r = firstRow + 1; r <= used.LastRow().RowNumber() && rows.Count < MaxRows; r++)
            {
                var cells = Enumerable.Range(1, columns).Select(c => worksheet.Cell(r, c).GetString().Trim()).ToList();
                if (cells.Any(c => c.Length > 0))
                {
                    rows.Add(cells);
                }
            }

            return new FilterSheet(header, rows);
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException("The Excel file could not be read.", ex);
        }
    }

    private static FilterSheet ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var records = ParseCsv(text);
        if (records.Count == 0)
        {
            return new FilterSheet([], []);
        }

        var header = records[0].Take(MaxColumns).Select(c => c.Trim()).ToList();
        var rows = records.Skip(1)
            .Where(r => r.Any(c => c.Trim().Length > 0))
            .Take(MaxRows)
            .Select(r => (IReadOnlyList<string>)Enumerable.Range(0, header.Count).Select(i => i < r.Count ? r[i].Trim() : string.Empty).ToList())
            .ToList();
        return new FilterSheet(header, rows);
    }

    /// <summary>RFC 4180 style CSV: quoted cells, doubled quotes, commas or semicolons, CRLF or LF.</summary>
    internal static List<List<string>> ParseCsv(string text)
    {
        var records = new List<List<string>>();
        var first = text.Split('\n', 2)[0];
        var delimiter = first.Count(c => c == ';') > first.Count(c => c == ',') ? ';' : ',';
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    cell.Append(ch);
                }

                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == delimiter)
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(cell.ToString());
                cell.Clear();
                records.Add(row);
                row = [];
            }
            else
            {
                cell.Append(ch);
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            records.Add(row);
        }

        return records;
    }

    private static byte[] WriteExcel(FilterSheet sheet)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Filters");
        for (var c = 0; c < sheet.Header.Count; c++)
        {
            var cell = worksheet.Cell(1, c + 1);
            cell.Value = sheet.Header[c];
            cell.Style.Font.Bold = true;
        }

        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            for (var c = 0; c < sheet.Rows[r].Count; c++)
            {
                // Everything is text, so ids and codes are never turned into numbers or dates by Excel.
                worksheet.Cell(r + 2, c + 1).SetValue(sheet.Rows[r][c]);
                worksheet.Cell(r + 2, c + 1).Style.NumberFormat.Format = "@";
            }
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents(1, Math.Min(sheet.Rows.Count + 1, 200));
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] WriteCsv(FilterSheet sheet)
    {
        var builder = new StringBuilder();
        builder.Append('﻿');
        builder.AppendLine(string.Join(',', sheet.Header.Select(Escape)));
        foreach (var row in sheet.Rows)
        {
            builder.AppendLine(string.Join(',', row.Select(Escape)));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string Escape(string value)
    {
        // A cell starting with = + - @ would be run as a formula when the CSV is opened in Excel; prefix it with a quote mark.
        if (value.Length > 0 && value[0] is '=' or '+' or '@')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
