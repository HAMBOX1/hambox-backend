namespace HAMBOX.Modules.Identity.Application.Options;

/// <summary>
/// A single development admin account to seed.
/// </summary>
public sealed class DevAdminAccountSeed
{
    /// <summary>
    /// Gets or sets the admin account email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the admin account password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the admin account first name.
    /// </summary>
    public string FirstName { get; set; } = "Dev";

    /// <summary>
    /// Gets or sets the admin account last name.
    /// </summary>
    public string LastName { get; set; } = "Admin";

    /// <summary>
    /// Gets or sets the seeded role name (for example, Owner or Administrator).
    /// </summary>
    public string Role { get; set; } = "Owner";
}

/// <summary>
/// Settings for seeding development admin accounts.
/// </summary>
public sealed class DevAdminSeedSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "DevAdminSeed";

    /// <summary>
    /// Gets or sets a value indicating whether the development admin seed runs at startup.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the primary admin account email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary admin account password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary admin account first name.
    /// </summary>
    public string FirstName { get; set; } = "Dev";

    /// <summary>
    /// Gets or sets the primary admin account last name.
    /// </summary>
    public string LastName { get; set; } = "Admin";

    /// <summary>
    /// Gets or sets the primary seeded role name.
    /// </summary>
    public string Role { get; set; } = "Owner";

    /// <summary>
    /// Gets or sets additional development admin accounts to seed.
    /// </summary>
    public List<DevAdminAccountSeed> AdditionalAccounts { get; set; } = [];

    /// <summary>
    /// Returns all configured admin accounts for seeding.
    /// </summary>
    public IEnumerable<DevAdminAccountSeed> EnumerateAccounts()
    {
        if (!string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password))
        {
            yield return new DevAdminAccountSeed
            {
                Email = Email,
                Password = Password,
                FirstName = FirstName,
                LastName = LastName,
                Role = Role,
            };
        }

        foreach (var account in AdditionalAccounts)
        {
            if (!string.IsNullOrWhiteSpace(account.Email) && !string.IsNullOrWhiteSpace(account.Password))
            {
                yield return account;
            }
        }
    }
}
