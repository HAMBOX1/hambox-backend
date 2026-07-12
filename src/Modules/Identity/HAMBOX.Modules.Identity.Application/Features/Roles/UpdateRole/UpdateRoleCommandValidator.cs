using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.UpdateRole;

internal sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
        RuleFor(x => x.PriorityLevel).GreaterThanOrEqualTo(0).When(x => x.PriorityLevel.HasValue);
    }
}
