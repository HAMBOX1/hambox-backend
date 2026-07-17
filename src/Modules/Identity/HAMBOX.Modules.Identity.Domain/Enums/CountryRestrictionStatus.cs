namespace HAMBOX.Modules.Identity.Domain.Enums;

/// <summary>
/// The administrator-configured access status of a country in the Security Center country list.
/// Countries with no <c>CountryRestriction</c> row default to <see cref="Allowed"/>.
/// </summary>
public enum CountryRestrictionStatus
{
    Allowed = 0,
    Blocked = 1,
    TemporarilyBlocked = 2
}
