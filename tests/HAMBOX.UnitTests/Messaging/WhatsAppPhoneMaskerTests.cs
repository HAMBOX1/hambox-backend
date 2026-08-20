using HAMBOX.Modules.Messaging.Application.Services;

namespace HAMBOX.UnitTests.Messaging;

/// <summary>Phone numbers are PII and must never reach a log in full — see the masking calls in
/// <c>FakeWhatsAppProvider</c> and <c>WhatsAppWebhookEndpoints</c>' error log.</summary>
public sealed class WhatsAppPhoneMaskerTests
{
    [Fact]
    public void Mask_KeepsOnlyLastFourDigits()
    {
        var masked = WhatsAppPhoneMasker.Mask("+201234567890");

        Assert.EndsWith("7890", masked);
        Assert.DoesNotContain("1234", masked);
        Assert.Equal("+201234567890".Length, masked.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Mask_NullOrWhitespace_ReturnsEmpty(string? input) =>
        Assert.Equal(string.Empty, WhatsAppPhoneMasker.Mask(input));

    [Fact]
    public void Mask_ShortNumber_IsFullyMasked() =>
        Assert.Equal("***", WhatsAppPhoneMasker.Mask("123"));
}
