using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

/// <summary>
/// Hides (or shows again) an option value without deleting it: every variant that uses the option, or an option
/// nested under it, is deactivated (or reactivated). This is the safe way out when deleting is refused because some
/// of those variants already have sales history — customers stop seeing the option and nothing is lost.
/// Archived variants are left alone.
/// </summary>
public sealed record SetOptionVisibilityCommand(Guid OptionId, bool Visible) : IRequest<Result>;

internal sealed class SetOptionVisibilityCommandHandler(ICatalogDbContext db)
    : IRequestHandler<SetOptionVisibilityCommand, Result>
{
    public async Task<Result> Handle(SetOptionVisibilityCommand request, CancellationToken cancellationToken)
    {
        if (!await db.ProductOptions.AnyAsync(o => o.Id == request.OptionId, cancellationToken))
        {
            return Result.Failure(CatalogErrors.OptionNotFound);
        }

        var optionIds = (await OptionGroupSubtreeHelper.CollectDescendantOptionIdsAsync(db, request.OptionId, cancellationToken))
            .Append(request.OptionId)
            .ToList();

        var variantIds = await db.ProductVariantOptions
            .Where(vo => optionIds.Contains(vo.OptionId))
            .Select(vo => vo.VariantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id) && !v.IsDeleted && v.Status != ProductVariantStatus.Archived)
            .ToListAsync(cancellationToken);

        foreach (var variant in variants)
        {
            if (request.Visible)
            {
                variant.Activate();
            }
            else
            {
                variant.Deactivate();
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
