namespace HAMBOX.Modules.Identity.Application.Options;

/// <summary>
/// Configuration for admin login email OTP. Defaults follow the scope: valid 5–10 minutes, 3 attempts, 60 s resend cool-down.
/// </summary>
public sealed class AdminOtpSettings
{
    public const string SectionName = "AdminOtpSettings";

    public int CodeLength { get; set; } = 6;

    public int ExpirationMinutes { get; set; } = 5;

    public int MaxAttempts { get; set; } = 3;

    public int ResendCooldownSeconds { get; set; } = 60;

    public int LockoutMinutes { get; set; } = 15;
}
