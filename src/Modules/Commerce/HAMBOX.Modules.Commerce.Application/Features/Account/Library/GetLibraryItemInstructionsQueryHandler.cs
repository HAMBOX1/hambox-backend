using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Library;

internal sealed class GetLibraryItemInstructionsQueryHandler(
    ICommerceDbContext commerceDbContext,
    ICatalogDbContext catalogDbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetLibraryItemInstructionsQuery, Result<LibraryItemInstructionsDto>>
{
    public async Task<Result<LibraryItemInstructionsDto>> Handle(
        GetLibraryItemInstructionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            return Result.Failure<LibraryItemInstructionsDto>(CommerceErrors.InstructionsNotAccessible);
        }

        // Every failure below collapses to the same error — order missing, not owned by this
        // customer, not completed, or instructions unpublished all read as "not accessible" to
        // avoid leaking which specific check failed.
        var orderItem = await (
            from item in commerceDbContext.OrderItems.AsNoTracking()
            join order in commerceDbContext.Orders.AsNoTracking() on item.OrderId equals order.Id
            where item.Id == request.OrderItemId
                && order.UserId == currentUserService.UserId
                && order.Status == OrderStatus.Completed
            select new { item.ProductId, item.ProductVariantId })
            .FirstOrDefaultAsync(cancellationToken);

        if (orderItem?.ProductId is null)
        {
            return Result.Failure<LibraryItemInstructionsDto>(CommerceErrors.InstructionsNotAccessible);
        }

        // Prefer instructions authored for the specific variant purchased; fall back to the
        // product's general instructions when the variant has none of its own.
        var instructions = orderItem.ProductVariantId is not null
            ? await catalogDbContext.ProductInstructions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    i => i.ProductId == orderItem.ProductId && i.VariantId == orderItem.ProductVariantId && i.IsPublished,
                    cancellationToken)
            : null;

        instructions ??= await catalogDbContext.ProductInstructions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                i => i.ProductId == orderItem.ProductId && i.VariantId == null && i.IsPublished,
                cancellationToken);

        if (instructions is null)
        {
            return Result.Failure<LibraryItemInstructionsDto>(CommerceErrors.InstructionsNotAccessible);
        }

        return Result.Success(new LibraryItemInstructionsDto(
            instructions.Title,
            instructions.ContentHtml,
            instructions.Version,
            instructions.ModifiedOnUtc ?? instructions.CreatedOnUtc));
    }
}
