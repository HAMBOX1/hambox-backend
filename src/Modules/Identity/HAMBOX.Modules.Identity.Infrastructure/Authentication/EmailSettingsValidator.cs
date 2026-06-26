using System.Net.Mail;
using HAMBOX.Modules.Identity.Application.Options;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Validates email settings at application startup.
/// </summary>
internal sealed class EmailSettingsValidator : IValidateOptions<EmailSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, EmailSettings options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.SmtpHost))
        {
            return ValidateOptionsResult.Fail("EmailSettings:SmtpHost is required when email delivery is enabled.");
        }

        if (options.SmtpPort is < 1 or > 65535)
        {
            return ValidateOptionsResult.Fail("EmailSettings:SmtpPort must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            return ValidateOptionsResult.Fail("EmailSettings:FromAddress is required when email delivery is enabled.");
        }

        try
        {
            _ = new MailAddress(options.FromAddress);
        }
        catch (FormatException)
        {
            return ValidateOptionsResult.Fail("EmailSettings:FromAddress must be a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(options.ApplicationBaseUrl))
        {
            return ValidateOptionsResult.Fail(
                "EmailSettings:ApplicationBaseUrl is required when email delivery is enabled.");
        }

        if (!Uri.TryCreate(options.ApplicationBaseUrl, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("EmailSettings:ApplicationBaseUrl must be an absolute URI.");
        }

        return ValidateOptionsResult.Success;
    }
}
