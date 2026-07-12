namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Generates cryptographically secure numeric OTP codes.
/// </summary>
public interface IOtpCodeGenerator
{
    /// <summary>
    /// Generates a numeric OTP code with the specified length.
    /// </summary>
    string GenerateNumericCode(int length);
}
