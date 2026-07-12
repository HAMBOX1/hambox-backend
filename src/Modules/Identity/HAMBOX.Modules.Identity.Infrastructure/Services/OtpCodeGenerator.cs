using System.Security.Cryptography;
using HAMBOX.Modules.Identity.Application.Abstractions;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class OtpCodeGenerator : IOtpCodeGenerator
{
    public string GenerateNumericCode(int length)
    {
        if (length < 4 || length > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "OTP length must be between 4 and 10.");
        }

        var max = (int)Math.Pow(10, length);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString($"D{length}");
    }
}
