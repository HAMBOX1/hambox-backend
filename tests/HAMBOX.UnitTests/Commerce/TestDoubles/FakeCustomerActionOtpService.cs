using HAMBOX.Application.Security;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>Scriptable <see cref="ICustomerActionOtpService"/>: the "correct" code and the issue outcome are set per test.</summary>
internal sealed class FakeCustomerActionOtpService : ICustomerActionOtpService
{
    public string CorrectCode { get; set; } = "123456";

    public CustomerActionOtpIssueStatus IssueStatus { get; set; } = CustomerActionOtpIssueStatus.Sent;

    public CustomerActionOtpVerifyStatus? ForcedVerifyStatus { get; set; }

    public List<(string UserId, string Purpose, string ScopeKey)> Issued { get; } = [];

    public List<(string UserId, string Purpose, string ScopeKey, string Code)> Verified { get; } = [];

    public Task<CustomerActionOtpIssueResult> IssueAsync(
        string userId, string purpose, string scopeKey, CancellationToken cancellationToken = default)
    {
        Issued.Add((userId, purpose, scopeKey));
        return Task.FromResult(IssueStatus == CustomerActionOtpIssueStatus.Sent
            ? new CustomerActionOtpIssueResult(
                IssueStatus, DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddSeconds(60), "u***@example.com")
            : new CustomerActionOtpIssueResult(IssueStatus));
    }

    public Task<CustomerActionOtpVerifyStatus> VerifyAsync(
        string userId, string purpose, string scopeKey, string code, CancellationToken cancellationToken = default)
    {
        Verified.Add((userId, purpose, scopeKey, code));
        return Task.FromResult(ForcedVerifyStatus
            ?? (code == CorrectCode ? CustomerActionOtpVerifyStatus.Verified : CustomerActionOtpVerifyStatus.Invalid));
    }
}
