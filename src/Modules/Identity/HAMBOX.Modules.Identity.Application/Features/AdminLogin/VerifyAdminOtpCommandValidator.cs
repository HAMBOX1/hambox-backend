using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.AdminLogin;

internal sealed class VerifyAdminOtpCommandValidator : AbstractValidator<VerifyAdminOtpCommand>
{
    public VerifyAdminOtpCommandValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Matches(@"^\d{6}$");
        RuleFor(x => x.IpAddress).NotEmpty();
        RuleFor(x => x.UserAgent).NotEmpty();
    }
}
