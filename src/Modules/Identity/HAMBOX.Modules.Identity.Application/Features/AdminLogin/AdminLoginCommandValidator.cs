using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.AdminLogin;

internal sealed class AdminLoginCommandValidator : AbstractValidator<AdminLoginCommand>
{
    public AdminLoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.IpAddress).NotEmpty();
        RuleFor(x => x.UserAgent).NotEmpty();
    }
}
