using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Categories.CreateTicketCategory;

internal sealed class CreateTicketCategoryCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<CreateTicketCategoryCommand, Result<TicketCategoryDto>>
{
    public async Task<Result<TicketCategoryDto>> Handle(CreateTicketCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = TicketCategory.Create(request.Name, request.Color, request.Icon, request.SortOrder);
        dbContext.TicketCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(SupportMapper.ToDto(category));
    }
}
