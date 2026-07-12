using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.GetRoleById;

internal sealed class GetRoleByIdQueryValidator : AbstractValidator<GetRoleByIdQuery>
{
    public GetRoleByIdQueryValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
    }
}
