using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Reports;
using HAMBOX.Modules.Commerce.Domain.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services.Reports;

internal sealed class ReportDocumentGenerator(IPlatformSettingsProvider platformSettings) : IReportDocumentGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    static ReportDocumentGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<ReportDocumentResult> GenerateAsync(
        ReportDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var format = (request.Format ?? ReportFormats.Pdf).Trim().ToLowerInvariant();
        var stamp = request.Model.GeneratedOnUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var safeType = SanitizeFilePart(request.Model.ReportType);

        return format switch
        {
            "xlsx" or "excel" => BuildExcel(request.Model, $"{safeType}-{stamp}.xlsx"),
            "csv" => BuildCsv(request.Model, $"{safeType}-{stamp}.csv"),
            "json" => BuildJson(request.Model, $"{safeType}-{stamp}.json"),
            _ => await BuildPdfAsync(request.Model, $"{safeType}-{stamp}.pdf", cancellationToken),
        };
    }

    private async Task<ReportDocumentResult> BuildPdfAsync(
        ReportModel model,
        string fileName,
        CancellationToken cancellationToken)
    {
        var branding = await platformSettings.GetBrandingAsync(cancellationToken);
        var general = await platformSettings.GetGeneralAsync(cancellationToken);
        var brand = string.IsNullOrWhiteSpace(model.BrandName)
            ? (string.IsNullOrWhiteSpace(general.StoreName) ? "HAMBOX" : general.StoreName)
            : model.BrandName;
        var accent = string.IsNullOrWhiteSpace(branding.PrimaryColor) ? "#2563EB" : branding.PrimaryColor;

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(brand).SemiBold().FontSize(16).FontColor(accent);
                    col.Item().Text(model.Title).FontSize(14).SemiBold();
                    col.Item().Text($"Generated {model.GeneratedOnUtc:u} UTC").FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                    text.Span($" 뿯½ {brand}");
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(12);

                    if (model.FiltersSummary.Count > 0)
                    {
                        col.Item().Text("Filters").SemiBold();
                        foreach (var filter in model.FiltersSummary)
                        {
                            col.Item().Text($"{filter.Key}: {filter.Value}").FontSize(9);
                        }
                    }

                    if (model.Totals is { Count: > 0 })
                    {
                        col.Item().Text("Totals").SemiBold();
                        foreach (var kpi in model.Totals)
                        {
                            col.Item().Text($"{kpi.Label}: {kpi.Value}{(kpi.Unit is null ? string.Empty : " " + kpi.Unit)}");
                        }
                    }

                    foreach (var section in model.Sections)
                    {
                        col.Item().PaddingTop(6).Text(section.Title).FontSize(12).SemiBold().FontColor(accent);

                        if (section.Kpis is { Count: > 0 })
                        {
                            foreach (var kpi in section.Kpis)
                            {
                                col.Item().Text($"{kpi.Label}: {kpi.Value}");
                            }
                        }

                        if (section.Chart is { Labels.Count: > 0 })
                        {
                            col.Item().Text(section.Chart.Title).FontSize(10).SemiBold();
                            var max = section.Chart.Values.DefaultIfEmpty(0).Max();
                            if (max <= 0) max = 1;
                            for (var i = 0; i < section.Chart.Labels.Count; i++)
                            {
                                var value = section.Chart.Values[i];
                                var width = (float)(Math.Min(1d, (double)(value / max)) * 250d);
                                col.Item().Row(row =>
                                {
                                    row.ConstantItem(90).Text(section.Chart.Labels[i]).FontSize(8);
                                    row.RelativeItem().Height(10).Background(Colors.Grey.Lighten3)
                                        .AlignLeft().Width(width).Background(accent);
                                    row.ConstantItem(50).AlignRight().Text(value.ToString("0.##", CultureInfo.InvariantCulture)).FontSize(8);
                                });
                            }
                        }

                        if (section.Table is not null)
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    foreach (var _ in section.Table.Columns)
                                    {
                                        columns.RelativeColumn();
                                    }
                                });

                                table.Header(header =>
                                {
                                    foreach (var column in section.Table.Columns)
                                    {
                                        header.Cell().Background(accent).Padding(4)
                                            .Text(column.Header).FontColor(Colors.White).FontSize(8).SemiBold();
                                    }
                                });

                                foreach (var row in section.Table.Rows.Take(200))
                                {
                                    foreach (var column in section.Table.Columns)
                                    {
                                        row.Cells.TryGetValue(column.Key, out var cell);
                                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                                            .Text(cell ?? string.Empty).FontSize(8);
                                    }
                                }
                            });
                        }
                    }
                });
            });
        }).GeneratePdf();

        return new ReportDocumentResult(bytes, "application/pdf", fileName);
    }

    private static ReportDocumentResult BuildExcel(ReportModel model, string fileName)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = model.Title;
        summary.Cell(1, 1).Style.Font.Bold = true;
        summary.Cell(2, 1).Value = "Generated (UTC)";
        summary.Cell(2, 2).Value = model.GeneratedOnUtc.ToString("u", CultureInfo.InvariantCulture);

        var row = 4;
        summary.Cell(row, 1).Value = "Filters";
        summary.Cell(row, 1).Style.Font.Bold = true;
        row++;
        foreach (var filter in model.FiltersSummary)
        {
            summary.Cell(row, 1).Value = filter.Key;
            summary.Cell(row, 2).Value = filter.Value;
            row++;
        }

        row += 1;
        if (model.Totals is { Count: > 0 })
        {
            summary.Cell(row, 1).Value = "Totals";
            summary.Cell(row, 1).Style.Font.Bold = true;
            row++;
            foreach (var kpi in model.Totals)
            {
                summary.Cell(row, 1).Value = kpi.Label;
                summary.Cell(row, 2).Value = kpi.Value;
                row++;
            }
        }

        var sheetIndex = 1;
        foreach (var section in model.Sections)
        {
            var sheetName = SanitizeSheetName($"{sheetIndex}-{section.Title}");
            var sheet = workbook.Worksheets.Add(sheetName);
            var r = 1;
            sheet.Cell(r, 1).Value = section.Title;
            sheet.Cell(r, 1).Style.Font.Bold = true;
            r += 2;

            if (section.Kpis is { Count: > 0 })
            {
                sheet.Cell(r, 1).Value = "KPI";
                sheet.Cell(r, 2).Value = "Value";
                sheet.Range(r, 1, r, 2).Style.Font.Bold = true;
                r++;
                foreach (var kpi in section.Kpis)
                {
                    sheet.Cell(r, 1).Value = kpi.Label;
                    sheet.Cell(r, 2).Value = kpi.Value;
                    r++;
                }

                r++;
            }

            if (section.Table is not null)
            {
                for (var c = 0; c < section.Table.Columns.Count; c++)
                {
                    sheet.Cell(r, c + 1).Value = section.Table.Columns[c].Header;
                    sheet.Cell(r, c + 1).Style.Font.Bold = true;
                    sheet.Cell(r, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
                    sheet.Cell(r, c + 1).Style.Font.FontColor = XLColor.White;
                }

                r++;
                foreach (var dataRow in section.Table.Rows)
                {
                    for (var c = 0; c < section.Table.Columns.Count; c++)
                    {
                        dataRow.Cells.TryGetValue(section.Table.Columns[c].Key, out var cell);
                        sheet.Cell(r, c + 1).Value = cell ?? string.Empty;
                    }

                    r++;
                }

                if (section.Kpis is { Count: > 0 })
                {
                    sheet.Cell(r, 1).Value = "Section totals";
                    sheet.Cell(r, 1).Style.Font.Bold = true;
                }
            }

            if (section.Chart is { Labels.Count: > 0 })
            {
                r += 2;
                sheet.Cell(r, 1).Value = section.Chart.Title;
                sheet.Cell(r, 1).Style.Font.Bold = true;
                r++;
                sheet.Cell(r, 1).Value = "Label";
                sheet.Cell(r, 2).Value = "Value";
                sheet.Range(r, 1, r, 2).Style.Font.Bold = true;
                r++;
                for (var i = 0; i < section.Chart.Labels.Count; i++)
                {
                    sheet.Cell(r, 1).Value = section.Chart.Labels[i];
                    sheet.Cell(r, 2).Value = section.Chart.Values[i];
                    r++;
                }
            }

            sheet.Columns().AdjustToContents();
            sheetIndex++;
        }

        summary.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ReportDocumentResult(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private static ReportDocumentResult BuildCsv(ReportModel model, string fileName)
    {
        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendLine(EscapeCsv("Section") + "," + EscapeCsv("Metric") + "," + EscapeCsv("Value"));
        sb.AppendLine($"{EscapeCsv("Meta")},{EscapeCsv("Title")},{EscapeCsv(model.Title)}");
        sb.AppendLine($"{EscapeCsv("Meta")},{EscapeCsv("GeneratedOnUtc")},{EscapeCsv(model.GeneratedOnUtc.ToString("u", CultureInfo.InvariantCulture))}");

        foreach (var filter in model.FiltersSummary)
        {
            sb.AppendLine($"{EscapeCsv("Filter")},{EscapeCsv(filter.Key)},{EscapeCsv(filter.Value)}");
        }

        if (model.Totals is not null)
        {
            foreach (var kpi in model.Totals)
            {
                sb.AppendLine($"{EscapeCsv("Total")},{EscapeCsv(kpi.Label)},{EscapeCsv(kpi.Value)}");
            }
        }

        foreach (var section in model.Sections)
        {
            if (section.Kpis is not null)
            {
                foreach (var kpi in section.Kpis)
                {
                    sb.AppendLine($"{EscapeCsv(section.Title)},{EscapeCsv(kpi.Label)},{EscapeCsv(kpi.Value)}");
                }
            }

            if (section.Table is not null)
            {
                foreach (var row in section.Table.Rows)
                {
                    var metric = string.Join(" | ", section.Table.Columns.Select(c =>
                    {
                        row.Cells.TryGetValue(c.Key, out var cell);
                        return $"{c.Header}={cell}";
                    }));
                    sb.AppendLine($"{EscapeCsv(section.Title)},{EscapeCsv(section.Table.Title)},{EscapeCsv(metric)}");
                }
            }

            if (section.Chart is not null)
            {
                for (var i = 0; i < section.Chart.Labels.Count; i++)
                {
                    sb.AppendLine($"{EscapeCsv(section.Title)},{EscapeCsv(section.Chart.Labels[i])},{EscapeCsv(section.Chart.Values[i].ToString(CultureInfo.InvariantCulture))}");
                }
            }
        }

        return new ReportDocumentResult(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", fileName);
    }

    private static ReportDocumentResult BuildJson(ReportModel model, string fileName)
    {
        var json = JsonSerializer.Serialize(model, JsonOptions);
        return new ReportDocumentResult(Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static string SanitizeFilePart(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        return new string(chars).Trim('-').ToLowerInvariant();
    }

    private static string SanitizeSheetName(string value)
    {
        var invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray());
        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }
}
