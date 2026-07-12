namespace HAMBOX.Modules.Identity.Application.Options;

/// <summary>
/// Configuration for admin login email OTP.
/// </summary>
public sealed class AdminOtpSettings
{
    public const string SectionName = "AdminOtpSettings";

    public int CodeLength { get; set; } = 6;

    public int ExpirationMinutes { get; set; } = 5;

    public int MaxAttempts { get; set; } = 5;

    public int ResendCooldownSeconds { get; set; } = 30;

    public int LockoutMinutes { get; set; } = 15;
}
