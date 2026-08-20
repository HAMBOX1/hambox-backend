using HAMBOX.Modules.Identity.Application.Abstractions;

namespace HAMBOX.UnitTests.Messaging.TestDoubles;

internal sealed class UnusedOtpCodeGenerator : IOtpCodeGenerator
{
    public string GenerateNumericCode(int length) => throw new NotSupportedException("Not needed by these tests.");
}
