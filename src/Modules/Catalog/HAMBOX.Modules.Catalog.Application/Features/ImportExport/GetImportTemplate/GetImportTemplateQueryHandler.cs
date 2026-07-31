using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetImportLookups;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetImportTemplate;

internal sealed class GetImportTemplateQueryHandler(IImportTemplateGenerator generator, ISender sender)
    : IRequestHandler<GetImportTemplateQuery, Result<ImportTemplateFileDto>>
{
    public async Task<Result<ImportTemplateFileDto>> Handle(GetImportTemplateQuery request, CancellationToken cancellationToken)
    {
        if (request.EntityType == CatalogImportEntityType.FullPackage)
        {
            return Result.Failure<ImportTemplateFileDto>(CatalogErrors.UnsupportedPackageFormat);
        }

        var lookupsResult = await sender.Send(new GetCatalogImportLookupsQuery(), cancellationToken);
        if (lookupsResult.IsFailure)
        {
            return Result.Failure<ImportTemplateFileDto>(lookupsResult.Error);
        }

        var bytes = generator.Generate(request.EntityType, lookupsResult.Value);
        var fileName = $"{request.EntityType}-template.xlsx";

        return Result.Success(new ImportTemplateFileDto(
            fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", bytes));
    }
}
