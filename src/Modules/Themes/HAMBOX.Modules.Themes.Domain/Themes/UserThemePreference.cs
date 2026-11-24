using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Themes.Domain.Themes;

/// <summary>
/// A customer's choice to keep the original HAMBOX design even when a rank or ELITE design is available. Absence of a row
/// means "use my earned design".
/// </summary>
public sealed class UserThemePreference : BaseEntity
{
    private UserThemePreference()
    {
    }

    private UserThemePreference(Guid id, string userId, bool useOriginalDesign)
        : base(id)
    {
        UserId = userId;
        UseOriginalDesign = useOriginalDesign;
    }

    public string UserId { get; private set; } = string.Empty;

    public bool UseOriginalDesign { get; private set; }

    public static UserThemePreference Create(string userId, bool useOriginalDesign) => new(Guid.NewGuid(), userId, useOriginalDesign);

    public void Set(bool useOriginalDesign) => UseOriginalDesign = useOriginalDesign;
}
