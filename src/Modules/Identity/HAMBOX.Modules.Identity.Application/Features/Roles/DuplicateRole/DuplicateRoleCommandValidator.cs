using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.DuplicateRole;

internal sealed class DuplicateRoleCommandValidator : AbstractValidator<DuplicateRoleCommand>
{
    public DuplicateRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.NewName).MaximumLength(100).When(x => x.NewName is not null);
    }
}
