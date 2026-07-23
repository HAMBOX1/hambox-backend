using FluentValidation;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.ExecuteCatalogImport;

public sealed class ExecuteCatalogImportCommandValidator : AbstractValidator<ExecuteCatalogImportCommand>
{
    public ExecuteCatalogImportCommandValidator()
    {
        RuleFor(x => x.UploadId).NotEmpty();
        RuleFor(x => x.Options).NotNull();
        RuleFor(x => x.Strategy).IsInEnum();
    }
}
