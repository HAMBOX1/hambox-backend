using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Categories.DeleteTicketCategory;

internal sealed class DeleteTicketCategoryCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<DeleteTicketCategoryCommand, Result>
{
    public async Task<Result> Handle(DeleteTicketCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await dbContext.TicketCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure(SupportErrors.CategoryNotFound);
        }

        category.Delete();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
