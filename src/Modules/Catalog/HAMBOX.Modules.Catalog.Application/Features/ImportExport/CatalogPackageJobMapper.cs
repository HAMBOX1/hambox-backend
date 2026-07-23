using System.Text.Json;
using HAMBOX.Modules.Catalog.Domain.Packaging;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport;

internal static class CatalogPackageJobMapper
{
    public static CatalogPackageJobDto ToDto(CatalogPackageJob job) => new(
        job.Id,
        job.Direction,
        job.Format,
        job.EntityType,
        job.Status,
        job.ProgressPercent,
        job.FileName.Length == 0 ? null : job.FileName,
        job.ResultFileName,
        job.SummaryJson is null ? null : JsonSerializer.Deserialize<CatalogPackageSummary>(job.SummaryJson),
        job.ErrorMessage);
}
