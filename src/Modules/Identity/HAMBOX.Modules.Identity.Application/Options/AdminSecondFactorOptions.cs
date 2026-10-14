namespace HAMBOX.Modules.Identity.Application.Options;

/// <summary>
/// Operator-owned (server configuration, never Platform Settings) policy for the admin second factor.
/// </summary>
public sealed class AdminSecondFactorOptions
{
    public const string SectionName = "AdminSecondFactor";

    /// <summary>
    /// Lets an admin sign in with a password alone in Production when the e-mailed code is switched off and no
    /// authenticator app is enrolled. Off by default: Production always requires a second factor. Turn it on only
    /// as a deliberate, temporary exception (for example while the admin mailbox is unreachable), enrol an
    /// authenticator app, then turn it off again. Every bypassed sign-in is written to the admin OTP audit log.
    /// </summary>
    public bool AllowOtpBypassInProduction { get; set; }
}
