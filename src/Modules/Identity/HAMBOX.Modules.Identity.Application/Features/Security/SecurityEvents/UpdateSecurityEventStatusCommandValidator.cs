using FluentValidation;
using HAMBOX.Modules.Identity.Domain.Enums;

namespace HAMBOX.Modules.Identity.Application.Features.Security.SecurityEvents;

internal sealed class UpdateSecurityEventStatusCommandValidator : AbstractValidator<UpdateSecurityEventStatusCommand>
{
    public UpdateSecurityEventStatusCommandValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.Status)
            .IsInEnum()
            .NotEqual(SecurityEventStatus.Open)
            .WithMessage("Status must be Acknowledged, Dismissed, or Resolved.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
