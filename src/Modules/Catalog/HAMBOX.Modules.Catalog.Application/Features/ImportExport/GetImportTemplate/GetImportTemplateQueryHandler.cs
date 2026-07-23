using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetImportTemplate;

internal sealed class GetImportTemplateQueryHandler(IImportTemplateGenerator generator)
    : IRequestHandler<GetImportTemplateQuery, Result<ImportTemplateFileDto>>
{
    public Task<Result<ImportTemplateFileDto>> Handle(GetImportTemplateQuery request, CancellationToken cancellationToken)
    {
        if (request.EntityType == CatalogImportEntityType.FullPackage)
        {
            return Task.FromResult(Result.Failure<ImportTemplateFileDto>(CatalogErrors.UnsupportedPackageFormat));
        }

        var bytes = generator.Generate(request.EntityType);
        var fileName = $"{request.EntityType}-template.xlsx";

        return Task.FromResult(Result.Success(new ImportTemplateFileDto(
            fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", bytes)));
    }
}
