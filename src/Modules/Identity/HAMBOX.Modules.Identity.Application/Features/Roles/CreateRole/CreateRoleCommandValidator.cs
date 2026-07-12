using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.CreateRole;

internal sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
        RuleFor(x => x.PriorityLevel).GreaterThanOrEqualTo(0).When(x => x.PriorityLevel.HasValue);
    }
}
