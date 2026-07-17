using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedIps;

internal sealed class CreateBlockedIpCommandValidator : AbstractValidator<CreateBlockedIpCommand>
{
    public CreateBlockedIpCommandValidator()
    {
        RuleFor(x => x.CidrOrAddress)
            .NotEmpty()
            .MaximumLength(64)
            .Must(BeAValidIpOrCidr)
            .WithMessage("Value must be a valid IP address or CIDR range.");

        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x.ExpiresOnUtc)
            .GreaterThan(DateTimeOffset.UtcNow)
            .When(x => x.ExpiresOnUtc.HasValue)
            .WithMessage("Expiration must be in the future.");
    }

    private static bool BeAValidIpOrCidr(string value)
    {
        var normalized = value.Trim();
        if (normalized.Contains('/'))
        {
            return System.Net.IPNetwork.TryParse(normalized, out _);
        }

        return System.Net.IPAddress.TryParse(normalized, out _);
    }
}
