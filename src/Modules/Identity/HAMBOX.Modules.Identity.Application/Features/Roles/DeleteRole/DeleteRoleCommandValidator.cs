using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.DeleteRole;

internal sealed class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
    }
}
