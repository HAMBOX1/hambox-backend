using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tags.CreateTicketTag;

internal sealed class CreateTicketTagCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<CreateTicketTagCommand, Result<TicketTagDto>>
{
    public async Task<Result<TicketTagDto>> Handle(CreateTicketTagCommand request, CancellationToken cancellationToken)
    {
        var exists = await dbContext.TicketTags.AnyAsync(t => t.Name == request.Name.Trim(), cancellationToken);
        if (exists)
        {
            return Result.Failure<TicketTagDto>(SupportErrors.TagAlreadyExists);
        }

        var tag = TicketTag.Create(request.Name, request.Color);
        dbContext.TicketTags.Add(tag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(SupportMapper.ToDto(tag));
    }
}
