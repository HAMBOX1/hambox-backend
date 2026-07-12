using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.SetRolePermissions;

internal sealed class SetRolePermissionsCommandValidator : AbstractValidator<SetRolePermissionsCommand>
{
    public SetRolePermissionsCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.PermissionIds).NotNull();
    }
}
