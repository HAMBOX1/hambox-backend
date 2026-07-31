using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.ValidateCatalogImport;

/// <summary>
/// Re-parses the stored upload and matches it against the DB — fully stateless, so calling this
/// again after the wizard's inline "apply to all" corrections IS the revalidation step; no separate
/// endpoint or persisted staging row exists (see <see cref="CatalogImportCorrectionApplier"/>).
/// </summary>
public sealed record ValidateCatalogImportQuery(
    Guid UploadId,
    string? PackagePassword,
    CatalogSkuStrategy SkuStrategy = CatalogSkuStrategy.UseImportedSku,
    IReadOnlyList<CatalogImportCorrection>? Corrections = null) : IRequest<Result<CatalogImportValidationReport>>;
