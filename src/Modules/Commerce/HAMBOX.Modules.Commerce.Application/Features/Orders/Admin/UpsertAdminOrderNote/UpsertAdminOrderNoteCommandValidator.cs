using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.UpsertAdminOrderNote;

internal sealed class UpsertAdminOrderNoteCommandValidator : AbstractValidator<UpsertAdminOrderNoteCommand>
{
    public UpsertAdminOrderNoteCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Request.Body).NotEmpty().MaximumLength(4000);
    }
}
