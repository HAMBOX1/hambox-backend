using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Categories.UpdateTicketCategory;

internal sealed class UpdateTicketCategoryCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<UpdateTicketCategoryCommand, Result>
{
    public async Task<Result> Handle(UpdateTicketCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await dbContext.TicketCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure(SupportErrors.CategoryNotFound);
        }

        category.Update(request.Name, request.Color, request.Icon, request.SortOrder, request.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
