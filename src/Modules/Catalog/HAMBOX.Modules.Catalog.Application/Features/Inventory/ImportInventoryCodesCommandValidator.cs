using FluentValidation;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

public sealed class ImportInventoryCodesCommandValidator : AbstractValidator<ImportInventoryCodesCommand>
{
    public const int MaxCodesPerImport = 10_000;

    public ImportInventoryCodesCommandValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.Codes).NotEmpty();
        // One request is processed synchronously (20,000 codes took ~16 s); larger files should be split.
        RuleFor(x => x.Codes.Count).LessThanOrEqualTo(MaxCodesPerImport)
            .When(x => x.Codes is not null)
            .WithMessage($"Import at most {MaxCodesPerImport:N0} codes at a time.");
    }
}
