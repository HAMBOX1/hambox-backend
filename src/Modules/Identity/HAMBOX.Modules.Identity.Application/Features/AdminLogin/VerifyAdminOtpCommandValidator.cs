using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.AdminLogin;

internal sealed class VerifyAdminOtpCommandValidator : AbstractValidator<VerifyAdminOtpCommand>
{
    public VerifyAdminOtpCommandValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();
        // A 6-digit e-mail / authenticator code, or a recovery code ("XXXXX-XXXXX") from the authenticator feature.
        RuleFor(x => x.Code).NotEmpty().Matches(@"^(\d{6}|[A-Za-z0-9]{5}-?[A-Za-z0-9]{5})$");
        RuleFor(x => x.IpAddress).NotEmpty();
        RuleFor(x => x.UserAgent).NotEmpty();
    }
}
