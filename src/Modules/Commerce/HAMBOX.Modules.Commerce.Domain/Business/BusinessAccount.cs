using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Business;

public enum BusinessAccountStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Suspended = 3,
}

/// <summary>
/// An ELITE Business (B2B) account: a company or reseller that buys through a sales contact at agreed prices. It is a
/// commercial classification kept separate from the customer's personal loyalty rank and ELITE subscription, and it
/// needs staff approval before any wholesale price or quote is visible.
/// </summary>
public sealed class BusinessAccount : AggregateRoot
{
    private BusinessAccount()
    {
    }

    private BusinessAccount(Guid id)
        : base(id)
    {
    }

    public string UserId { get; private set; } = string.Empty;

    public string CompanyName { get; private set; } = string.Empty;

    public string? TaxId { get; private set; }

    public string Country { get; private set; } = string.Empty;

    public string? Website { get; private set; }

    public string? ContactPhone { get; private set; }

    /// <summary>What the applicant says about the business, and a reference to the verification documents staff reviewed.</summary>
    public string? Notes { get; private set; }

    public BusinessAccountStatus Status { get; private set; }

    public string? SalesRepUserId { get; private set; }

    /// <summary>Whether this account's wholesale orders earn Stars. Off by default: wholesale orders do not earn Stars (B2B-04).</summary>
    public bool StarsEligible { get; private set; }

    public string? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedOnUtc { get; private set; }

    public string? DecisionReason { get; private set; }

    public static BusinessAccount Apply(string userId, string companyName, string? taxId, string country, string? website, string? phone, string? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);
        var account = new BusinessAccount(Guid.NewGuid()) { UserId = userId };
        account.UpdateApplication(companyName, taxId, country, website, phone, notes);
        return account;
    }

    /// <summary>Applicant edits (or re-applies after a rejection). An approved account must go back to review to change its details.</summary>
    public void UpdateApplication(string companyName, string? taxId, string country, string? website, string? phone, string? notes)
    {
        CompanyName = companyName.Trim();
        TaxId = string.IsNullOrWhiteSpace(taxId) ? null : taxId.Trim();
        Country = country.Trim().ToUpperInvariant();
        Website = string.IsNullOrWhiteSpace(website) ? null : website.Trim();
        ContactPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (Status is BusinessAccountStatus.Rejected or BusinessAccountStatus.Approved)
        {
            Status = BusinessAccountStatus.Pending;
            DecisionReason = null;
        }
    }

    public void Decide(BusinessAccountStatus status, string reviewer, string? reason)
    {
        if (status == BusinessAccountStatus.Pending)
        {
            throw new ArgumentException("A decision must approve, reject or suspend.", nameof(status));
        }

        Status = status;
        ReviewedBy = reviewer;
        ReviewedOnUtc = DateTimeOffset.UtcNow;
        DecisionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void Configure(string? salesRepUserId, bool starsEligible)
    {
        SalesRepUserId = string.IsNullOrWhiteSpace(salesRepUserId) ? null : salesRepUserId.Trim();
        StarsEligible = starsEligible;
    }

    public bool IsApproved => Status == BusinessAccountStatus.Approved;
}
