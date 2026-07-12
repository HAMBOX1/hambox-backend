using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.UpsertAdminOrderNote;

public sealed record UpsertAdminOrderNoteCommand(Guid OrderId, Guid? NoteId, UpsertAdminOrderNoteRequest Request)
    : IRequest<Result<AdminOrderAdminNoteDto>>;

internal sealed class UpsertAdminOrderNoteCommandHandler
    : IRequestHandler<UpsertAdminOrderNoteCommand, Result<AdminOrderAdminNoteDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public UpsertAdminOrderNoteCommandHandler(
        ICommerceDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<AdminOrderAdminNoteDto>> Handle(
        UpsertAdminOrderNoteCommand command,
        CancellationToken cancellationToken)
    {
        var orderExists = await _dbContext.Orders
            .AsNoTracking()
            .AnyAsync(o => o.Id == command.OrderId, cancellationToken);

        if (!orderExists)
        {
            return Result.Failure<AdminOrderAdminNoteDto>(CommerceErrors.OrderNotFound);
        }

        var actorId = _currentUserService.UserId ?? "system";
        var actorName = actorId;

        OrderAdminNote? note = null;
        if (command.NoteId is Guid noteId)
        {
            note = await _dbContext.OrderAdminNotes
                .FirstOrDefaultAsync(n => n.Id == noteId && n.OrderId == command.OrderId, cancellationToken);

            if (note is null)
            {
                return Result.Failure<AdminOrderAdminNoteDto>(CommerceErrors.OrderNoteNotFound);
            }

            note.UpdateBody(command.Request.Body);
        }
        else
        {
            note = OrderAdminNote.Create(command.OrderId, command.Request.Body, actorId, actorName);
            _dbContext.OrderAdminNotes.Add(note);
        }

        _dbContext.OrderAuditEntries.Add(OrderAuditEntry.Create(
            command.OrderId,
            command.NoteId.HasValue ? "NoteUpdated" : "NoteAdded",
            command.NoteId.HasValue ? "An internal admin note was updated." : "An internal admin note was added.",
            actorId,
            actorName));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AdminOrderAdminNoteDto(
            note.Id,
            note.Body,
            note.AuthorDisplayName,
            note.CreatedOnUtc,
            note.ModifiedOnUtc));
    }
}
