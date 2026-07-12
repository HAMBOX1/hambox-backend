using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.RemoveUserFromRole;

internal sealed class RemoveUserFromRoleCommandValidator : AbstractValidator<RemoveUserFromRoleCommand>
{
    public RemoveUserFromRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
