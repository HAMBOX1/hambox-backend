using HAMBOX.Modules.Identity.Domain.Enums;

namespace HAMBOX.Modules.Identity.Domain.Security;

/// <summary>
/// An administrator-set access override for one ISO-3166 alpha-2 country code. Countries with
/// no row here are implicitly <see cref="CountryRestrictionStatus.Allowed"/> — this table only
/// ever holds explicit overrides, never the full country list (enumerated at read time from
/// <see cref="System.Globalization.RegionInfo"/> instead of being hardcoded/seeded).
/// </summary>
public sealed class CountryRestriction : BlockListEntry
{
    private CountryRestriction()
    {
    }

    private CountryRestriction(
        Guid id,
        string countryCode,
        CountryRestrictionStatus status,
        string reason,
        string? notes,
        DateTimeOffset? expiresOnUtc)
        : base(id, reason, notes, expiresOnUtc)
    {
        CountryCode = countryCode;
        Status = status;
    }

    /// <summary>
    /// Gets the ISO-3166 alpha-2 country code (e.g. <c>EG</c>, <c>US</c>).
    /// </summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the administrator-set access status for this country.
    /// </summary>
    public CountryRestrictionStatus Status { get; private set; }

    public static CountryRestriction Create(
        string countryCode,
        CountryRestrictionStatus status,
        string reason,
        string? notes = null,
        DateTimeOffset? expiresOnUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        return new CountryRestriction(Guid.NewGuid(), countryCode.Trim().ToUpperInvariant(), status, reason, notes, expiresOnUtc);
    }

    public void UpdateStatus(CountryRestrictionStatus status, string reason, string? notes, DateTimeOffset? expiresOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Status = status;
        SetReason(reason, notes, expiresOnUtc);
    }
}
