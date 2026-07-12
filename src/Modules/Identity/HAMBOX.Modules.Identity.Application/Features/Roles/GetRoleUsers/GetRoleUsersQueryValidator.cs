using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.GetRoleUsers;

internal sealed class GetRoleUsersQueryValidator : AbstractValidator<GetRoleUsersQuery>
{
    public GetRoleUsersQueryValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
