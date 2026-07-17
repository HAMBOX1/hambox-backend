using FluentValidation;

namespace HAMBOX.Modules.Suppliers.Application.Features.Suppliers;

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Request.ProviderType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.BaseUrl)
            .Must(url => string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Base URL must be a valid absolute URL.");
    }
}

public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.ProviderType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.BaseUrl)
            .Must(url => string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Base URL must be a valid absolute URL.");
    }
}

public sealed class UpdateSupplierPriorityCommandValidator : AbstractValidator<UpdateSupplierPriorityCommand>
{
    public UpdateSupplierPriorityCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateSupplierMappingCommandValidator : AbstractValidator<CreateSupplierMappingCommand>
{
    public CreateSupplierMappingCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Request.InternalProductId).NotEmpty();
        RuleFor(x => x.Request.ExternalProductId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Request.BuyingPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.Priority).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateSupplierMappingCommandValidator : AbstractValidator<UpdateSupplierMappingCommand>
{
    public UpdateSupplierMappingCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.MappingId).NotEmpty();
        RuleFor(x => x.Request.ExternalProductId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Request.BuyingPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.Priority).GreaterThanOrEqualTo(0);
    }
}
