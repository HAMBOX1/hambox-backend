using FluentValidation;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.ExportCatalog;

public sealed class ExportCatalogCommandValidator : AbstractValidator<ExportCatalogCommand>
{
    public ExportCatalogCommandValidator()
    {
        RuleFor(x => x.Scope).NotNull();
        RuleFor(x => x.Options).NotNull();

        RuleFor(x => x.PackagePassword)
            .NotEmpty()
            .MinimumLength(6)
            .When(x => x.EncryptCodes || x.PasswordProtectPackage)
            .WithMessage("Package password must be at least 6 characters.");

        RuleFor(x => x.Scope)
            .Must(scope => scope.ExportEntireCatalog
                || (scope.ProductIds is { Count: > 0 })
                || scope.SelectAllMatching)
            .WithMessage("Select at least one product, a filter, or the entire catalog.");
    }
}
