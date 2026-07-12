using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.AssignUsersToRole;

internal sealed class AssignUsersToRoleCommandValidator : AbstractValidator<AssignUsersToRoleCommand>
{
    public AssignUsersToRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.UserIds).NotNull().NotEmpty();
        RuleForEach(x => x.UserIds).NotEmpty();
    }
}
