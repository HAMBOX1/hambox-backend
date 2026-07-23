using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetImportTemplate;

public sealed record ImportTemplateFileDto(string FileName, string ContentType, byte[] Content);

public sealed record GetImportTemplateQuery(CatalogImportEntityType EntityType) : IRequest<Result<ImportTemplateFileDto>>;
