using System.Text.Json;
using FluentValidation;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.Modules.Suppliers.Application.Validation;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Suppliers.Application.Features.Suppliers;

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Request.ProviderType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.BaseUrl).Custom((url, context) =>
        {
            if (!SupplierBaseUrlValidator.IsAllowed(url, out var error))
            {
                context.AddFailure(error!);
            }
        });
    }
}

public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.ProviderType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.BaseUrl).Custom((url, context) =>
        {
            if (!SupplierBaseUrlValidator.IsAllowed(url, out var error))
            {
                context.AddFailure(error!);
            }
        });
    }
}

/// <summary>
/// Provider-aware guard for the one Bamboo setting that actually matters at purchase time:
/// <c>BambooSupplierProvider.PurchaseAsync</c> (Infrastructure layer, not referenced here) fails closed
/// with <c>InvalidConfiguration</c> when <c>SettingsJson</c> has no positive numeric "accountId" — this
/// validator surfaces that same requirement to the admin at save time instead of
/// only at first purchase attempt. A no-op for every other <c>ProviderType</c>: the generic
/// <c>SettingsJson</c> mechanism stays freeform JSON for everyone else, per the Platform-Settings-style
/// "JSON blob per category" convention this module mirrors.
/// </summary>
public sealed class UpdateSupplierSettingsCommandValidator : AbstractValidator<UpdateSupplierSettingsCommand>
{
    private const string BambooProviderType = "Bamboo";

    public UpdateSupplierSettingsCommandValidator(ISuppliersDbContext dbContext)
    {
        RuleFor(x => x).CustomAsync(async (command, context, cancellationToken) =>
        {
            var providerType = await dbContext.Suppliers.AsNoTracking()
                .Where(s => s.Id == command.Id)
                .Select(s => s.ProviderType)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.Equals(providerType, BambooProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var settingsJson = command.Request.SettingsJson;
            if (string.IsNullOrWhiteSpace(settingsJson))
            {
                context.AddFailure(nameof(UpdateSupplierSettingsRequest.SettingsJson), "Bamboo requires a Default Account ID.");
                return;
            }

            try
            {
                using var document = JsonDocument.Parse(settingsJson);
                var isValidAccountId = document.RootElement.TryGetProperty("accountId", out var accountId)
                    && accountId.ValueKind == JsonValueKind.Number
                    && accountId.TryGetInt64(out var accountIdValue)
                    && accountIdValue > 0;

                if (!isValidAccountId)
                {
                    context.AddFailure(nameof(UpdateSupplierSettingsRequest.SettingsJson), "Bamboo requires a positive numeric \"accountId\".");
                }
            }
            catch (JsonException)
            {
                context.AddFailure(nameof(UpdateSupplierSettingsRequest.SettingsJson), "Settings must be valid JSON.");
            }
        });
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

public sealed class UpdateSupplierMappingPriorityCommandValidator : AbstractValidator<UpdateSupplierMappingPriorityCommand>
{
    public UpdateSupplierMappingPriorityCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.MappingId).NotEmpty();
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
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
