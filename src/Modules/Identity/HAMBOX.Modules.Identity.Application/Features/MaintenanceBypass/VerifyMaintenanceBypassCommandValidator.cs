using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.MaintenanceBypass;

internal sealed class VerifyMaintenanceBypassCommandValidator : AbstractValidator<VerifyMaintenanceBypassCommand>
{
    public VerifyMaintenanceBypassCommandValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
    }
}
