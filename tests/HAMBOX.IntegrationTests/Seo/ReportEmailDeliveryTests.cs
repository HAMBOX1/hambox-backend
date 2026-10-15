using HAMBOX.API.Extensions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.IntegrationTests.Seo;

/// <summary>Audit R58: a scheduled report is e-mailed as an attachment, one bad address never stops the rest.</summary>
public sealed class ReportEmailDeliveryTests
{
    private sealed class RecordingEmailService : IEmailService
    {
        public List<(string To, string FileName, int Bytes, string ContentType)> Sent { get; } = [];

        public Task SendEmailWithAttachmentAsync(
            string toEmail, string subject, string htmlBody, string attachmentFileName, byte[] attachmentContent,
            string attachmentContentType, string? correlationId = null, CancellationToken cancellationToken = default)
        {
            if (toEmail.StartsWith("bad", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Mailbox unavailable");
            }

            Sent.Add((toEmail, attachmentFileName, attachmentContent.Length, attachmentContentType));
            return Task.CompletedTask;
        }

        public Task<bool> SendEmailVerificationAsync(Guid userId, string email, DateTimeOffset expiresAt, string token, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> SendPasswordResetAsync(Guid userId, string email, DateTimeOffset expiresAt, string token, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> SendAdminLoginOtpAsync(Guid userId, string email, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task SendTemplatedEmailAsync(string toEmail, string subject, string htmlBody, string? correlationId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task TheReport_IsAttached_AndOneFailingRecipientDoesNotStopTheOthers()
    {
        var email = new RecordingEmailService();
        var delivery = new ReportEmailDelivery(email, NullLogger<ReportEmailDelivery>.Instance);

        var delivered = await delivery.SendAsync(
            ["finance@example.com", "bad@example.com", "FINANCE@example.com", "owner@example.com"],
            "HAMBOX report: Sales",
            "<p>attached</p>",
            "sales.csv",
            [1, 2, 3, 4],
            "text/csv");

        Assert.Equal(2, delivered);
        Assert.All(email.Sent, s =>
        {
            Assert.Equal("sales.csv", s.FileName);
            Assert.Equal(4, s.Bytes);
            Assert.Equal("text/csv", s.ContentType);
        });
        Assert.Equal(["finance@example.com", "owner@example.com"], email.Sent.Select(s => s.To).ToArray());
    }
}
