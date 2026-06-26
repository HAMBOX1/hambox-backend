namespace HAMBOX.Modules.Identity.Application.Options;

/// <summary>
/// Settings for seeding a development admin account.
/// </summary>
public sealed class DevAdminSeedSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "DevAdminSeed";

    /// <summary>
    /// Gets a value indicating whether the development admin seed runs at startup.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the admin account email address.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Gets the admin account password.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Gets the admin account first name.
    /// </summary>
    public string FirstName { get; init; } = "Dev";

    /// <summary>
    /// Gets the admin account last name.
    /// </summary>
    public string LastName { get; init; } = "Admin";

    /// <summary>
    /// Gets the seeded role name (for example, Admin or SuperAdmin).
    /// </summary>
    public string Role { get; init; } = "Admin";
}
